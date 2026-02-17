# Sink Bottle Research Report

**Levels 1–1000 | Decantra**  
**Date:** 2026-02-17

---

## Executive Summary

Sink bottles in Decantra are bottles marked with a black line underneath that can only be poured *into*, never poured *from*. This research analyzes all 1000 levels to determine how sinks are assigned, whether they must be used in solutions, and their empirical distribution.

**Key findings:**

1. **Levels 1–17 have zero sink bottles.** Levels 18+ have exactly one sink bottle each.
2. **No sink bottle is ever required by any solution.** The solver architecturally excludes sinks as pour targets. All 983 sink bottles across levels 18–1000 are classifed `can_be_avoided=true`, `must_be_used=false`.
3. **Sinks are decorative constraints for the player.** They serve as strategic traps — the game UI allows pouring into sinks, but doing so wastes moves since sinks cannot be poured from.
4. When a sink-aware solver (that *allows* pouring into sinks) is used, **27 out of 983 levels** (2.7%) produce solutions that happen to use the sink. In all cases, the standard solver also finds a solution *without* using the sink.
5. **"Some must be used and some must not be used" is empirically FALSE.** All sinks are always avoidable.

---

## Code Path Analysis

### Where Sink Bottles Are Represented

Sink status is stored as a `bool _isSink` field on the `Bottle` class:

- **File:** `Assets/Decantra/Domain/Model/Bottle.cs`
- **Property:** `public bool IsSink => _isSink;`
- **Sealed state:** `public bool IsSealed => _isSink && IsFull;` (a full sink is sealed — cannot accept more pours)

### How Sink Bottles Are Assigned During Level Creation

Sink count is determined deterministically by level index in `LevelDifficultyEngine.ResolveSinkCount()`:

```csharp
// Assets/Decantra/Domain/Rules/LevelDifficultyEngine.cs:139-148
public static int ResolveSinkCount(int levelIndex)
{
    int eff = GetEffectiveLevel(levelIndex);  // clamped to max 100
    if (eff < 18) return 0;   // No sinks before level 18
    return 1;                  // Levels 18+: exactly 1 sink
}
```

Sink assignment happens in `LevelGenerator.CreateBottlePlans()`:

```csharp
// Assets/Decantra/Domain/Generation/LevelGenerator.cs:1010-1023
int sinkCount = LevelDifficultyEngine.ResolveSinkCount(profile.LevelIndex);
sinkCount = Math.Min(sinkCount, profile.EmptyBottleCount);
for (int i = 0; i < profile.EmptyBottleCount; i++)
{
    plans.Add(new BottlePlan
    {
        Capacity = emptyCaps[i],
        FillColor = null,
        IsSink = i < sinkCount   // First N empties are sinks
    });
}
Shuffle(plans, rng);  // Seeded RNG shuffle randomizes position
```

The process:
1. Empty bottles are allocated (levels 18+: 2 empty bottles).
2. The first `sinkCount` empties are marked as sinks (always 1 for levels 18+).
3. All bottle plans (color + empty) are shuffled using the seeded RNG.
4. The sink bottle's grid position (0–8) is thus deterministic per level/seed but varies across levels.

### Seed Logic

Seeds are computed via a deterministic chain:

```csharp
private static int NextSeed(int level, int previous)
{
    unchecked
    {
        int baseSeed = previous != 0 ? previous : 12345;
        int mix = baseSeed * 1103515245 + 12345 + level * 97;
        return Math.Abs(mix == 0 ? level * 7919 : mix);
    }
}
```

Each level's seed depends on all previous seeds, making the sequence fully deterministic.

### Why the Solver Never Uses Sinks

The BFS solver explicitly skips sink bottles as targets in move enumeration:

```csharp
// Assets/Decantra/Domain/Solver/BfsSolver.cs:207-210
// Solver optimization: skip sink targets.
// Level generation ensures solutions never require pouring INTO sinks.
var target = state.Bottles[j];
if (target.IsSink) continue;
```

Additionally, the scrambler (which creates puzzles by reverse-pouring from a solved state) also excludes sinks:

```csharp
// Assets/Decantra/Domain/Generation/LevelGenerator.cs:533-545
// CRITICAL: Sink bottles cannot be sources in forward gameplay,
// so they must not be sources during reverse scrambling either
if (source.IsSink) continue;
// ...
if (state.Bottles[j].IsSink) continue;
```

Since scrambling never moves liquid into or out of sinks, and the solver never considers pouring into sinks, **sinks are always empty at level start and never interact with solutions**.

---

## Methodology

### Level Generation

