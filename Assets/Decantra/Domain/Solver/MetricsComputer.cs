/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Text;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;

namespace Decantra.Domain.Solver
{
    /// <summary>
    /// Computes decision-density metrics along the optimal solution path.
    /// </summary>
    public static class MetricsComputer
    {
        /// <summary>
        /// Computes metrics by walking the optimal solution path.
        /// </summary>
        public static PathMetrics ComputePathMetrics(LevelState initial, IReadOnlyList<Move> optimalPath)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (optimalPath == null || optimalPath.Count == 0)
            {
                return new PathMetrics(1f, 1f, 0, 0f, 0);
            }

            var state = CloneState(initial);
            int forcedMoveCount = 0;
            int totalLegalMoves = 0;
            int decisionDepth = -1;
            int emptyBottlePours = 0;
            int stateCount = 0;

            // Analyze initial state
            int initialLegalMoves = CountLegalMoves(state);
            if (initialLegalMoves == 1)
            {
                forcedMoveCount++;
            }
            else if (decisionDepth < 0 && initialLegalMoves >= 2)
            {
                decisionDepth = 0;
            }
            totalLegalMoves += initialLegalMoves;
            stateCount++;

            // Walk through each move in the optimal path
            for (int i = 0; i < optimalPath.Count; i++)
            {
                var move = optimalPath[i];

                // Check if this move pours into an empty bottle
                if (state.Bottles[move.Target].IsEmpty)
                {
                    emptyBottlePours++;
                }

                // Apply move
                state.TryApplyMove(move.Source, move.Target, out _);

                // Skip analyzing the final (win) state
                if (i == optimalPath.Count - 1)
                    break;

                // Analyze resulting state
                int legalMoves = CountLegalMoves(state);
                if (legalMoves == 1)
                {
                    forcedMoveCount++;
                }
                else if (decisionDepth < 0 && legalMoves >= 2)
                {
                    decisionDepth = i + 1;
                }
                totalLegalMoves += legalMoves;
                stateCount++;
            }

            // If no decision point found, set to path length (all forced)
            if (decisionDepth < 0)
            {
                decisionDepth = optimalPath.Count;
            }

            float forcedMoveRatio = stateCount > 0 ? (float)forcedMoveCount / stateCount : 1f;
            float avgBranchingFactor = stateCount > 0 ? (float)totalLegalMoves / stateCount : 1f;
            float emptyBottleUsageRatio = optimalPath.Count > 0 ? (float)emptyBottlePours / optimalPath.Count : 0f;

            return new PathMetrics(forcedMoveRatio, avgBranchingFactor, decisionDepth, emptyBottleUsageRatio, optimalPath.Count);
        }

        /// <summary>
        /// Estimates solution multiplicity by checking for alternative optimal or near-optimal paths.
        /// Returns 1 if only one solution exists, higher if multiple paths found.
        /// </summary>
        public static int EstimateSolutionMultiplicity(LevelState initial, int optimalLength, int maxSolutions = 3, int nearOptimalMargin = 1)
        {
            if (initial == null) return 1;
            if (optimalLength <= 0) return 1;

            // Use BFS to count distinct optimal solutions (up to maxSolutions)
            var visited = new HashSet<string>();
            var queue = new Queue<(LevelState state, int depth, string prefix)>();
            int solutionCount = 0;
            int targetLength = optimalLength + nearOptimalMargin;

            var startKey = StateEncoder.EncodeCanonical(initial);
            visited.Add(startKey);
            queue.Enqueue((CloneState(initial), 0, ""));

            while (queue.Count > 0 && solutionCount < maxSolutions)
            {
                var (state, depth, prefix) = queue.Dequeue();

                if (depth > targetLength)
                    continue;

                if (state.IsWin())
                {
                    if (depth <= targetLength)
                    {
                        solutionCount++;
                    }
                    continue;
                }

                if (depth >= targetLength)
                    continue;

                foreach (var move in EnumerateMoves(state))
                {
                    var next = CloneState(state);
                    if (!next.TryApplyMove(move.Source, move.Target, out _))
                        continue;

                    var key = StateEncoder.EncodeCanonical(next);
                    if (visited.Add(key))
                    {
                        var moveKey = $"{prefix}|{move.Source}-{move.Target}";
                        queue.Enqueue((next, depth + 1, moveKey));
                    }
                }
            }

            return Math.Max(1, solutionCount);
        }

