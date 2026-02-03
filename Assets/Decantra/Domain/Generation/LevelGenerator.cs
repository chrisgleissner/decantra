/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;

namespace Decantra.Domain.Generation
{
    public sealed class LevelGenerator
    {
        private readonly BfsSolver _solver;
        private readonly DifficultyObjective _difficultyObjective;

        /// <summary>
        /// Most recent generation report (for telemetry/debugging).
        /// </summary>
        public LevelGenerationReport LastReport { get; private set; }

        public Action<string>? Log { get; set; }

        public LevelGenerator(BfsSolver solver)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _difficultyObjective = DifficultyObjective.Default;
        }

        public LevelState Generate(int seed, DifficultyProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var overallTimer = Stopwatch.StartNew();
            var rng = new Random(seed);
            var plans = CreateBottlePlans(profile, rng);
            var solved = CreateSolvedBottles(plans);
            int scrambleMovesTarget = profile.ReverseMoves;

            var scrambleTimer = Stopwatch.StartNew();
            LevelState bestCandidate = null;
            LevelMetrics bestMetrics = null;
            float bestScore = float.MinValue;
            int bestDifficulty100 = 1;
            string lastFailure = null;
            int optimal = -1;
            int movesAllowed = 0;
            int scrambleMoves = 0;
            long solveMs = 0;
            long metricsMs = 0;
            int attemptsUsed = 0;
            bool qualityGatesApplied = true;

            const int minOptimalMoves = 2;
            int maxAttempts = ResolveMaxAttempts(profile.LevelIndex);
            int candidatesPerAttempt = ResolveCandidatesPerAttempt(profile.LevelIndex);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                attemptsUsed = attempt + 1;

                // Relaxed mode: after many attempts, disable strict quality gates
                bool relaxedMode = attempt >= ResolveRelaxedAttempt(profile.LevelIndex, maxAttempts);
                if (relaxedMode)
                {
                    qualityGatesApplied = false;
                }
                var thresholds = relaxedMode
                    ? QualityThresholds.ForBand(profile.Band).Relaxed()
                    : QualityThresholds.ForBand(profile.Band);
                int minOptimalForAttempt = ComputeMinOptimalMoves(profile, relaxedMode);

                // Generate multiple scramble candidates and pick the best
                for (int candidateIdx = 0; candidateIdx < candidatesPerAttempt; candidateIdx++)
                {
                    int candidateSeed = seed + attempt * 7919 + candidateIdx * 1307;
                    var attemptRng = new Random(candidateSeed);
                    int attemptScrambleTarget = Math.Max(4, scrambleMovesTarget - attempt / 3);

                    // Adaptive strategy: If we fail repeatedly, ensure we keep at least one empty bottle
                    int minEmptyDuringScramble = profile.LevelIndex <= 6
                        ? 1
                        : (attempt >= 5 && profile.EmptyBottleCount > 1) ? 1 : 0;
                    int maxEmptyDuringScramble = Math.Max(profile.EmptyBottleCount + 2, profile.EmptyBottleCount);

                    var attemptState = new LevelState(CloneBottles(solved), 0, 0, 0, profile.LevelIndex, seed);
                    bool preventEmptySourceDuringScramble = profile.LevelIndex <= 6;
                    int appliedMoves = ScrambleState(attemptState, attemptRng, attemptScrambleTarget, minEmptyDuringScramble, maxEmptyDuringScramble, preventEmptySourceDuringScramble);

                    if (appliedMoves <= 0)
                    {
                        lastFailure = "scramble";
                        continue;
                    }

                    if (profile.LevelIndex <= 6 && HasSolvedBottle(attemptState.Bottles))
                    {
                        // Best-effort only for early tutorial levels; do not fail generation if we cannot break solved bottles.
                        BreakSolvedBottles(attemptState, attemptRng, minEmptyDuringScramble, maxEmptyDuringScramble, Math.Max(1, appliedMoves) * 6);
                    }

                    if (CountEmpty(attemptState.Bottles) > profile.EmptyBottleCount)
                    {
                        if (!ReduceEmptyCount(attemptState, attemptRng, profile.EmptyBottleCount, Math.Max(1, appliedMoves) * 8))
                        {
                            lastFailure = "reduce_empty";
                            continue;
                        }
                    }

                    if (!IsAcceptableStart(attemptState, profile))
                    {
                        lastFailure = "accept";
                        continue;
                    }

                    if (!LevelIntegrity.TryValidate(attemptState, out string integrityError))
                    {
                        lastFailure = $"integrity:{integrityError}";
                        continue;
                    }

                    if (!LevelStartValidator.TryValidate(attemptState, out string startError))
                    {
                        lastFailure = $"start:{startError}";
                        continue;
                    }

                    // Check structural complexity (Requirement G)
                    if (!relaxedMode && !IsStructurallyComplex(attemptState.Bottles, profile.LevelIndex, profile.ColorCount, profile.EmptyBottleCount))
                    {
                        lastFailure = "structural_complexity";
                        continue;
                    }

                    // Check empty-bottle chain risk (Requirement D)
                    if (!relaxedMode && HasEmptyBottleChainRisk(attemptState, profile.EmptyBottleCount))
                    {
                        lastFailure = "empty_chain_risk";
                        continue;
                    }

                    var solveTimer = Stopwatch.StartNew();
                    var solveResult = _solver.SolveWithPath(
                        attemptState,
                        ResolveSolveNodeLimit(profile.LevelIndex),
                        ResolveSolveTimeLimitMs(profile.LevelIndex));
                    solveTimer.Stop();
                    long candidateSolveMs = solveTimer.ElapsedMilliseconds;

                    if (solveResult.OptimalMoves < 0)
                    {
                        // Handle timeout case
                        if (solveResult.Status == SolverStatus.Timeout && appliedMoves >= minOptimalMoves)
                        {
                            int estimatedOptimal = (int)(appliedMoves * 0.7f);
                            if (estimatedOptimal < minOptimalForAttempt)
                            {
                                lastFailure = "min_optimal_est";
                                continue;
                            }
                            int estMovesAllowed = Math.Max(2, MoveAllowanceCalculator.ComputeMovesAllowed(profile, estimatedOptimal));

                            // For timeout cases, use estimated metrics
                            var timeoutMetrics = new LevelMetrics(0.5f, 1.5f, 1, 0.5f, 0.1f, 1,
                                CountMixedBottles(attemptState.Bottles),
                                CountDistinctSignatures(attemptState.Bottles),
                                CountTopColorVariety(attemptState.Bottles));

                            float timeoutScore = _difficultyObjective.Score(timeoutMetrics)
                                + 0.25f * Clamp01((estimatedOptimal - minOptimalForAttempt) / (float)Math.Max(1, minOptimalForAttempt));

                            if (timeoutScore > bestScore)
                            {
                                bestScore = timeoutScore;
                                bestMetrics = timeoutMetrics;
                                bestDifficulty100 = DifficultyScorer.ComputeDifficulty100(timeoutMetrics, estimatedOptimal);
                                bestCandidate = new LevelState(attemptState.Bottles, 0, estMovesAllowed, estimatedOptimal, profile.LevelIndex, seed, appliedMoves);
                                optimal = estimatedOptimal;
                                movesAllowed = estMovesAllowed;
                                scrambleMoves = appliedMoves;
                                solveMs = candidateSolveMs;
                            }
                            continue;
                        }

                        lastFailure = solveResult.Status == SolverStatus.Timeout ? "solver_timeout" : "solver_unsolvable";
                        continue;
                    }

                    if (solveResult.OptimalMoves < minOptimalMoves)
                    {
                        lastFailure = "min_optimal";
                        continue;
                    }
                    if (solveResult.OptimalMoves < minOptimalForAttempt)
                    {
                        lastFailure = "min_optimal_floor";
                        continue;
                    }

                    // Compute metrics (Requirements A, B, C)
                    var metricsTimer = Stopwatch.StartNew();
                    var pathMetrics = MetricsComputer.ComputePathMetrics(attemptState, solveResult.Path);
                    var structuralMetrics = MetricsComputer.ComputeStructuralMetrics(attemptState);

                    // Compute trap score (Requirement C) - budget-limited for performance
                    float trapScore = 0f;
                    if (!relaxedMode && solveResult.Path.Count > 0)
                    {
                        trapScore = MetricsComputer.ComputeTrapScore(
                            attemptState,
                            solveResult.Path,
                            _solver,
                            sampleCount: ResolveTrapSampleCount(profile.LevelIndex),
                            nodeBudget: ResolveTrapNodeBudget(profile.LevelIndex));
                    }

                    // Estimate solution multiplicity (Requirement B) - only for non-relaxed mode
                    int multiplicity = 1;
                    if (!relaxedMode && solveResult.OptimalMoves <= 20 && ShouldEstimateMultiplicity(profile.LevelIndex))
                    {
                        multiplicity = MetricsComputer.EstimateSolutionMultiplicity(
                            attemptState,
                            solveResult.OptimalMoves,
                            maxSolutions: 3,
                            nearOptimalMargin: 1,
                            maxVisited: ResolveMultiplicityNodeLimit(profile.LevelIndex),
                            maxMillis: ResolveMultiplicityTimeLimitMs(profile.LevelIndex));
                    }

                    metricsTimer.Stop();
                    long candidateMetricsMs = metricsTimer.ElapsedMilliseconds;

                    var candidateMetrics = new LevelMetrics(
                        pathMetrics.ForcedMoveRatio,
                        pathMetrics.AverageBranchingFactor,
                        pathMetrics.DecisionDepth,
                        pathMetrics.EmptyBottleUsageRatio,
                        trapScore,
                        multiplicity,
                        structuralMetrics.MixedBottleCount,
                        structuralMetrics.DistinctSignatureCount,
                        structuralMetrics.TopColorVariety);

                    // Quality gate check (Requirements A-G)
                    if (!relaxedMode && !thresholds.Passes(candidateMetrics, out string failureReason))
                    {
                        lastFailure = $"quality:{failureReason}";
                        ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                        continue;
                    }

                    // Compute difficulty score for hill-climb selection
                    // Apply a diminishing-returns bonus based on surplus optimal moves.
                    float surplusRatio = Clamp01((solveResult.OptimalMoves - minOptimalForAttempt) / (float)Math.Max(1, minOptimalForAttempt));
                    float candidateScore = _difficultyObjective.Score(candidateMetrics)
                        + 0.25f * (float)Math.Sqrt(surplusRatio);

                    // Keep best candidate
                    if (candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        bestMetrics = candidateMetrics;
                        bestDifficulty100 = DifficultyScorer.ComputeDifficulty100(candidateMetrics, solveResult.OptimalMoves);
                        int candidateMovesAllowed = Math.Max(2, MoveAllowanceCalculator.ComputeMovesAllowed(profile, solveResult.OptimalMoves));
                        bestCandidate = new LevelState(attemptState.Bottles, 0, candidateMovesAllowed, solveResult.OptimalMoves, profile.LevelIndex, seed, appliedMoves);
                        optimal = solveResult.OptimalMoves;
                        movesAllowed = candidateMovesAllowed;
                        scrambleMoves = appliedMoves;
                        solveMs = candidateSolveMs;
                        metricsMs = candidateMetricsMs;
                    }
                }

                // If we have a passing candidate, use it
                if (bestCandidate != null && qualityGatesApplied)
                {
                    break;
                }
            }
            scrambleTimer.Stop();