For each level L in 1..1000:
1. Compute seed via deterministic `NextSeed()` chain.
2. Get `DifficultyProfile` via `LevelDifficultyEngine.GetProfile(L)`.
3. Generate level via `LevelGenerator.Generate(seed, profile)`.
4. All generation is deterministic — same level index always produces identical state.

### Solver Configuration

| Parameter | Value |
|-----------|-------|
| Solver algorithm | A* (BFS with heuristic) |
| Max nodes | 8,000,000 |
| Max time | 8,000 ms |
| Retry with | 16,000,000 nodes / 16,000 ms |
| Heuristic | Sum of color chunks minus unique colors |

### Experiment Design

For each level:

1. **Identify sinks:** Scan `Bottle.IsSink` for all 9 bottles.
2. **Normal solver run:** Standard BFS (skips sinks as targets). Record solution and verify 0 sink pours.
3. **Sink-aware solver run:** Modified BFS (does NOT skip sinks as targets) using plain BFS without A* heuristic. This tests whether solutions *can* use sinks when allowed.
4. **Classification:**
   - `can_be_avoided`: Always true (normal solver never uses sinks by construction).
   - `must_be_used`: Always false (at least one solution — the normal one — never uses sinks).
   - `forcing_use_leads_to_unsolvable`: "false" if sink-aware solver found a solution, "unknown" if it timed out within bounds.

---

## Results

### Distribution of Sink Bottles per Level

| Sink Count | Levels | Percentage | Level Range |
|-----------|--------|------------|-------------|
| 0 | 17 | 1.7% | 1–17 |
| 1 | 983 | 98.3% | 18–1000 |
| 2+ | 0 | 0.0% | — |

### Sink Bottle Position Distribution (0-indexed)

| Position | Count | Percentage |
|----------|-------|------------|
| 0 | 123 | 12.5% |
| 1 | 108 | 11.0% |
| 2 | 112 | 11.4% |
| 3 | 108 | 11.0% |
| 4 | 108 | 11.0% |
| 5 | 111 | 11.3% |
| 6 | 108 | 11.0% |
| 7 | 113 | 11.5% |
| 8 | 92 | 9.4% |

The nearly uniform distribution confirms the seeded RNG shuffle in `CreateBottlePlans()`.

### Per-Sink-Bottle Classification Summary

| Metric | Count | Percentage |
|--------|-------|------------|
| Total sink bottles | 983 | 100% |
| `must_be_used = true` | 0 | 0.0% |
| `must_be_used = false` | 983 | 100.0% |
| `can_be_avoided = true` | 983 | 100.0% |
| `can_be_avoided = false` | 0 | 0.0% |
| `forcing_use_leads_to_unsolvable = false` | 102 | 10.4% |
| `forcing_use_leads_to_unsolvable = unknown` | 881 | 89.6% |

### Among Levels with Sinks (983 levels)

| Category | Count | Percentage |
|----------|-------|------------|
| All sinks avoidable | 983 | 100% |
| At least one must_be_used | 0 | 0% |
| Mixed (some must, some avoidable) | 0 | 0% |

### Sink-Aware Solver Results

When a modified solver that **allows** pouring into sinks is used:

| Outcome | Count | Percentage |
|---------|-------|------------|
| Found solution (sink NOT used) | 75 | 7.6% |
| Found solution (sink USED) | 27 | 2.7% |
| Timed out within bounds | 881 | 89.6% |

The 27 levels where the sink-aware solver chose to use the sink:

Levels: 18, 59, 60, 64, 65, 229, 230, 247, 252, 268, 365, 378, 381, 489, 504, 509, 595, 634, 658, 660, 711, 749, 863, 876, 880, 985, 999

In all 27 cases, the standard solver (which excludes sinks) also found a solution without using the sink.

### Solver Status

| Status | Count |
|--------|-------|
| Solved | 1000 |
| Unsolvable | 0 |
| Timeout | 0 |
| Error | 0 |

All 1000 levels are solvable.

---

## Representative Examples

### Category 1: No Sink Bottles (Levels 1–17)

| Level | Seed | Sink Count | Solution Length |
|-------|------|-----------|----------------|
| 1 | 740550945 | 0 | 6 |
| 5 | 815309 | 0 | 4 |
| 10 | 1251565814 | 0 | 8 |
| 14 | 542466952 | 0 | 10 |
| 17 | 833979121 | 0 | 9 |

### Category 2: One Sink, Avoidable, Sink-Aware Solver Completed (sink NOT used)

| Level | Seed | Sink ID | Solution Length | Sink Used in Sink-Aware? |
|-------|------|---------|----------------|-------------------------|
| 18 | 275487320 | 4 | 16 | Yes |
| 24 | 1302768481 | 2 | 13 | No |
| 26 | 2011261099 | 6 | 16 | No |
| 28 | 1403792537 | 2 | 14 | No |
| 30 | 1621853218 | 5 | 15 | No |

