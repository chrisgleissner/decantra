using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using Decantra.Domain.Model;

namespace Decantra.Domain.Solver
{
    public sealed class BfsSolver
    {
        private readonly ConcurrentDictionary<string, int> _optimalCache = new ConcurrentDictionary<string, int>();

        public SolverResult Solve(LevelState initial)
        {
            return SolveOptimal(initial);
        }

        public SolverResult SolveOptimal(LevelState initial)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            var key = StateEncoder.EncodeCanonical(initial);
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

            var visited = new HashSet<string>();
            var queue = new Queue<Node>();
            var stopwatch = Stopwatch.StartNew();

            var startKey = StateEncoder.EncodeCanonical(initial);
            visited.Add(startKey);
            queue.Enqueue(new Node(CloneState(initial), 0, null, default));

            int processed = 0;
            while (queue.Count > 0)
            {
                if (useLimits && (processed >= maxNodes || stopwatch.ElapsedMilliseconds > maxMillis))
                {
                    return new SolverResult(-1, new List<Move>());
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

                    var key = StateEncoder.EncodeCanonical(next);
                    if (visited.Add(key))
                    {
                        queue.Enqueue(new Node(next, node.Depth + 1, trackPath ? node : null, trackPath ? new Move(move.Source, move.Target, poured) : default));
                    }
                }
            }

            return new SolverResult(-1, new List<Move>());
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
                    var target = state.Bottles[j];
                    int amount = source.MaxPourAmountInto(target);
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
            return new LevelState(bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed, state.ScrambleMoves);
        }

        private sealed class Node
        {
            public Node(LevelState state, int depth, Node parent, Move moveFromParent)
            {
                State = state;
                Depth = depth;
                Parent = parent;
                MoveFromParent = moveFromParent;
            }

            public LevelState State { get; }
            public int Depth { get; }
            public Node Parent { get; }
            public Move MoveFromParent { get; }
        }
    }
}
