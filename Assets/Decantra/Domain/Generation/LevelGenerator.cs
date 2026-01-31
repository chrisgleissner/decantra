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
            var solved = CreateSolvedBottles(plans);
            int scrambleMovesTarget = profile.ReverseMoves;

            var scrambleTimer = Stopwatch.StartNew();
            LevelState scrambled = null;
            string lastFailure = null;
            int optimal = -1;
            int movesAllowed = 0;
            int scrambleMoves = 0;
            long solveMs = 0;
            const int minOptimalMoves = 2;
            const int maxAttempts = 20;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var attemptRng = attempt == 0 ? rng : new Random(seed + attempt * 7919);
                int attemptScrambleTarget = Math.Max(4, scrambleMovesTarget - attempt / 2);
                int minEmptyDuringScramble = 0;
                int maxEmptyDuringScramble = Math.Max(profile.EmptyBottleCount + 2, profile.EmptyBottleCount);
                var attemptState = new LevelState(CloneBottles(solved), 0, 0, 0, profile.LevelIndex, seed);
                int appliedMoves = ScrambleState(attemptState, attemptRng, attemptScrambleTarget, minEmptyDuringScramble, maxEmptyDuringScramble);
                if (appliedMoves <= 0)
                {
                    lastFailure = "scramble";
                    ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                    continue;
                }

                if (profile.LevelIndex <= 6 && HasSolvedBottle(attemptState.Bottles))
                {
                    if (!BreakSolvedBottles(attemptState, attemptRng, minEmptyDuringScramble, maxEmptyDuringScramble, Math.Max(1, appliedMoves) * 6))
                    {
                        lastFailure = "break_solved";
                        ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                        continue;
                    }
                }

                if (CountEmpty(attemptState.Bottles) > profile.EmptyBottleCount)
                {
                    if (!ReduceEmptyCount(attemptState, attemptRng, profile.EmptyBottleCount, Math.Max(1, appliedMoves) * 8))
                    {
                        lastFailure = "reduce_empty";
                        ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                        continue;
                    }
                }

                if (!IsAcceptableStart(attemptState, profile))
                {
                    lastFailure = "accept";
                    ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                    continue;
                }

                if (!LevelIntegrity.TryValidate(attemptState, out string integrityError))
                {
                    lastFailure = $"integrity:{integrityError}";
                    ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                    continue;
                }

                var solveTimer = Stopwatch.StartNew();
                var solveResult = _solver.SolveOptimal(attemptState);
                solveTimer.Stop();
                if (solveResult.OptimalMoves < 0)
                {
                    lastFailure = "solver";
                    ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                    continue;
                }
                if (solveResult.OptimalMoves < minOptimalMoves)
                {
                    lastFailure = "min_optimal";
                    ReportReject(profile.LevelIndex, seed, attempt, lastFailure);
                    continue;
                }

                optimal = solveResult.OptimalMoves;
                movesAllowed = Math.Max(2, MoveAllowanceCalculator.ComputeMovesAllowed(profile, optimal));
                scrambleMoves = appliedMoves;
                solveMs = solveTimer.ElapsedMilliseconds;
                scrambled = new LevelState(attemptState.Bottles, 0, movesAllowed, optimal, profile.LevelIndex, seed, scrambleMoves);
                break;
            }
            scrambleTimer.Stop();

            if (scrambled == null)
            {
                throw new InvalidOperationException($"Failed to scramble a valid level state ({lastFailure ?? "unknown"})");
            }
            overallTimer.Stop();
            Log?.Invoke($"LevelGenerator.Generate seed={seed} level={profile.LevelIndex} scrambleMoves={scrambled.ScrambleMoves} movesAllowed={movesAllowed} scrambleMs={scrambleTimer.ElapsedMilliseconds} solveMs={solveMs} totalMs={overallTimer.ElapsedMilliseconds}");
            return scrambled;
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        private void ReportReject(int levelIndex, int seed, int attempt, string reason)
        {
            Log?.Invoke($"LevelGenerator.Reject level={levelIndex} seed={seed} attempt={attempt} reason={reason}");
        }

        private static int ScrambleState(LevelState state, Random rng, int moves, int minEmptyCount, int maxEmptyCount)
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
                var candidates = EnumerateScrambleMovePairs(state, buffer, lastMove);
                if (candidates.Count == 0)
                {
                    break;
                }

                var move = candidates[rng.Next(candidates.Count)];
                int poured;
                if (!TryApplyScrambleMove(state, move.Source, move.Target, rng, minEmptyCount, maxEmptyCount, out poured))
                {
                    continue;
                }

                lastMove = new Move(move.Source, move.Target, poured);
                applied++;
            }

            return applied;
        }

        private static List<Move> EnumerateScrambleMovePairs(LevelState state, List<Move> buffer, Move? lastMove)
        {
            buffer.Clear();
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                var source = state.Bottles[i];
                if (source.IsEmpty) continue;
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    if (lastMove.HasValue && lastMove.Value.Source == j && lastMove.Value.Target == i)
                    {
                        continue;
                    }

                    if (state.Bottles[j].IsSink) continue;

                    var target = state.Bottles[j];
                    int maxAmount = GetMaxReverseAmount(source, target);
                    if (maxAmount <= 0) continue;
                    buffer.Add(new Move(i, j, maxAmount));
                }
            }
            return buffer;
        }

        private static bool TryApplyScrambleMove(LevelState state, int sourceIndex, int targetIndex, Random rng, int minEmptyCount, int maxEmptyCount, out int appliedAmount)
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
            int maxAmount = GetMaxReverseAmount(source, target);
            if (maxAmount <= 0) return false;
            int amount = rng.Next(1, maxAmount + 1);
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

            if (profile.LevelIndex <= 6 && HasSolvedBottle(state.Bottles)) return false;

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

                        int maxAmount = GetMaxReverseAmount(source, target);
                        maxAmount = Math.Min(maxAmount, source.Count - 1);
                        if (maxAmount <= 0) continue;

                        candidates.Add(new Move(i, j, maxAmount));
                    }
                }

                if (candidates.Count == 0)
                {
                    return false;
                }

                var pick = candidates[rng.Next(candidates.Count)];
                int amount = rng.Next(1, pick.Amount + 1);
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
                    for (int j = 0; j < state.Bottles.Count; j++)
                    {
                        if (i == j) continue;
                        if (state.Bottles[j].IsSink) continue;
                        var target = state.Bottles[j];
                        int maxAmount = GetMaxReverseAmount(source, target);
                        if (maxAmount <= 0) continue;
                        candidates.Add(new Move(i, j, maxAmount));
                    }
                }

                if (candidates.Count == 0) break;
                var move = candidates[rng.Next(candidates.Count)];
                var moveSource = state.Bottles[move.Source];
                var moveTarget = state.Bottles[move.Target];
                int moveMaxAmount = GetMaxReverseAmount(moveSource, moveTarget);
                if (moveMaxAmount <= 0) continue;

                int amount = rng.Next(1, moveMaxAmount + 1);
                if (moveTarget.IsEmpty && moveSource.Count == moveMaxAmount && amount == moveMaxAmount)
                {
                    amount = Math.Max(1, moveMaxAmount - 1);
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
