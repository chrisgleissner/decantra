/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Generates levels with monotonically increasing intrinsic difficulty.
    /// 
    /// For each level N, generates K candidate levels in parallel and selects
    /// the one whose intrinsic difficulty is closest to the target difficulty curve.
    /// 
    /// This ensures:
    /// - Determinism: Same level index always produces the same level
    /// - Monotonicity: Intrinsic difficulty increases roughly linearly to level 200
    /// - Performance: Parallel generation across multiple cores
    /// - Unlimited levels: Works for any level index
    /// </summary>
    public sealed class MonotonicLevelSelector
    {
        /// <summary>Number of candidate levels to generate per level index.</summary>
        public const int DefaultCandidateCount = 16;

        /// <summary>Level at which difficulty starts to plateau.</summary>
        public const int PlateauStartLevel = 200;

        /// <summary>Minimum intrinsic difficulty for level 1.</summary>
        public const int MinDifficulty = 35;

        /// <summary>Maximum intrinsic difficulty for plateau (level 200+).</summary>
        public const int MaxDifficulty = 92;

        /// <summary>
        /// Window size for computing minimum acceptable difficulty.
        /// Levels within this window of each other can have some variance,
        /// but comparing across windows must show increase.
        /// </summary>
        public const int WindowSize = 10;

        /// <summary>
        /// Maximum allowed difficulty drop from any level to the next.
        /// Small drops are tolerable if the overall trend increases.
        /// </summary>
        public const int MaxAllowedDropBetweenLevels = 15;

        private readonly BfsSolver _solver;
        private readonly int _candidateCount;

        public Action<string>? Log { get; set; }

        public MonotonicLevelSelector(BfsSolver solver, int candidateCount = DefaultCandidateCount)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _candidateCount = Math.Max(1, candidateCount);
        }

        /// <summary>
        /// Computes the target intrinsic difficulty for a given level index.
        /// Returns a value that increases linearly from MinDifficulty at level 1
        /// to MaxDifficulty at level PlateauStartLevel, then stays constant.
        /// </summary>
        public static int TargetDifficulty(int levelIndex)
        {
            if (levelIndex <= 1) return MinDifficulty;
            if (levelIndex >= PlateauStartLevel) return MaxDifficulty;

            // Linear interpolation from level 1 to PlateauStartLevel
            double t = (levelIndex - 1.0) / (PlateauStartLevel - 1.0);
            return MinDifficulty + (int)Math.Round(t * (MaxDifficulty - MinDifficulty));
        }

        /// <summary>
        /// Gets the minimum acceptable difficulty for a level.
        /// Uses a window-based approach to allow local variance while maintaining overall trend.
        /// The minimum is based on the target from WindowSize levels ago minus slack.
        /// </summary>
        public static int MinimumDifficulty(int levelIndex)
        {
            // For early levels, be very lenient
            if (levelIndex <= 6)
                return 1; // Tutorial band, accept anything

            // For levels 7-17 (band B/C with 4-5 colors), start setting floor
            if (levelIndex <= 17)
            {
                // Floor rises from ~30 to ~45 across this range
                return 30 + (levelIndex - 7);
            }

            // For levels 18+ (with sinks), require higher difficulty
            // The reference is WindowSize levels back
            int referenceLevel = Math.Max(18, levelIndex - WindowSize);
            int referenceTarget = TargetDifficulty(referenceLevel);

            // Allow slack below the reference
            return Math.Max(45, referenceTarget - 10);
        }

        /// <summary>
        /// Computes a deterministic seed for a given level index and candidate index.
        /// Uses a mixing function to ensure good distribution across candidate space.
        /// </summary>
        public static int ComputeCandidateSeed(int levelIndex, int candidateIndex)
        {
            // FNV-1a-inspired hash for good distribution
            unchecked
            {
                const int Prime = 16777619;
                const int Offset = unchecked((int)2166136261);

                int hash = Offset;
                hash = (hash ^ levelIndex) * Prime;
                hash = (hash ^ candidateIndex) * Prime;
                hash = (hash ^ (levelIndex >> 16)) * Prime;
                hash = (hash ^ (candidateIndex * 31)) * Prime;

                return Math.Abs(hash);
            }
        }

        /// <summary>
        /// Generates a level with monotonically increasing difficulty.
        /// Selects from multiple candidates the one closest to the target difficulty.
        /// </summary>
        /// <param name="levelIndex">The level index (1-based).</param>
        /// <param name="token">Cancellation token for async operations.</param>
        /// <returns>The selected level state and its metadata.</returns>
        public MonotonicLevelResult Generate(int levelIndex, CancellationToken token = default)
        {
            if (levelIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(levelIndex), "Level index must be positive.");

            int targetDiff = TargetDifficulty(levelIndex);
            var profile = LevelDifficultyEngine.GetProfile(levelIndex);

            Log?.Invoke($"[Monotonic] Level {levelIndex}: target difficulty={targetDiff}, candidates={_candidateCount}");

            // Generate candidates in parallel
            var candidates = new CandidateResult[_candidateCount];
            var generators = new LevelGenerator[_candidateCount];

            for (int i = 0; i < _candidateCount; i++)
            {
                generators[i] = new LevelGenerator(new BfsSolver());
            }

            Parallel.For(0, _candidateCount, new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, i =>
            {
                int seed = ComputeCandidateSeed(levelIndex, i);
                var generator = generators[i];

                try
                {
                    var state = generator.Generate(seed, profile);
                    var report = generator.LastReport;
                    var metrics = report?.Metrics ?? LevelMetrics.Empty;
                    int optimalMoves = report?.OptimalMoves ?? state.OptimalMoves;
                    int intrinsicDiff = DifficultyScorer.ComputeIntrinsicDifficulty100(metrics, optimalMoves);

                    candidates[i] = new CandidateResult
                    {
                        Seed = seed,
                        CandidateIndex = i,
                        State = state,
                        Metrics = metrics,
                        OptimalMoves = optimalMoves,
                        IntrinsicDifficulty = intrinsicDiff,
                        IsValid = true
                    };

                    Log?.Invoke($"[Monotonic]   Candidate {i}: seed={seed}, difficulty={intrinsicDiff}, optimal={optimalMoves}");
                }
                catch (Exception ex)
                {
                    candidates[i] = new CandidateResult
                    {
                        Seed = seed,
                        CandidateIndex = i,
                        IsValid = false,
                        ErrorMessage = ex.Message
                    };

                    Log?.Invoke($"[Monotonic]   Candidate {i}: FAILED - {ex.Message}");
                }
            });

            // Select the best candidate using a target-focused selection
            // We want to be close to the target difficulty, allowing for natural fluctuations.
            CandidateResult best = null;
            int bestScore = int.MaxValue;

            for (int i = 0; i < _candidateCount; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsValid) continue;

                // Score: distance from target (lower is better)
                int score = Math.Abs(candidate.IntrinsicDifficulty - targetDiff);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == null)
            {
                throw new InvalidOperationException($"Failed to generate any valid candidate for level {levelIndex}");
            }

            Log?.Invoke($"[Monotonic] Selected candidate {best.CandidateIndex}: difficulty={best.IntrinsicDifficulty} (target={targetDiff}, delta={best.IntrinsicDifficulty - targetDiff})");

            return new MonotonicLevelResult
            {
                LevelIndex = levelIndex,
                SelectedSeed = best.Seed,
                SelectedCandidateIndex = best.CandidateIndex,
                State = best.State,
                Metrics = best.Metrics,
                OptimalMoves = best.OptimalMoves,
                IntrinsicDifficulty = best.IntrinsicDifficulty,
                TargetDifficulty = targetDiff,
                CandidatesEvaluated = _candidateCount,
                AllCandidates = candidates
            };
        }

        /// <summary>
        /// Generates a level using single-candidate mode for speed when monotonicity
        /// is less critical (e.g., fallback or emergency generation).
        /// </summary>
        public LevelState GenerateFast(int levelIndex, int seed)
        {
            var profile = LevelDifficultyEngine.GetProfile(levelIndex);
            var generator = new LevelGenerator(_solver);
            return generator.Generate(seed, profile);
        }

        /// <summary>Represents a single candidate level during selection.</summary>
        public sealed class CandidateResult
        {
            public int Seed { get; set; }
            public int CandidateIndex { get; set; }
            public LevelState State { get; set; }
            public LevelMetrics Metrics { get; set; }
            public int OptimalMoves { get; set; }
            public int IntrinsicDifficulty { get; set; }
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>Result of monotonic level generation including selection metadata.</summary>
        public sealed class MonotonicLevelResult
        {
            public int LevelIndex { get; set; }
            public int SelectedSeed { get; set; }
            public int SelectedCandidateIndex { get; set; }
            public LevelState State { get; set; }
            public LevelMetrics Metrics { get; set; }
            public int OptimalMoves { get; set; }
            public int IntrinsicDifficulty { get; set; }
            public int TargetDifficulty { get; set; }
            public int CandidatesEvaluated { get; set; }
            public CandidateResult[] AllCandidates { get; set; }
        }
    }
}