        /// <summary>
        /// Computes trap score by sampling non-optimal moves and checking if they lead to harder states.
        /// </summary>
        public static float ComputeTrapScore(LevelState initial, IReadOnlyList<Move> optimalPath, BfsSolver solver, int sampleCount = 15, int nodeBudget = 2000)
        {
            if (initial == null || optimalPath == null || optimalPath.Count == 0)
                return 0f;

            // Get the optimal first move(s)
            var optimalFirstMoves = new HashSet<string>();
            if (optimalPath.Count > 0)
            {
                optimalFirstMoves.Add($"{optimalPath[0].Source}-{optimalPath[0].Target}");
            }

            // Get all legal moves from initial state
            var allMoves = new List<Move>();
            foreach (var move in EnumerateMoves(initial))
            {
                allMoves.Add(move);
            }

            // Find non-optimal moves
            var nonOptimalMoves = new List<Move>();
            foreach (var move in allMoves)
            {
                var key = $"{move.Source}-{move.Target}";
                if (!optimalFirstMoves.Contains(key))
                {
                    nonOptimalMoves.Add(move);
                }
            }

            if (nonOptimalMoves.Count == 0)
                return 0f;

            // Sample and test non-optimal moves
            int sampled = Math.Min(sampleCount, nonOptimalMoves.Count);
            int trapCount = 0;

            for (int i = 0; i < sampled; i++)
            {
                var move = nonOptimalMoves[i];
                var testState = CloneState(initial);
                if (!testState.TryApplyMove(move.Source, move.Target, out _))
                    continue;

                // Try to solve from this state with limited budget
                var result = solver.Solve(testState, nodeBudget, 100);

                // Count as trap if:
                // 1. Solver times out (too hard)
                // 2. Solution is longer than optimal
                // 3. Unsolvable
                bool isTrap = result.Status == SolverStatus.Timeout
                           || result.Status == SolverStatus.Unsolvable
                           || (result.OptimalMoves >= 0 && result.OptimalMoves > optimalPath.Count);

                if (isTrap)
                    trapCount++;
            }

            return (float)trapCount / sampled;
        }

        /// <summary>
        /// Computes structural metrics for the level state.
        /// </summary>
        public static StructuralMetrics ComputeStructuralMetrics(LevelState state)
        {
            if (state == null)
                return new StructuralMetrics(0, 0, 0);

            int mixedBottleCount = 0;
            var signatures = new HashSet<string>();
            var topColors = new HashSet<ColorId>();

            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsEmpty)
                    continue;

                // Check if mixed (more than one color)
                if (!bottle.IsSingleColorOrEmpty())
                {
                    mixedBottleCount++;
                }

                // Compute signature
                signatures.Add(ComputeBottleSignature(bottle));

                // Track top color
                var topColor = bottle.TopColor;
                if (topColor.HasValue)
                {
                    topColors.Add(topColor.Value);
                }
            }

            return new StructuralMetrics(mixedBottleCount, signatures.Count, topColors.Count);
        }

        private static string ComputeBottleSignature(Bottle bottle)
        {
            var sb = new StringBuilder(bottle.Slots.Count + 2);
            sb.Append(bottle.IsSink ? 'S' : 'N');
            sb.Append(bottle.Capacity);
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var color = bottle.Slots[i];
                sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
            }
            return sb.ToString();
        }

        private static int CountLegalMoves(LevelState state)
        {
            int count = 0;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var source = state.Bottles[i];
                if (source.IsEmpty) continue;
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    int amount = MoveRules.GetPourAmount(state, i, j);
                    if (amount > 0) count++;
                }
            }
            return count;
        }

        private static IEnumerable<Move> EnumerateMoves(LevelState state)
        {
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var source = state.Bottles[i];
                if (source.IsEmpty) continue;
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    int amount = MoveRules.GetPourAmount(state, i, j);
                    if (amount > 0)
                        yield return new Move(i, j, amount);
                }
            }
        }

        private static LevelState CloneState(LevelState state)
        {
            var bottles = new List<Bottle>(state.Bottles.Count);
            foreach (var bottle in state.Bottles)
            {
                bottles.Add(bottle.Clone());
            }
            return new LevelState(bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves, state.BackgroundPaletteIndex);
        }
    }

    /// <summary>
    /// Metrics computed along the optimal solution path.
    /// </summary>
    public readonly struct PathMetrics
    {
        public float ForcedMoveRatio { get; }
        public float AverageBranchingFactor { get; }
        public int DecisionDepth { get; }
        public float EmptyBottleUsageRatio { get; }
        public int PathLength { get; }

        public PathMetrics(float forcedMoveRatio, float avgBranchingFactor, int decisionDepth, float emptyBottleUsageRatio, int pathLength)
        {
            ForcedMoveRatio = forcedMoveRatio;
            AverageBranchingFactor = avgBranchingFactor;
            DecisionDepth = decisionDepth;
            EmptyBottleUsageRatio = emptyBottleUsageRatio;
            PathLength = pathLength;
        }
    }

    /// <summary>
    /// Structural complexity metrics for a level state.
    /// </summary>
    public readonly struct StructuralMetrics
    {
        public int MixedBottleCount { get; }
        public int DistinctSignatureCount { get; }
        public int TopColorVariety { get; }

        public StructuralMetrics(int mixedBottleCount, int distinctSignatureCount, int topColorVariety)
        {
            MixedBottleCount = mixedBottleCount;
            DistinctSignatureCount = distinctSignatureCount;
            TopColorVariety = topColorVariety;
        }
    }
}