            if (bestCandidate == null)
            {
                throw new InvalidOperationException($"Failed to scramble a valid level state ({lastFailure ?? "unknown"})");
            }

            overallTimer.Stop();

            // Create generation report (Requirement F)
            LastReport = new LevelGenerationReport(
                profile.LevelIndex,
                seed,
                attemptsUsed,
                bestMetrics ?? LevelMetrics.Empty,
                optimal,
                movesAllowed,
                scrambleMoves,
                overallTimer.ElapsedMilliseconds,
                solveMs,
                metricsMs,
                bestScore,
                bestDifficulty100,
                qualityGatesApplied,
                lastFailure);

            Log?.Invoke($"LevelGenerator.Generate seed={seed} level={profile.LevelIndex} scrambleMoves={bestCandidate.ScrambleMoves} movesAllowed={movesAllowed} score={bestScore:F2} attempts={attemptsUsed} scrambleMs={scrambleTimer.ElapsedMilliseconds} solveMs={solveMs} totalMs={overallTimer.ElapsedMilliseconds} metrics={bestMetrics}");

            return bestCandidate;
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        private void ReportReject(int levelIndex, int seed, int attempt, string reason)
        {
            Log?.Invoke($"LevelGenerator.Reject level={levelIndex} seed={seed} attempt={attempt} reason={reason}");
        }

