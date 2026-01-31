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
            var plans = CreateBottlePlans(profile, rng);
            var working = CreateSolvedSlots(plans, out var sinkFlags);
            int reverseMoves = profile.ReverseMoves;

            var reverseTimer = Stopwatch.StartNew();

            int applied = 0;
            int guard = 0;
            int minEmptyCount = 0;
            int maxEmptyCount = Math.Max(0, profile.EmptyBottleCount);
            var movesBuffer = new List<Move>(working.Count * working.Count);
            var appliedMoves = new List<Move>(reverseMoves + 8);
            while (applied < reverseMoves && guard < reverseMoves * 30)
            {
                guard++;
                var moves = EnumerateValidReverseMoves(working, movesBuffer);
                if (moves.Count == 0) break;
                var move = moves[rng.Next(moves.Count)];
                int appliedAmount;
                if (TryApplyReverseMove(working, sinkFlags, move.Source, move.Target, rng, minEmptyCount, maxEmptyCount, out appliedAmount))
                {
                    appliedMoves.Add(new Move(move.Source, move.Target, appliedAmount));
                    applied++;
                }
            }

            if (!BreakSolvedBottles(working, sinkFlags, rng, minEmptyCount, maxEmptyCount, reverseMoves * 10, appliedMoves))
            {
                TryUncapCappedBottles(working, sinkFlags, rng, reverseMoves * 8, appliedMoves);
                if (HasCappedBottle(working))
                {
                    throw new InvalidOperationException("Generated level contains capped bottles at start");
                }
            }

            EnsureAtLeastOneEmpty(working, appliedMoves, sinkFlags);
            if (profile.EmptyBottleCount > 0 && CountEmpty(working) == 0)
            {
                throw new InvalidOperationException("Generated level lacks empty bottles at start");
            }
            if (HasCappedBottle(working))
            {
                if (!BreakSolvedBottles(working, sinkFlags, rng, minEmptyCount, maxEmptyCount, reverseMoves * 6, appliedMoves))
                {
                    TryUncapCappedBottles(working, sinkFlags, rng, reverseMoves * 6, appliedMoves);
                    if (HasCappedBottle(working))
                    {
                        throw new InvalidOperationException("Generated level contains capped bottles at start");
                    }
                }
            }

            if (CountEmpty(working) > maxEmptyCount || CountEmpty(working) < minEmptyCount)
            {
                throw new InvalidOperationException("Generated level contains too many empty bottles at start");
            }

            if (HasFullSinkBottle(working, sinkFlags))
            {
                throw new InvalidOperationException("Generated level contains full sink bottles at start");
            }
            reverseTimer.Stop();

            var bottles = new List<Bottle>(working.Count);
            for (int i = 0; i < working.Count; i++)
            {
                bottles.Add(new Bottle(working[i], sinkFlags[i]));
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

        private static List<ColorId?[]> CreateSolvedSlots(IReadOnlyList<BottlePlan> plans, out bool[] sinkFlags)
        {
            if (plans == null) throw new ArgumentNullException(nameof(plans));
            if (plans.Count == 0) throw new ArgumentOutOfRangeException(nameof(plans));

            sinkFlags = new bool[plans.Count];
            var bottles = new List<ColorId?[]>(plans.Count);
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
                bottles.Add(slots);
                sinkFlags[i] = plan.IsSink;
            }
            return bottles;
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

            var emptyCaps = BuildEmptyCapacities(profile.LevelIndex, profile.EmptyBottleCount);
            int sinkCount = ResolveSinkCount(profile.LevelIndex, profile.EmptyBottleCount);
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

        private static List<int> BuildColorCapacities(int levelIndex, int colorCount, Random rng)
        {
            int largeCount = ResolveLargeColorCount(levelIndex, colorCount);
            var capacities = new List<int>(colorCount);
            for (int i = 0; i < colorCount - largeCount; i++)
            {
                capacities.Add(4);
            }
            for (int i = 0; i < largeCount; i++)
            {
                capacities.Add(5);
            }
            Shuffle(capacities, rng);
            return capacities;
        }

        private static List<int> BuildEmptyCapacities(int levelIndex, int emptyCount)
        {
            var capacities = new List<int>(emptyCount);
            int smallCount = ResolveSmallEmptyCount(levelIndex, emptyCount);
            for (int i = 0; i < emptyCount; i++)
            {
                capacities.Add(i < smallCount ? 3 : 4);
            }
            return capacities;
        }

        private static int ResolveLargeColorCount(int levelIndex, int colorCount)
        {
            if (levelIndex < 16) return 0;
            int target = 1 + (levelIndex - 16) / 10;
            int maxLarge = Math.Max(1, colorCount / 4);
            return Math.Min(target, Math.Max(0, Math.Min(maxLarge, colorCount)));
        }

        private static int ResolveSmallEmptyCount(int levelIndex, int emptyCount)
        {
            if (emptyCount <= 0) return 0;
            if (levelIndex < 6) return 0;
            if (levelIndex < 12) return Math.Min(1, emptyCount);
            return emptyCount;
        }

        private static int ResolveSinkCount(int levelIndex, int emptyCount)
        {
            if (emptyCount <= 0) return 0;
            return levelIndex >= 18 ? 1 : 0;
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
                    var targetTop = TopColor(target);
                    if (targetTop.HasValue && targetTop.Value != sourceTop.Value) continue;
                    int amount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
                    if (amount <= 0) continue;
                    moves.Add(new Move(i, j, amount));
                }
            }
            return moves;
        }

        private static bool TryApplyReverseMove(List<ColorId?[]> bottles, bool[] sinkFlags, int sourceIndex, int targetIndex, Random rng, int minEmptyCount, int maxEmptyCount, out int appliedAmount)
        {
            appliedAmount = 0;
            if (sourceIndex == targetIndex) return false;
            if (sourceIndex < 0 || sourceIndex >= bottles.Count) return false;
            if (targetIndex < 0 || targetIndex >= bottles.Count) return false;
            if (sinkFlags == null || sinkFlags.Length != bottles.Count) throw new ArgumentException("sinkFlags must align with bottles.", nameof(sinkFlags));

            var source = bottles[sourceIndex];
            var target = bottles[targetIndex];

            int maxAmount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
            if (sinkFlags[targetIndex])
            {
                maxAmount = Math.Min(maxAmount, FreeSpace(target) - 1);
            }
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

            if (maxEmptyCount >= 0 || minEmptyCount > 0)
            {
                int emptyCount = 0;
                for (int i = 0; i < bottles.Count; i++)
                {
                    if (IsEmpty(bottles[i])) emptyCount++;
                }
                if (emptyCount > maxEmptyCount || emptyCount < minEmptyCount)
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

        private static bool BreakSolvedBottles(List<ColorId?[]> bottles, bool[] sinkFlags, Random rng, int minEmptyCount, int maxEmptyCount, int maxAttempts, List<Move> appliedMoves)
        {
            if (bottles == null || bottles.Count == 0) return false;
            if (sinkFlags == null || sinkFlags.Length != bottles.Count) return false;
            if (!HasCappedBottle(bottles)) return true;

            int attempts = 0;
            var candidates = new List<Move>(bottles.Count * bottles.Count);
            while (HasCappedBottle(bottles) && attempts < maxAttempts)
            {
                attempts++;
                candidates.Clear();
                for (int i = 0; i < bottles.Count; i++)
                {
                    if (!IsSolvedSlots(bottles[i])) continue;
                    var source = bottles[i];
                    if (IsEmpty(source)) continue;
                    for (int j = 0; j < bottles.Count; j++)
                    {
                        if (i == j) continue;
                        var target = bottles[j];
                        var sourceTop = TopColor(source);
                        var targetTop = TopColor(target);
                        if (targetTop.HasValue && sourceTop.HasValue && targetTop.Value != sourceTop.Value) continue;
                        int amount = Math.Min(ContiguousTopCount(source), FreeSpace(target));
                        if (sinkFlags[j])
                        {
                            amount = Math.Min(amount, FreeSpace(target) - 1);
                        }
                        if (amount <= 0) continue;
                        candidates.Add(new Move(i, j, amount));
                    }
                }

                if (candidates.Count == 0) break;
                var move = candidates[rng.Next(candidates.Count)];
                int appliedAmount;
                if (TryApplyReverseMove(bottles, sinkFlags, move.Source, move.Target, rng, minEmptyCount, maxEmptyCount, out appliedAmount))
                {
                    appliedMoves?.Add(new Move(move.Source, move.Target, appliedAmount));
                    continue;
                }
            }

            return !HasCappedBottle(bottles);
        }

        private static bool HasFullSinkBottle(List<ColorId?[]> bottles, bool[] sinkFlags)
        {
            if (sinkFlags == null || sinkFlags.Length != bottles.Count) return false;
            for (int i = 0; i < bottles.Count; i++)
            {
                if (!sinkFlags[i]) continue;
                if (FreeSpace(bottles[i]) == 0) return true;
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

        private static void TryUncapCappedBottles(List<ColorId?[]> bottles, bool[] sinkFlags, Random rng, int maxAttempts, List<Move> appliedMoves)
        {
            if (bottles == null || bottles.Count == 0) return;
            if (sinkFlags == null || sinkFlags.Length != bottles.Count) return;
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
                int targetIndex = FindUncapTarget(bottles, sinkFlags, sourceIndex, rng);
                if (targetIndex < 0) return;

                int amount = 1;
                if (sinkFlags[targetIndex] && FreeSpace(bottles[targetIndex]) == 1)
                {
                    continue;
                }
                if (!WouldCreateSolvedAfterInsert(bottles[targetIndex], TopColor(bottles[sourceIndex]), amount))
                {
                    ApplyReverseMove(bottles, sourceIndex, targetIndex, amount);
                    appliedMoves?.Add(new Move(sourceIndex, targetIndex, amount));
                }
            }
        }

        private static int FindUncapTarget(List<ColorId?[]> bottles, bool[] sinkFlags, int sourceIndex, Random rng)
        {
            var source = bottles[sourceIndex];
            var sourceColor = TopColor(source);
            var preferred = new List<int>(bottles.Count);
            var fallback = new List<int>(bottles.Count);

            for (int i = 0; i < bottles.Count; i++)
            {
                if (i == sourceIndex) continue;
                if (FreeSpace(bottles[i]) <= 0) continue;
                if (sinkFlags != null && sinkFlags[i] && FreeSpace(bottles[i]) == 1) continue;

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

        private static void EnsureAtLeastOneEmpty(List<ColorId?[]> bottles, List<Move> appliedMoves, bool[] sinkFlags)
        {
            if (CountEmpty(bottles) > 0) return;
            if (sinkFlags == null || sinkFlags.Length != bottles.Count) return;

            int guard = 0;
            int maxAttempts = appliedMoves.Count * 2 + 8;
            while (appliedMoves.Count > 0 && CountEmpty(bottles) == 0 && guard < maxAttempts)
            {
                guard++;
                var move = appliedMoves[appliedMoves.Count - 1];
                appliedMoves.RemoveAt(appliedMoves.Count - 1);
                if (sinkFlags[move.Source] && FreeSpace(bottles[move.Source]) == move.Amount)
                {
                    continue;
                }
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
