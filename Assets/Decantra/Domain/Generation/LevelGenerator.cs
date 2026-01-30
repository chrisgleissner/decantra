using System;
using System.Collections.Generic;
using System.Diagnostics;
using Decantra.Domain.Model;
using Decantra.Domain.Solver;

namespace Decantra.Domain.Generation
{
    public sealed class LevelGenerator
    {
        private readonly BfsSolver _solver;

        public Action<string> Log { get; set; }

        public LevelGenerator(BfsSolver solver)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

        public LevelState Generate(int seed, int levelIndex, int reverseMoves, int movesAllowedPadding)
        {
            if (reverseMoves <= 0) throw new ArgumentOutOfRangeException(nameof(reverseMoves));
            if (movesAllowedPadding < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowedPadding));

            var overallTimer = Stopwatch.StartNew();
            var rng = new Random(seed);
            var working = CreateSolvedSlots();

            var reverseTimer = Stopwatch.StartNew();

            int applied = 0;
            int guard = 0;
            var movesBuffer = new List<Move>(working.Count * working.Count);
            while (applied < reverseMoves && guard < reverseMoves * 20)
            {
                guard++;
                var moves = EnumerateValidReverseMoves(working, movesBuffer);
                if (moves.Count == 0) break;
                var move = moves[rng.Next(moves.Count)];
                if (TryApplyReverseMove(working, move.Source, move.Target, rng))
                {
                    applied++;
                }
            }

            int extraGuard = 0;
            while (HasCappedBottle(working) && extraGuard < reverseMoves * 12)
            {
                extraGuard++;
                var moves = EnumerateValidReverseMoves(working, movesBuffer);
                if (moves.Count == 0) break;
                var move = moves[rng.Next(moves.Count)];
                TryApplyReverseMove(working, move.Source, move.Target, rng);
            }

            if (HasCappedBottle(working))
            {
                if (levelIndex <= 3)
                {
                    int uncapGuard = 0;
                    while (HasCappedBottle(working) && uncapGuard < reverseMoves * 8)
                    {
                        uncapGuard++;
                        var moves = EnumerateValidReverseMoves(working, movesBuffer);
                        if (moves.Count == 0) break;
                        var move = moves[rng.Next(moves.Count)];
                        TryApplyReverseMove(working, move.Source, move.Target, rng);
                    }
                }
                if (HasCappedBottle(working))
                {
                    throw new InvalidOperationException("Generated level contains capped bottles at start");
                }
            }
            reverseTimer.Stop();

            var bottles = new List<Bottle>(working.Count);
            for (int i = 0; i < working.Count; i++)
            {
                bottles.Add(new Bottle(working[i]));
            }

            int emptyCount = 0;
            var distinctColors = new HashSet<ColorId>();
            for (int i = 0; i < bottles.Count; i++)
            {
                var bottle = bottles[i];
                if (bottle.IsEmpty)
                {
                    emptyCount++;
                    continue;
                }
                if (bottle.IsSolvedBottle())
                {
                    if (levelIndex > 3)
                    {
                        throw new InvalidOperationException("Generated level contains capped bottles at start");
                    }
                }
                for (int s = 0; s < bottle.Slots.Count; s++)
                {
                    var color = bottle.Slots[s];
                    if (color.HasValue)
                    {
                        distinctColors.Add(color.Value);
                    }
                }
            }

            if (levelIndex > 3)
            {
                if (emptyCount < 2)
                {
                    throw new InvalidOperationException("Generated level has insufficient empty bottles");
                }

                if (distinctColors.Count < 2)
                {
                    throw new InvalidOperationException("Generated level lacks color variety");
                }
            }

            var state = new LevelState(bottles, 0, 0, 0, levelIndex, seed);
            var solveTimer = Stopwatch.StartNew();
            int optimal;
            if (levelIndex <= 3)
            {
                optimal = Math.Max(1, reverseMoves);
                solveTimer.Stop();
                Log?.Invoke($"LevelGenerator.Generate skip solve seed={seed} level={levelIndex} reverseMoves={reverseMoves}");
            }
            else
            {
                int maxNodes = 30_000 + levelIndex * 500;
                int maxMillis = 120 + levelIndex * 3;
                if (maxNodes > 60_000) maxNodes = 60_000;
                if (maxMillis > 220) maxMillis = 220;
                var solveResult = _solver.Solve(state, maxNodes, maxMillis);
                solveTimer.Stop();
                optimal = solveResult.OptimalMoves;
                if (optimal < 0)
                {
                    optimal = Math.Max(1, reverseMoves - 1);
                    Log?.Invoke($"LevelGenerator.Generate fallback optimal seed={seed} level={levelIndex} reverseMoves={reverseMoves} maxNodes={maxNodes} maxMillis={maxMillis}");
                }
            }

