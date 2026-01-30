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

        public Action<string> Log { get; set; }

        public LevelGenerator(BfsSolver solver)
        {
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

        public LevelState Generate(int seed, DifficultyProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var overallTimer = Stopwatch.StartNew();
            var rng = new Random(seed);
            var working = CreateSolvedSlots(profile.ColorCount, profile.EmptyBottleCount);
            int reverseMoves = profile.ReverseMoves;

            var reverseTimer = Stopwatch.StartNew();

            int applied = 0;
            int guard = 0;
            int minEmptyCount = profile.EmptyBottleCount > 1 ? 2 : (profile.EmptyBottleCount > 0 ? 1 : 0);
            var movesBuffer = new List<Move>(working.Count * working.Count);
            var appliedMoves = new List<Move>(reverseMoves + 8);
            while (applied < reverseMoves && guard < reverseMoves * 20)
            {
                guard++;
                var moves = EnumerateValidReverseMoves(working, movesBuffer);
                if (moves.Count == 0) break;
                var move = moves[rng.Next(moves.Count)];
                int appliedAmount;
                if (TryApplyReverseMove(working, move.Source, move.Target, rng, minEmptyCount, out appliedAmount))
                {
                    appliedMoves.Add(new Move(move.Source, move.Target, appliedAmount));
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
                int appliedAmount;
                if (TryApplyReverseMove(working, move.Source, move.Target, rng, minEmptyCount, out appliedAmount))
                {
                    appliedMoves.Add(new Move(move.Source, move.Target, appliedAmount));
                }
            }

            if (HasCappedBottle(working))
            {
                int uncapGuard = profile.LevelIndex <= 3 ? reverseMoves * 12 : reverseMoves * 6;
                TryUncapCappedBottles(working, rng, 0, uncapGuard);
                if (HasCappedBottle(working))
                {
                    throw new InvalidOperationException("Generated level contains capped bottles at start");
                }
            }

            UndoToEmptyCount(working, appliedMoves, profile.EmptyBottleCount);
            if (HasCappedBottle(working))
            {
                int uncapGuard = profile.LevelIndex <= 3 ? reverseMoves * 12 : reverseMoves * 6;
                TryUncapCappedBottles(working, rng, 0, uncapGuard);
            }

            int postGuard = 0;
            while (postGuard < reverseMoves * 6 && (HasCappedBottle(working) || CountEmpty(working) < profile.EmptyBottleCount))
            {
                postGuard++;
                if (CountEmpty(working) < profile.EmptyBottleCount)
                {
                    EnsureEmptyBottleCount(working, rng, profile.EmptyBottleCount, reverseMoves * 4);
                }

                if (HasCappedBottle(working))
                {
                    int uncapGuard = profile.LevelIndex <= 3 ? reverseMoves * 12 : reverseMoves * 6;
                    TryUncapCappedBottles(working, rng, 0, uncapGuard);
                }
            }

            EnsureAtLeastOneEmpty(working, appliedMoves);
            if (HasCappedBottle(working))
            {
                int uncapGuard = profile.LevelIndex <= 3 ? reverseMoves * 12 : reverseMoves * 6;
                TryUncapCappedBottles(working, rng, 0, uncapGuard);
            }

            if (HasCappedBottle(working))
            {
                throw new InvalidOperationException("Generated level contains capped bottles at start");
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
                    if (profile.LevelIndex > 3)
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

            if (profile.LevelIndex > 3)
            {
                if (distinctColors.Count < Math.Min(profile.ColorCount, 2))
                {
                    throw new InvalidOperationException("Generated level lacks color variety");
                }
            }

            if (!IsStructurallyComplex(bottles, profile.LevelIndex, profile.ColorCount, profile.EmptyBottleCount))
            {
                throw new InvalidOperationException("Generated level is too trivial");
            }

            var state = new LevelState(bottles, 0, 0, 0, profile.LevelIndex, seed);
            var solveTimer = Stopwatch.StartNew();
            var solveResult = _solver.SolveOptimal(state);
            solveTimer.Stop();
            int optimal = solveResult.OptimalMoves;
            if (optimal < 0)
            {
                throw new InvalidOperationException("Solver failed to find optimal moves");
            }

            int movesAllowed = MoveAllowanceCalculator.ComputeMovesAllowed(profile, optimal);
            overallTimer.Stop();
            Log?.Invoke($"LevelGenerator.Generate seed={seed} level={profile.LevelIndex} reverseMoves={reverseMoves} movesAllowed={movesAllowed} reverseMs={reverseTimer.ElapsedMilliseconds} solveMs={solveTimer.ElapsedMilliseconds} totalMs={overallTimer.ElapsedMilliseconds}");
            return new LevelState(state.Bottles, 0, movesAllowed, optimal, profile.LevelIndex, seed);
        }

        private static bool IsStructurallyComplex(List<Bottle> bottles, int levelIndex, int colorCount, int emptyCount)
        {
            if (bottles == null || bottles.Count == 0) return false;

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
            var sb = new StringBuilder(bottle.Slots.Count + 1);
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var color = bottle.Slots[i];
                sb.Append(color.HasValue ? ((int)color.Value + 1).ToString() : "0");
            }
            return sb.ToString();
        }

        private static List<ColorId?[]> CreateSolvedSlots(int colorCount, int emptyBottleCount)
        {
            if (colorCount <= 0) throw new ArgumentOutOfRangeException(nameof(colorCount));
            if (emptyBottleCount < 0) throw new ArgumentOutOfRangeException(nameof(emptyBottleCount));

            int available = Enum.GetValues(typeof(ColorId)).Length;
            if (colorCount > available)
            {
                throw new InvalidOperationException($"Color count {colorCount} exceeds available colors {available}");
            }

            var bottles = new List<ColorId?[]>(colorCount + emptyBottleCount);
            for (int i = 0; i < colorCount; i++)
            {
                bottles.Add(new ColorId?[]
                {
                    (ColorId)i, (ColorId)i, (ColorId)i, (ColorId)i
                });
            }
            for (int i = 0; i < emptyBottleCount; i++)
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
                var sourceTop = TopColor(source);
                if (!sourceTop.HasValue) continue;
                for (int j = 0; j < bottles.Count; j++)
                {
                    if (i == j) continue;
                    var target = bottles[j];
                    if (IsSolvedSlots(source) && IsEmpty(target)) continue;
                    var targetTop = TopColor(target);
                    if (targetTop.HasValue && targetTop.Value != sourceTop.Value) continue;
                    int amount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
                    if (amount <= 0) continue;
                    moves.Add(new Move(i, j, amount));
                }
            }
            return moves;
        }

        private static bool TryApplyReverseMove(List<ColorId?[]> bottles, int sourceIndex, int targetIndex, Random rng, int minEmptyCount, out int appliedAmount)
        {
            appliedAmount = 0;
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

            var sourceSnapshot = (ColorId?[])source.Clone();
            var targetSnapshot = (ColorId?[])target.Clone();

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

            if (inserted <= 0)
            {
                Array.Copy(sourceSnapshot, source, sourceSnapshot.Length);
                Array.Copy(targetSnapshot, target, targetSnapshot.Length);
                return false;
            }

            if (minEmptyCount > 0)
            {
                int emptyCount = 0;
                for (int i = 0; i < bottles.Count; i++)
                {
                    if (IsEmpty(bottles[i])) emptyCount++;
                }
                if (emptyCount < minEmptyCount)
                {
                    Array.Copy(sourceSnapshot, source, sourceSnapshot.Length);
                    Array.Copy(targetSnapshot, target, targetSnapshot.Length);
                    return false;
                }
            }

            appliedAmount = amount;
            return true;
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

        private static void TryUncapCappedBottles(List<ColorId?[]> bottles, Random rng, int minEmptyCount, int maxAttempts)
        {
            if (bottles == null || bottles.Count == 0) return;
            int attempts = 0;
            var candidates = new List<int>(bottles.Count);

            while (HasCappedBottle(bottles) && attempts < maxAttempts)
            {
                attempts++;
                candidates.Clear();
                for (int i = 0; i < bottles.Count; i++)
                {
                    if (IsSolvedSlots(bottles[i]))
                    {
                        candidates.Add(i);
                    }
                }

                if (candidates.Count == 0) return;
                int sourceIndex = candidates[rng.Next(candidates.Count)];
                int targetIndex = FindUncapTarget(bottles, sourceIndex, rng);
                if (targetIndex < 0) return;

                int amount = 1;
                if (!WouldCreateSolvedAfterInsert(bottles[targetIndex], TopColor(bottles[sourceIndex]), amount))
                {
                    ApplyReverseMove(bottles, sourceIndex, targetIndex, amount);
                }
            }
        }

        private static int FindUncapTarget(List<ColorId?[]> bottles, int sourceIndex, Random rng)
        {
            var source = bottles[sourceIndex];
            var sourceColor = TopColor(source);
            var preferred = new List<int>(bottles.Count);
            var fallback = new List<int>(bottles.Count);

            for (int i = 0; i < bottles.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (FreeSpace(bottles[i]) <= 0) continue;

                if (!IsEmpty(bottles[i]))
                {
                    var targetTop = TopColor(bottles[i]);
                    if (targetTop.HasValue && sourceColor.HasValue && targetTop.Value != sourceColor.Value)
                    {
                        if (!WouldCreateSolvedAfterInsert(bottles[i], sourceColor, 1))
                        {
                            preferred.Add(i);
                        }
                    }
                    else
                    {
                        if (!WouldCreateSolvedAfterInsert(bottles[i], sourceColor, 1))
                        {
                            fallback.Add(i);
                        }
                    }
                }
                else
                {
                    if (!WouldCreateSolvedAfterInsert(bottles[i], sourceColor, 1))
                    {
                        fallback.Add(i);
                    }
                }
            }

            if (preferred.Count > 0) return preferred[rng.Next(preferred.Count)];
            if (fallback.Count > 0) return fallback[rng.Next(fallback.Count)];
            return -1;
        }

        private static bool WouldCreateSolvedAfterInsert(ColorId?[] slots, ColorId? color, int amount)
        {
            if (!color.HasValue) return false;
            if (amount <= 0) return false;
            int free = FreeSpace(slots);
            if (free <= 0) return false;
            if (amount < free) return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].HasValue && slots[i].Value != color.Value) return false;
            }
            return true;
        }

        private static void UndoToEmptyCount(List<ColorId?[]> bottles, List<Move> appliedMoves, int minEmptyCount)
        {
            if (minEmptyCount <= 0) return;
            while (appliedMoves.Count > 0 && CountEmpty(bottles) < minEmptyCount)
            {
                var move = appliedMoves[appliedMoves.Count - 1];
                appliedMoves.RemoveAt(appliedMoves.Count - 1);
                ApplyReverseMove(bottles, move.Target, move.Source, move.Amount);
            }
        }

        private static bool EnsureEmptyBottleCount(List<ColorId?[]> bottles, Random rng, int minEmptyCount, int maxAttempts)
        {
            if (minEmptyCount <= 0) return true;

            int attempts = 0;
            while (attempts < maxAttempts && CountEmpty(bottles) < minEmptyCount)
            {
                attempts++;
                int sourceIndex = FindMostFilledBottle(bottles);
                if (sourceIndex < 0) break;

                int targetIndex = FindTargetWithSpace(bottles, sourceIndex, rng);
                if (targetIndex < 0) break;

                int amount = Math.Min(ContiguousTopCount(bottles[sourceIndex]), FreeSpace(bottles[targetIndex]));
                if (amount <= 0) continue;

                ApplyReverseMove(bottles, sourceIndex, targetIndex, amount);
            }

            return CountEmpty(bottles) >= minEmptyCount;
        }

        private static void EnsureAtLeastOneEmpty(List<ColorId?[]> bottles, List<Move> appliedMoves)
        {
            if (CountEmpty(bottles) > 0) return;

            int guard = 0;
            int maxAttempts = appliedMoves.Count * 2 + 8;
            while (appliedMoves.Count > 0 && CountEmpty(bottles) == 0 && guard < maxAttempts)
            {
                guard++;
                var move = appliedMoves[appliedMoves.Count - 1];
                appliedMoves.RemoveAt(appliedMoves.Count - 1);
                ApplyReverseMove(bottles, move.Target, move.Source, move.Amount);
            }
        }

        private static int FindMostFilledBottle(List<ColorId?[]> bottles)
        {
            int bestIndex = -1;
            int bestCount = 0;
            for (int i = 0; i < bottles.Count; i++)
            {
                int count = bottles[i].Length - FreeSpace(bottles[i]);
                if (count > bestCount)
                {
                    bestCount = count;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static int FindTargetWithSpace(List<ColorId?[]> bottles, int sourceIndex, Random rng)
        {
            var candidates = new List<int>(bottles.Count);
            for (int i = 0; i < bottles.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (FreeSpace(bottles[i]) <= 0) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0) return -1;
            return candidates[rng.Next(candidates.Count)];
        }

        private static void ApplyReverseMove(List<ColorId?[]> bottles, int sourceIndex, int targetIndex, int amount)
        {
            if (amount <= 0) return;
            var source = bottles[sourceIndex];
            var target = bottles[targetIndex];
            var color = TopColor(source);
            if (!color.HasValue) return;

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
        }

        private static int CountEmpty(List<ColorId?[]> bottles)
        {
            int empty = 0;
            for (int i = 0; i < bottles.Count; i++)
            {
                if (IsEmpty(bottles[i])) empty++;
            }
            return empty;
        }
    }
}
