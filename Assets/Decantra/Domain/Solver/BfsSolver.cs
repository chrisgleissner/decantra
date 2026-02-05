/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;


namespace Decantra.Domain.Solver
{
    public sealed class BfsSolver
    {
        private readonly ConcurrentDictionary<StateKey, int> _optimalCache = new ConcurrentDictionary<StateKey, int>();

        public SolverResult Solve(LevelState initial)
        {
            return SolveOptimal(initial);
        }

        public SolverResult SolveOptimal(LevelState initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            var key = StateEncoder.EncodeCanonicalKey(initial);
            if (_optimalCache.TryGetValue(key, out int cached))
            {
                return new SolverResult(cached, new List<Move>());
            }

            var result = SolveInternal(initial, -1, -1, false, false);
            if (result.OptimalMoves >= 0)
            {
                _optimalCache.TryAdd(key, result.OptimalMoves);
            }
            return result;
        }

        public SolverResult SolveWithPath(LevelState initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            return SolveInternal(initial, -1, -1, false, true);
        }

        public SolverResult SolveWithPath(LevelState initial, int maxNodes, int maxMillis)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (maxNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxNodes));
            if (maxMillis <= 0) throw new ArgumentOutOfRangeException(nameof(maxMillis));

            return SolveInternal(initial, maxNodes, maxMillis, true, true);
        }

        public SolverResult Solve(LevelState initial, int maxNodes, int maxMillis)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (maxNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxNodes));
            if (maxMillis <= 0) throw new ArgumentOutOfRangeException(nameof(maxMillis));

            return SolveInternal(initial, maxNodes, maxMillis, true, false);
        }

        private SolverResult SolveInternal(LevelState initial, int maxNodes, int maxMillis, bool useLimits, bool trackPath)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));

            var visited = new HashSet<StateKey>();
            // Use PriorityQueue for A* Search. 
            // Comparator sorts by f = g + h. 
            // If f is equal, prefer higher g (depth) for DFS-like behavior (often faster) or lower h.
            var queue = new PriorityQueue<Node>(new NodeComparer());
            var stopwatch = Stopwatch.StartNew();

            var startKey = StateEncoder.EncodeCanonicalKey(initial);
            visited.Add(startKey);

            // Initial node: g=0, h=Heuristic
            queue.Enqueue(new Node(CloneState(initial), 0, CalculateHeuristic(initial), null, default));

            int processed = 0;
            // Adaptive limit: if unlimited, allow very deep search (10M nodes) to handle Level 153+
            int safetyLimit = useLimits ? maxNodes : 10_000_000;

            while (queue.Count > 0)
            {
                if (processed >= safetyLimit || (useLimits && stopwatch.ElapsedMilliseconds > maxMillis))
                {
                    // If we hit the safety limit, we return failure to avoid hanging
                    return new SolverResult(-1, new List<Move>(), SolverStatus.Timeout);
                }

                var node = queue.Dequeue();
                processed++;

                if (node.State.IsWin())
                {
                    return BuildResult(node, trackPath);
                }

                foreach (var move in EnumerateMoves(node.State))
                {
                    var next = CloneState(node.State);
                    int poured;
                    if (!next.TryApplyMove(move.Source, move.Target, out poured))
                    {
                        continue;
                    }

                    var key = StateEncoder.EncodeCanonicalKey(next);
                    if (visited.Add(key))
                    {
                        int g = node.Depth + 1;
                        int h = CalculateHeuristic(next);
                        // Optimization: if h=0 (and we think it's admissible), it's a win? 
                        // IsWin check covers it.
                        queue.Enqueue(new Node(next, g, h, trackPath ? node : null, trackPath ? new Move(move.Source, move.Target, poured) : default));
                    }
                }
            }

            return new SolverResult(-1, new List<Move>(), SolverStatus.Unsolvable);
        }

        private static int CalculateHeuristic(LevelState state)
        {
            // Heuristic: Sum of Color Chunks - Unique Colors
            // This estimates moves needed to merge all chunks of the same color.
            // Example: [Red], [Red] -> 2 chunks, 1 unique. Cost >= 1.
            // [Red, Blue], [Red] -> 3 chunks, 2 unique. Cost >= 1.

            int chunks = 0;
            // Use a bitmask for simple color tracking (assuming max 32 colors, ColorId is small enum)
            int uniqueColorMask = 0;

            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var bottle = state.Bottles[i];
                if (bottle.IsEmpty) continue;

                var slots = bottle.Slots;
                ColorId? lastColor = null;

                for (int s = 0; s < slots.Count; s++)
                {
                    var color = slots[s];
                    if (!color.HasValue) break;

                    if (lastColor == null || color.Value != lastColor.Value)
                    {
                        chunks++;
                        uniqueColorMask |= (1 << (int)color.Value);
                    }
                    lastColor = color;
                }
            }

            int uniqueColors = 0;
            while (uniqueColorMask != 0)
            {
                uniqueColors++;
                uniqueColorMask &= (uniqueColorMask - 1);
            }

            return chunks - uniqueColors;
        }

        private static SolverResult BuildResult(Node node, bool trackPath)
        {
            if (!trackPath)
            {
                return new SolverResult(node.Depth, new List<Move>());
            }

            var path = new List<Move>(node.Depth);
            var current = node;
            while (current != null && current.Parent != null)
            {
                path.Add(current.MoveFromParent);
                current = current.Parent;
            }
            path.Reverse();
            return new SolverResult(path.Count, path);
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
                    // Solver optimization: skip sink targets.
                    // Level generation ensures solutions never require pouring INTO sinks.
                    // (Scrambling explicitly skips sinks as targets, so forward solutions don't need them.)
                    // The game UI still allows sink pours via MoveRules for player flexibility.
                    var target = state.Bottles[j];
                    if (target.IsSink) continue;

                    int amount = MoveRules.GetPourAmount(state, i, j);
                    if (amount <= 0) continue;

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

        private sealed class Node
        {
            public Node(LevelState state, int depth, int h, Node? parent, Move moveFromParent)
            {
                State = state;
                Depth = depth;
                H = h;
                F = depth + h;
                Parent = parent;
                MoveFromParent = moveFromParent;
            }

            public LevelState State { get; }
            public int Depth { get; }
            public int H { get; }
            public int F { get; }
            public Node? Parent { get; }
            public Move MoveFromParent { get; }
        }

        private sealed class NodeComparer : IComparer<Node>
        {
            public int Compare(Node x, Node y)
            {
                // Prefer lower F
                int cmp = x.F.CompareTo(y.F);
                if (cmp != 0) return cmp;

                // Tie-breaker: prefer higher depth (DFS-like) to explore deeper solutions first when costs are equal.
                return y.Depth.CompareTo(x.Depth);
            }
        }
    }
}

