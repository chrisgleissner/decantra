using System;
using System.Collections.Generic;
using System.Diagnostics;
using Decantra.Domain.Model;

namespace Decantra.Domain.Solver
{
    public sealed class BfsSolver
    {
        public SolverResult Solve(LevelState initial)
        {
            return Solve(initial, 300_000, 800);
        }

        public SolverResult Solve(LevelState initial, int maxNodes, int maxMillis)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (maxNodes <= 0) throw new ArgumentOutOfRangeException(nameof(maxNodes));
            if (maxMillis <= 0) throw new ArgumentOutOfRangeException(nameof(maxMillis));

            var visited = new HashSet<string>();
            var queue = new Queue<Node>();
            var stopwatch = Stopwatch.StartNew();

            var startKey = StateEncoder.Encode(initial);
            visited.Add(startKey);
            queue.Enqueue(new Node(CloneState(initial), 0));

            int processed = 0;
            while (queue.Count > 0)
            {
                if (processed >= maxNodes || stopwatch.ElapsedMilliseconds > maxMillis)
                {
                    return new SolverResult(-1, new List<Move>());
                }

                var node = queue.Dequeue();
                processed++;
                if (node.State.IsWin())
                {
                    return new SolverResult(node.Depth, new List<Move>());
                }

                foreach (var move in EnumerateMoves(node.State))
                {
                    var next = CloneState(node.State);
                    int poured;
                    if (!next.TryApplyMove(move.Source, move.Target, out poured))
                    {
                        continue;
                    }

                    var key = StateEncoder.Encode(next);
                    if (visited.Add(key))
                    {
                        queue.Enqueue(new Node(next, node.Depth + 1));
                    }
                }
            }

            return new SolverResult(-1, new List<Move>());
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

                    if (target.IsEmpty && source.IsSolvedBottle())
                    {
                        continue;
                    }

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
            return new LevelState(bottles, 0, state.MovesAllowed, state.OptimalMoves, state.LevelIndex, state.Seed);
        }

        private sealed class Node
        {
            public Node(LevelState state, int depth)
            {
                State = state;
                Depth = depth;
            }

            public LevelState State { get; }
            public int Depth { get; }
        }
    }
}