        private static int ResolveMaxAttempts(int levelIndex)
        {
            if (levelIndex <= 6) return 6;
            if (levelIndex >= 100) return 10;
            if (levelIndex >= 60) return 14;
            return 20;
        }

        private static int ResolveCandidatesPerAttempt(int levelIndex)
        {
            if (levelIndex <= 6) return 1;
            if (levelIndex >= 100) return 1;
            if (levelIndex >= 60) return 2;
            return 3;
        }

        private static int ResolveRelaxedAttempt(int levelIndex, int maxAttempts)
        {
            if (levelIndex <= 6) return Math.Min(3, maxAttempts - 1);
            if (levelIndex >= 100) return Math.Min(5, maxAttempts - 1);
            if (levelIndex >= 60) return Math.Min(8, maxAttempts - 1);
            return Math.Min(12, maxAttempts - 1);
        }

        private static int ResolveSolveTimeLimitMs(int levelIndex)
        {
            if (levelIndex >= 100) return 1200;
            if (levelIndex >= 60) return 1500;
            return 2000;
        }

        private static int ResolveSolveNodeLimit(int levelIndex)
        {
            if (levelIndex >= 100) return 500_000;
            if (levelIndex >= 60) return 800_000;
            return 1_200_000;
        }

        private static int ResolveTrapSampleCount(int levelIndex)
        {
            if (levelIndex >= 100) return 4;
            if (levelIndex >= 60) return 6;
            return 10;
        }