            int movesAllowed = Math.Max(1, optimal + movesAllowedPadding);
            overallTimer.Stop();
            Log?.Invoke($"LevelGenerator.Generate seed={seed} level={levelIndex} reverseMoves={reverseMoves} movesAllowedPadding={movesAllowedPadding} reverseMs={reverseTimer.ElapsedMilliseconds} solveMs={solveTimer.ElapsedMilliseconds} totalMs={overallTimer.ElapsedMilliseconds}");
            return new LevelState(state.Bottles, 0, movesAllowed, optimal, levelIndex, seed);
        }

        private static List<ColorId?[]> CreateSolvedSlots()
        {
            var bottles = new List<ColorId?[]>(9);
            for (int i = 0; i < 6; i++)
            {
                bottles.Add(new ColorId?[]
                {
                    (ColorId)i, (ColorId)i, (ColorId)i, (ColorId)i
                });
            }
            for (int i = 0; i < 3; i++)
            {
                bottles.Add(new ColorId?[4]);
            }
            return bottles;
        }

        private static List<Move> EnumerateValidReverseMoves(List<ColorId?[]> bottles, List<Move> moves)
        {
            moves.Clear();
            for (int i = 0; i < bottles.Count; i++)
            {
                var source = bottles[i];
                if (IsEmpty(source)) continue;
                for (int j = 0; j < bottles.Count; j++)
                {
                    if (i == j) continue;
                    var target = bottles[j];
                    int amount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
                    if (amount <= 0) continue;
                    moves.Add(new Move(i, j, amount));
                }
            }
            return moves;
        }

        private static bool TryApplyReverseMove(List<ColorId?[]> bottles, int sourceIndex, int targetIndex, Random rng)
        {
            if (sourceIndex == targetIndex) return false;
            if (sourceIndex < 0 || sourceIndex >= bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= bottles.Count) return false;

            var source = bottles[sourceIndex];
            var target = bottles[targetIndex];

            int maxAmount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
            if (maxAmount <= 0) return false;

            int amount = rng.Next(1, maxAmount + 1);
            var color = TopColor(source);
            if (!color.HasValue) return false;

            int removed = 0;
            for (int i = source.Length - 1; i >= 0 && removed < amount; i--)
            {
                if (source[i] == color)
                {
                    source[i] = null;
                    removed++;
                }
                else if (source[i].HasValue)
                {
                    break;
                }
            }

            int inserted = 0;
            for (int i = 0; i < target.Length && inserted < amount; i++)
            {
                if (!target[i].HasValue)
                {
                    target[i] = color;
                    inserted++;
                }
            }

            return inserted > 0;
        }

        private static bool IsEmpty(ColorId?[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].HasValue) return false;
            }
            return true;
        }

        private static int FreeSpace(ColorId?[] slots)
        {
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].HasValue) count++;
            }
            return count;
        }

        private static ColorId? TopColor(ColorId?[] slots)
        {
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].HasValue) return slots[i];
            }
            return null;
        }

        private static int ContiguousTopCount(ColorId?[] slots)
        {
            var top = TopColor(slots);
            if (!top.HasValue) return 0;
            int count = 0;
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i] == top)
                {
                    count++;
                }
                else if (slots[i].HasValue)
                {
                    break;
                }
            }
            return count;
        }

        private static bool HasCappedBottle(List<ColorId?[]> bottles)
        {
            for (int i = 0; i < bottles.Count; i++)
            {
                if (IsSolvedSlots(bottles[i])) return true;
            }
            return false;
        }

        private static bool IsSolvedSlots(ColorId?[] slots)
        {
            if (slots == null || slots.Length == 0) return false;
            var color = slots[0];
            if (!color.HasValue) return false;
            for (int i = 1; i < slots.Length; i++)
            {
                if (!slots[i].HasValue || slots[i] != color) return false;
            }
            return true;
        }
    }
}
