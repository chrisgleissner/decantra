/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Domain.Generation
{
    /// <summary>
    /// Report containing generation statistics and metrics for telemetry/calibration.
    /// Designed to support future player-data-driven difficulty tuning.
    /// </summary>
    public sealed class LevelGenerationReport
    {
        /// <summary>
        /// The level index that was generated.
        /// </summary>
        public int LevelIndex { get; }

        /// <summary>
        /// The seed used for generation.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Number of scramble attempts before a valid level was found.
        /// </summary>
        public int AttemptsUsed { get; }

        /// <summary>
        /// Quality metrics for the final generated level.
        /// </summary>
        public LevelMetrics Metrics { get; }

        /// <summary>
        /// The optimal solution length.
        /// </summary>
        public int OptimalMoves { get; }

        /// <summary>
        /// Moves allowed for the player.
        /// </summary>
        public int MovesAllowed { get; }

        /// <summary>
        /// Number of scramble moves applied during generation.
        /// </summary>
        public int ScrambleMoves { get; }

        /// <summary>
        /// Total generation time in milliseconds.
        /// </summary>
        public long GenerationTimeMs { get; }

        /// <summary>
        /// Time spent in solver during generation in milliseconds.
        /// </summary>
        public long SolverTimeMs { get; }

        /// <summary>
        /// Time spent computing metrics in milliseconds.
        /// </summary>
        public long MetricsTimeMs { get; }

        /// <summary>
        /// Difficulty objective score for the generated level.
        /// </summary>
        public float DifficultyScore { get; }

        /// <summary>
        /// Whether quality gates were applied (false = fallback/relaxed generation).
        /// </summary>
        public bool QualityGatesApplied { get; }

        /// <summary>
        /// Rejection reason if level was initially rejected (null if first attempt succeeded).
        /// </summary>
        public string LastRejectionReason { get; }

        public LevelGenerationReport(
            int levelIndex,
            int seed,
            int attemptsUsed,
            LevelMetrics metrics,
            int optimalMoves,
            int movesAllowed,
            int scrambleMoves,
            long generationTimeMs,
            long solverTimeMs,
            long metricsTimeMs,
            float difficultyScore,
            bool qualityGatesApplied,
            string lastRejectionReason)
        {
            LevelIndex = levelIndex;
            Seed = seed;
            AttemptsUsed = attemptsUsed;
            Metrics = metrics;
            OptimalMoves = optimalMoves;
            MovesAllowed = movesAllowed;
            ScrambleMoves = scrambleMoves;
            GenerationTimeMs = generationTimeMs;
            SolverTimeMs = solverTimeMs;
            MetricsTimeMs = metricsTimeMs;
            DifficultyScore = difficultyScore;
            QualityGatesApplied = qualityGatesApplied;
            LastRejectionReason = lastRejectionReason;
        }

        public override string ToString()
        {
            return $"LevelGenerationReport[L{LevelIndex} seed={Seed} attempts={AttemptsUsed} optimal={OptimalMoves} allowed={MovesAllowed} score={DifficultyScore:F2} totalMs={GenerationTimeMs}]";
        }
    }
}