        private static int ResolveTrapNodeBudget(int levelIndex)
        {
            if (levelIndex >= 100) return 500;
            if (levelIndex >= 60) return 900;
            return 1500;
        }

        private static bool ShouldEstimateMultiplicity(int levelIndex)
        {
            return levelIndex < 60;
        }

        private static int ResolveMultiplicityNodeLimit(int levelIndex)
        {
            if (levelIndex >= 50) return 2000;
            return 5000;
        }

        private static int ResolveMultiplicityTimeLimitMs(int levelIndex)
        {
            if (levelIndex >= 50) return 30;
            return 60;
        }

        /// <summary>
        /// Checks for empty-bottle chain risk (Requirement D).
        /// Returns true if multiple empties exist and are immediately fillable by many sources.
        /// </summary>
        private static bool HasEmptyBottleChainRisk(LevelState state, int emptyBottleCount)
        {
            if (emptyBottleCount <= 1) return false;

            int emptyCount = CountEmpty(state.Bottles);
            if (emptyCount <= 1) return false;

            // Count how many sources can pour into each empty bottle
            int totalFillOptions = 0;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var target = state.Bottles[i];
                if (!target.IsEmpty) continue;
                if (target.IsSink) continue;

                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    var source = state.Bottles[j];
                    if (source.IsEmpty) continue;

                    int amount = MoveRules.GetPourAmount(state, j, i);
                    if (amount > 0)
                    {
                        totalFillOptions++;
                    }
                }
            }

            // Chain risk if too many fill options exist across empties
            // Threshold: more than 4 immediate fill options suggests mechanical chains
            return totalFillOptions > 4;
        }

        /// <summary>
        /// Counts bottles with mixed colors (more than one color).
        /// </summary>
        private static int CountMixedBottles(IReadOnlyList<Bottle> bottles)
        {
            int count = 0;
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (!bottle.IsEmpty && !bottle.IsSingleColorOrEmpty())
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Counts distinct bottle content signatures.
        /// </summary>
        private static int CountDistinctSignatures(IReadOnlyList<Bottle> bottles)
        {
            var signatures = new HashSet<string>();
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (!bottle.IsEmpty)
                {
                    signatures.Add(BottleSignature(bottle));
                }
            }
            return signatures.Count;
        }

        /// <summary>
        /// Counts distinct colors appearing at top positions.
        /// </summary>
        private static int CountTopColorVariety(IReadOnlyList<Bottle> bottles)
        {
            var topColors = new HashSet<ColorId>();
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                var topColor = bottle.TopColor;
                if (topColor.HasValue)
                {
                    topColors.Add(topColor.Value);
                }
            }
            return topColors.Count;
        }

        private static int ScrambleState(LevelState state, Random rng, int moves, int minEmptyCount, int maxEmptyCount, bool preventEmptySource)
        {
            if (state == null) return 0;
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (moves <= 0) return 0;

            int applied = 0;
            int guard = Math.Max(40, moves * 50);
            var buffer = new List<Move>(state.Bottles.Count * state.Bottles.Count);
            Move? lastMove = null;

            while (applied < moves && guard-- > 0)
            {
                var candidates = EnumerateScrambleMovePairs(state, buffer, lastMove, preventEmptySource);
                if (candidates.Count == 0)
                {
                    break;
                }

                var move = candidates[rng.Next(candidates.Count)];
                int poured;
                if (!TryApplyScrambleMove(state, move.Source, move.Target, rng, minEmptyCount, maxEmptyCount, preventEmptySource, out poured))
                {
                    continue;
                }

                lastMove = new Move(move.Source, move.Target, poured);
                applied++;
            }

            return applied;
        }

        private static List<Move> EnumerateScrambleMovePairs(LevelState state, List<Move> buffer, Move? lastMove, bool preventEmptySource)
        {
            buffer.Clear();
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var source = state.Bottles[i];
                if (source.IsEmpty) continue;
                // CRITICAL: Sink bottles cannot be sources in forward gameplay,
                // so they must not be sources during reverse scrambling either
                if (source.IsSink) continue;

                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    if (lastMove.HasValue && lastMove.Value.Source == j && lastMove.Value.Target == i)
                    {
                        continue;
                    }

                    if (state.Bottles[j].IsSink) continue;

                    var target = state.Bottles[j];
                    int maxAmount = GetMaxReversibleReverseAmount(source, target, preventEmptySource);
                    if (maxAmount <= 0) continue;
                    buffer.Add(new Move(i, j, maxAmount));
                }
            }
            return buffer;
        }

        private static bool TryApplyScrambleMove(LevelState state, int sourceIndex, int targetIndex, Random rng, int minEmptyCount, int maxEmptyCount, bool preventEmptySource, out int appliedAmount)
        {
            appliedAmount = 0;
            if (state == null) return false;
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (sourceIndex == targetIndex) return false;
            if (sourceIndex < 0 || sourceIndex >= state.Bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= state.Bottles.Count) return false;

            var source = state.Bottles[sourceIndex];
            var target = state.Bottles[targetIndex];
            if (target.IsSink) return false;
            int maxAmount = GetMaxReversibleReverseAmount(source, target, preventEmptySource);
            // Scramble must respect the "Pour All" rule of the game.
            // If we reverse-pour a partial amount, the forward solution might require a partial pour 
            // which is not allowed by the game rules (MoveRules).
            // Therefore, always move the maximum reversible amount to simulate a valid inverse move.
            int amount = maxAmount;
            int emptyCount = CountEmpty(state.Bottles);
            bool targetWasEmpty = target.IsEmpty;
            bool sourceBecomesEmpty = source.Count == amount;

            int newEmptyCount = emptyCount;
            if (targetWasEmpty) newEmptyCount--;
            if (sourceBecomesEmpty) newEmptyCount++;

            if (newEmptyCount < minEmptyCount || newEmptyCount > maxEmptyCount)
            {
                return false;
            }

            if (!source.TryReversePourInto(target, amount))
            {
                return false;
            }

            appliedAmount = amount;
            return true;
        }

        private static bool IsAcceptableStart(LevelState state, DifficultyProfile profile)
        {
            if (state == null || profile == null) return false;
            if (state.IsWin()) return false;

            int emptyCount = CountEmpty(state.Bottles);
            if (emptyCount > profile.EmptyBottleCount) return false;

            // Early tutorial levels can still start with solved bottles if other constraints pass.

            int distinct = CountDistinctColors(state.Bottles);
            if (profile.LevelIndex > 3)
            {
                if (distinct < Math.Min(profile.ColorCount, 2)) return false;
            }

            return true;
        }

        private static bool ReduceEmptyCount(LevelState state, Random rng, int targetEmptyCount, int maxAttempts)
        {
            if (state == null) return false;
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (targetEmptyCount < 0) return false;

            int attempts = Math.Max(20, maxAttempts);
            var candidates = new List<Move>(state.Bottles.Count * state.Bottles.Count);

            while (CountEmpty(state.Bottles) > targetEmptyCount && attempts-- > 0)
            {
                candidates.Clear();
                int emptyCount = CountEmpty(state.Bottles);
                for (int i = 0; i < state.Bottles.Count; i++)
                {
                    var source = state.Bottles[i];
                    if (source.IsEmpty) continue;
                    if (source.Count <= 1) continue;

                    for (int j = 0; j < state.Bottles.Count; j++)
                    {
                        if (i == j) continue;
                        var target = state.Bottles[j];
                        if (target.IsSink) continue;
                        if (!target.IsEmpty) continue;

                        int maxAmount = GetMaxReversibleReverseAmount(source, target, preventEmptySource: true);
                        if (maxAmount <= 0) continue;

                        candidates.Add(new Move(i, j, maxAmount));
                    }
                }

                if (candidates.Count == 0)
                {
                    return false;
                }

                var pick = candidates[rng.Next(candidates.Count)];
                int amount = pick.Amount;
                if (!state.Bottles[pick.Source].TryReversePourInto(state.Bottles[pick.Target], amount))
                {
                    return false;
                }
            }

            return CountEmpty(state.Bottles) <= targetEmptyCount;
        }

        private static int GetMaxReverseAmount(Bottle source, Bottle target)
        {
            if (source == null || target == null) return 0;
            int maxAmount = Math.Min(source.ContiguousTopCount, target.FreeSpace);
            return Math.Max(0, maxAmount);
        }

        /// <summary>
        /// Computes the maximum reverse-pour amount that ensures reversibility.
        /// A reverse move is only valid if it can be undone by a forward move.
        /// </summary>
        private static int GetMaxReversibleReverseAmount(Bottle source, Bottle target, bool preventEmptySource)
        {
            if (source == null || target == null) return 0;
            var color = source.TopColor;
            if (!color.HasValue) return 0;

            // CRITICAL: If target already has the same top color as source, the inverse forward  
            // move would pour more than the reverse amount, breaking reversibility.
            // Example: Source=[R,R,B], Target=[R]. Reverse pour 2R -> Source=[_,_,B], Target=[R,R,R]
            // Forward reverse: Target top=R (3), Source top=B. R!=B so forward move impossible.
            var targetTop = target.TopColor;
            if (targetTop.HasValue && targetTop.Value == color.Value)
            {
                return 0;
            }

            int maxAmount = Math.Min(source.ContiguousTopCount, target.FreeSpace);
            if (maxAmount <= 0) return 0;

            int maxAllowed = maxAmount;

            // If removing the entire top block would reveal a different color beneath,
            // then the inverse forward move would become illegal (top colors would mismatch).
            // Example: Source=[R,R,B], maxAmount=2. After reverse: Source=[_,_,B] top=B
            // For forward reverse, target would need top=B, but if target was empty,
            // it now has top=R. Can't pour B->R.
            if (source.ContiguousTopCount == maxAmount && source.Count > maxAmount)
            {
                maxAllowed = Math.Min(maxAllowed, source.ContiguousTopCount - 1);
            }

            if (preventEmptySource)
            {
                maxAllowed = Math.Min(maxAllowed, source.Count - 1);
            }

            return Math.Max(0, maxAllowed);
        }

        /// <summary>
        /// Computes a relaxed maximum reverse-pour amount that keeps the source top color stable
        /// (or empty), without enforcing strict forward-reversibility on target top color.
        /// </summary>
        private static int GetMaxRelaxedReverseAmount(Bottle source, Bottle target, bool preventEmptySource)
        {
            if (source == null || target == null) return 0;
            int maxAmount = Math.Min(source.ContiguousTopCount, target.FreeSpace);
            if (maxAmount <= 0) return 0;

            int maxAllowed = maxAmount;
            if (source.ContiguousTopCount == maxAmount && source.Count > maxAmount)
            {
                maxAllowed = Math.Min(maxAllowed, source.ContiguousTopCount - 1);
            }

            if (preventEmptySource)
            {
                maxAllowed = Math.Min(maxAllowed, source.Count - 1);
            }

            return Math.Max(0, maxAllowed);
        }

        private static int CountDistinctColors(IReadOnlyList<Bottle> bottles)
        {
            var distinct = new HashSet<ColorId>();
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                for (int s = 0; s < bottle.Slots.Count; s++)
                {
                    var color = bottle.Slots[s];
                    if (color.HasValue)
                    {
                        distinct.Add(color.Value);
                    }
                }
            }
            return distinct.Count;
        }

        private static int CountEmpty(IReadOnlyList<Bottle> bottles)
        {
            int empty = 0;
            for (int i = 0; i < bottles.Count; i++)
            {
                if (bottles[i].IsEmpty) empty++;
            }
            return empty;
        }

        private static bool HasSolvedBottle(IReadOnlyList<Bottle> bottles)
        {
            for (int i = 0; i < bottles.Count; i++)
            {
                if (bottles[i].IsSolvedBottle()) return true;
            }
            return false;
        }

        private static bool BreakSolvedBottles(LevelState state, Random rng, int minEmptyCount, int maxEmptyCount, int maxAttempts)
        {
            if (state == null) return false;
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int attempts = 0;
            var candidates = new List<Move>(state.Bottles.Count * state.Bottles.Count);
            while (HasSolvedBottle(state.Bottles) && attempts < maxAttempts)
            {
                attempts++;
                candidates.Clear();
                for (int i = 0; i < state.Bottles.Count; i++)
                {
                    var source = state.Bottles[i];
                    if (!source.IsSolvedBottle()) continue;
                    // CRITICAL: Sink bottles cannot be sources in forward gameplay
                    if (source.IsSink) continue;

                    for (int j = 0; j < state.Bottles.Count; j++)
                    {
                        if (i == j) continue;
                        if (state.Bottles[j].IsSink) continue;
                        var target = state.Bottles[j];
                        int maxAmount = GetMaxReversibleReverseAmount(source, target, preventEmptySource: false);
                        if (maxAmount <= 0)
                        {
                            maxAmount = GetMaxRelaxedReverseAmount(source, target, preventEmptySource: false);
                        }
                        if (maxAmount <= 0) continue;
                        candidates.Add(new Move(i, j, maxAmount));
                    }
                }

                if (candidates.Count == 0) break;
                var move = candidates[rng.Next(candidates.Count)];
                var moveSource = state.Bottles[move.Source];
                var moveTarget = state.Bottles[move.Target];
                int moveMaxAmount = GetMaxReversibleReverseAmount(moveSource, moveTarget, preventEmptySource: false);
                if (moveMaxAmount <= 0)
                {
                    moveMaxAmount = GetMaxRelaxedReverseAmount(moveSource, moveTarget, preventEmptySource: false);
                }
                if (moveMaxAmount <= 0) continue;
                int amount = moveMaxAmount;

                int emptyCount = CountEmpty(state.Bottles);
                bool targetWasEmpty = moveTarget.IsEmpty;
                bool sourceBecomesEmpty = moveSource.Count == amount;
                int newEmptyCount = emptyCount;
                if (targetWasEmpty) newEmptyCount--;
                if (sourceBecomesEmpty) newEmptyCount++;
                if (newEmptyCount < minEmptyCount || newEmptyCount > maxEmptyCount)
                {
                    continue;
                }

                moveSource.TryReversePourInto(moveTarget, amount);
            }

            return !HasSolvedBottle(state.Bottles);
        }

        private static bool HasFullSinkBottle(IReadOnlyList<Bottle> bottles)
        {
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (bottle.IsSink && bottle.IsFull) return true;
            }
            return false;
        }

        private static int ComputeMinOptimalMoves(DifficultyProfile profile, bool relaxedMode)
        {
            float factor;
            switch (profile.Band)
            {
                case LevelBand.A:
                    factor = 0.10f;
                    break;
                case LevelBand.B:
                    factor = 0.12f;
                    break;
                case LevelBand.C:
                    factor = 0.14f;
                    break;
                case LevelBand.D:
                    factor = 0.16f;
                    break;
                case LevelBand.E:
                default:
                    factor = 0.18f;
                    break;
            }

            // High empty bottle count reduces the effective difficulty per scramble move.
            // Adjust expectation downwards to avoid rejection loops for Sink levels.
            if (profile.EmptyBottleCount >= 2)
            {
                factor *= 0.4f;
            }

            int baseMin = Math.Max(3, (int)Math.Round(profile.ReverseMoves * factor));
            if (relaxedMode)
            {
                baseMin = Math.Max(2, baseMin - 2);
            }
            return baseMin;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static bool IsStructurallyComplex(IReadOnlyList<Bottle> bottles, int levelIndex, int colorCount, int emptyCount)
        {
            if (bottles == null || bottles.Count == 0) return false;

            if (levelIndex >= 6)
            {
                var distinctCaps = new HashSet<int>();
                bool hasLarge = false;
                for (int i = 0; i < bottles.Count; i++)
                {
                    int cap = bottles[i].Capacity;
                    distinctCaps.Add(cap);
                    if (cap >= 5) hasLarge = true;
                }
                if (distinctCaps.Count < 2) return false;
                if (levelIndex >= 16 && !hasLarge) return false;
            }

            if (levelIndex <= 8)
            {
                return true;
            }

            int mixedCount = 0;
            int nonEmptyCount = 0;
            var signatures = new HashSet<string>();
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (bottle.IsEmpty) continue;
                nonEmptyCount++;
                if (!bottle.IsSingleColorOrEmpty())
                {
                    mixedCount++;
                }

                signatures.Add(BottleSignature(bottle));
            }

            int requiredMixed = colorCount <= 3 ? 1 : Math.Max(2, colorCount / 2);
            if (mixedCount < requiredMixed) return false;

            int requiredDistinct = Math.Max(3, Math.Min(bottles.Count - emptyCount, colorCount));
            if (signatures.Count < requiredDistinct) return false;

            return nonEmptyCount >= colorCount;
        }

        private static string BottleSignature(Bottle bottle)
        {
            var sb = new StringBuilder(bottle.Slots.Count + 6);
            sb.Append(bottle.IsSink ? 'S' : 'N');
            sb.Append(bottle.Capacity);
            sb.Append(':');
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var color = bottle.Slots[i];
                sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
            }
            return sb.ToString();
        }

        private static List<Bottle> CreateSolvedBottles(IReadOnlyList<BottlePlan> plans)
        {
            if (plans == null) throw new ArgumentNullException(nameof(plans));
            if (plans.Count == 0) throw new ArgumentOutOfRangeException(nameof(plans));

            var bottles = new List<Bottle>(plans.Count);
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                if (plan.Capacity <= 0) throw new InvalidOperationException("Bottle capacity must be positive.");
                var slots = new ColorId?[plan.Capacity];
                if (plan.FillColor.HasValue)
                {
                    for (int s = 0; s < plan.Capacity; s++)
                    {
                        slots[s] = plan.FillColor.Value;
                    }
                }
                bottles.Add(new Bottle(slots, plan.IsSink));
            }
            return bottles;
        }

        private static List<Bottle> CloneBottles(IReadOnlyList<Bottle> bottles)
        {
            var clone = new List<Bottle>(bottles.Count);
            for (int i = 0; i < bottles.Count; i++)
            {
                clone.Add(bottles[i].Clone());
            }
            return clone;
        }

        private static List<BottlePlan> CreateBottlePlans(DifficultyProfile profile, Random rng)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int available = Enum.GetValues(typeof(ColorId)).Length;
            if (profile.ColorCount > available)
            {
                throw new InvalidOperationException($"Color count {profile.ColorCount} exceeds available colors {available}");
            }

            var plans = new List<BottlePlan>(profile.BottleCount);
            var colorCaps = BuildColorCapacities(profile.LevelIndex, profile.ColorCount, rng);
            for (int i = 0; i < profile.ColorCount; i++)
            {
                plans.Add(new BottlePlan
                {
                    Capacity = colorCaps[i],
                    FillColor = (ColorId)i,
                    IsSink = false
                });
            }

            var emptyCaps = BuildEmptyCapacities(profile.LevelIndex, profile.EmptyBottleCount, rng);
            int sinkCount = LevelDifficultyEngine.ResolveSinkCount(profile.LevelIndex);
            // Clamp sinkCount to available empty bottles
            sinkCount = Math.Min(sinkCount, profile.EmptyBottleCount);
            for (int i = 0; i < profile.EmptyBottleCount; i++)
            {
                plans.Add(new BottlePlan
                {
                    Capacity = emptyCaps[i],
                    FillColor = null,
                    IsSink = i < sinkCount
                });
            }

            Shuffle(plans, rng);
            return plans;
        }

        /// <summary>
        /// Builds bottle capacities for color bottles using the capacity profile.
        /// Ensures minimum diversity requirements are met.
        /// </summary>
        private static List<int> BuildColorCapacities(int levelIndex, int colorCount, Random rng)
        {
            var profile = CapacityProfile.ForLevel(levelIndex);
            var capacities = new List<int>(colorCount);
            var usedCapacities = new HashSet<int>();

            // Phase 1: Add required small bottles (capacity <= 3)
            int smallAdded = 0;
            while (smallAdded < profile.MinSmallBottles && capacities.Count < colorCount)
            {
                int cap = PickFromRange(profile.CapacityPool, 2, 3, rng);
                if (cap > 0)
                {
                    capacities.Add(cap);
                    usedCapacities.Add(cap);
                    smallAdded++;
                }
                else
                {
                    break; // No small bottles available in pool
                }
            }

            // Phase 2: Add required large bottles (capacity >= 6)
            int largeAdded = 0;
            while (largeAdded < profile.MinLargeBottles && capacities.Count < colorCount)
            {
                int cap = PickFromRange(profile.CapacityPool, 6, 10, rng);
                if (cap > 0)
                {
                    capacities.Add(cap);
                    usedCapacities.Add(cap);
                    largeAdded++;
                }
                else
                {
                    break; // No large bottles available in pool
                }
            }

            // Phase 3: Fill remaining with diverse capacities
            // First, ensure minimum distinct capacities are met
            while (usedCapacities.Count < profile.MinDistinctCapacities && capacities.Count < colorCount)
            {
                // Pick a capacity we haven't used yet
                int cap = PickUnused(profile.CapacityPool, usedCapacities, rng);
                if (cap > 0)
                {
                    capacities.Add(cap);
                    usedCapacities.Add(cap);
                }
                else
                {
                    break; // All capacities used
                }
            }

            // Phase 4: Fill remaining bottles with random capacities from pool
            while (capacities.Count < colorCount)
            {
                int cap = profile.CapacityPool[rng.Next(profile.CapacityPool.Length)];
                capacities.Add(cap);
                usedCapacities.Add(cap);
            }

            Shuffle(capacities, rng);
            return capacities;
        }

        /// <summary>
        /// Picks a random capacity from the pool within the given range (inclusive).
        /// Returns 0 if no capacity in range exists in pool.
        /// </summary>
        private static int PickFromRange(int[] pool, int minCap, int maxCap, Random rng)
        {
            var candidates = new List<int>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] >= minCap && pool[i] <= maxCap)
                {
                    candidates.Add(pool[i]);
                }
            }
            if (candidates.Count == 0) return 0;
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Picks a random capacity from the pool that hasn't been used yet.
        /// Returns 0 if all capacities have been used.
        /// </summary>
        private static int PickUnused(int[] pool, HashSet<int> used, Random rng)
        {
            var candidates = new List<int>();
            for (int i = 0; i < pool.Length; i++)
            {
                if (!used.Contains(pool[i]))
                {
                    candidates.Add(pool[i]);
                }
            }
            if (candidates.Count == 0) return 0;
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Builds bottle capacities for empty bottles.
        /// Empty bottles use the same capacity profile for consistency.
        /// </summary>
        private static List<int> BuildEmptyCapacities(int levelIndex, int emptyCount, Random rng)
        {
            if (emptyCount <= 0) return new List<int>();

            var profile = CapacityProfile.ForLevel(levelIndex);
            var capacities = new List<int>(emptyCount);

            // Empty bottles get medium-sized capacities to serve as staging areas
            // Pick from the middle of the pool
            for (int i = 0; i < emptyCount; i++)
            {
                // Prefer capacities in the 3-5 range for empty bottles
                int cap = PickFromRange(profile.CapacityPool, 3, 5, rng);
                if (cap <= 0)
                {
                    // Fallback to any capacity
                    cap = profile.CapacityPool[rng.Next(profile.CapacityPool.Length)];
                }
                capacities.Add(cap);
            }

            return capacities;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        private sealed class BottlePlan
        {
            public int Capacity;
            public ColorId? FillColor;
            public bool IsSink;
        }
    }
}