### Category 3: One Sink, Avoidable, Sink-Aware Solver Timed Out

| Level | Seed | Sink ID | Solution Length | Force Status |
|-------|------|---------|----------------|-------------|
| 19 | 2129116900 | 3 | 14 | unknown |
| 20 | 757987041 | 5 | 16 | unknown |
| 21 | 1355867131 | 0 | 12 | unknown |
| 22 | 495567982 | 7 | 17 | unknown |
| 23 | 1754685382 | 3 | 17 | unknown |

### Category 4: Sink-Aware Solver Used Sink in Solution

| Level | Seed | Sink ID | Normal Solution | Sink-Aware Used Sink? |
|-------|------|---------|----------------|----------------------|
| 18 | 275487320 | 4 | 16 moves | Yes |
| 59 | 1780361427 | 3 | 16 moves | Yes |
| 60 | 1637268916 | 0 | 15 moves | Yes |
| 64 | 1032789181 | 0 | 15 moves | Yes |
| 65 | 1879866764 | 8 | 16 moves | Yes |

### Category 5: High-Level Solvable with Avoidable Sink

| Level | Seed | Sink ID | Solution Length |
|-------|------|---------|----------------|
| 996 | 1698649597 | 0 | 15 |
| 997 | 1397975194 | 3 | 17 |
| 998 | 1649574891 | 6 | 16 |
| 999 | 1032006580 | 3 | 16 |
| 1000 | 375461077 | 8 | 14 |

---

## Limitations and Confidence Bounds

1. **Solver completeness for sink-aware variant:** The sink-aware BFS solver (plain BFS, no A* heuristic) timed out in 881/983 levels (89.6%) within the 8M node / 8s bound. This means `forcing_use_leads_to_unsolvable` is classified as "unknown" for these levels. The timeout does NOT mean forcing sink use leads to unsolvability — it means the solver's search space was too large to explore fully within bounds. The sink-aware solver has a strictly larger state space than the normal solver (more legal moves), making BFS exploration slower.

2. **Structural guarantee:** The classification `must_be_used=false` and `can_be_avoided=true` for all sinks is a **proven structural property** of the solver, not a bounded empirical claim. The solver code (`BfsSolver.EnumerateMoves()`) contains an explicit `if (target.IsSink) continue;` that prevents any solution from pouring into sinks. This is a code-level guarantee.

3. **Scramble guarantee:** Scrambling (`EnumerateScrambleMovePairs()`) also skips sinks as both source and target. This means sinks are always empty at level start — they never contain any liquid to pour from, making them even more irrelevant to solutions.

4. **Game UI vs. solver:** The game UI (`MoveRules`, `InteractionRules`) *does* allow players to pour into sinks. This is intentional — sinks serve as traps that waste player moves.

---

## Exact Reproduction Steps

```bash
# 1. Build the reproduction project
cd /path/to/decantra
dotnet build Reproduction/Reproduction.csproj

# 2. Run sink analysis for levels 1-1000
dotnet run --project Reproduction/Reproduction.csproj -- sinkanalysis 1000

# 3. Outputs:
#    - doc/sink-bottles-levels-1-1000.csv     (per-level CSV)
#    - solver-solutions-debug-sinks.txt       (structured debug output)
#    - Console output with distribution statistics
```

The analysis is fully deterministic. Running the same command produces identical output.

---

## Conclusions

1. **How sink bottles are assigned:** Deterministically based on level index and seed. `LevelDifficultyEngine.ResolveSinkCount()` returns 0 for levels 1-17 and 1 for levels 18+. The sink is always one of the empty bottles, with its grid position randomized by the seeded RNG shuffle.

2. **"Some must be used and some must not be used"** is **empirically and structurally FALSE**. All 983 sink bottles across levels 18-1000 are always avoidable. No solution ever requires pouring into a sink. This is guaranteed by the solver architecture (`BfsSolver.EnumerateMoves()` skipping sink targets).

3. **Exact distribution:**
   - 1.7% of levels (1-17) have no sinks.
   - 98.3% of levels (18-1000) have exactly 1 sink.
   - 0% of levels have multiple sinks.
   - 100% of sinks are avoidable.
   - 0% of sinks must be used.

4. **Sink purpose:** Sinks are strategic traps for human players. The game allows pouring into them, wasting moves, but the optimal solution never requires it. When a sink-aware solver is given the option, it occasionally chooses to use sinks (27/983 = 2.7% of levels) but this is never required.
