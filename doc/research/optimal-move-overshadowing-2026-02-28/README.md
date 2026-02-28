# Research: Optimal-Move Overshadowing at High Levels

**Date:** 2026-02-28  
**Status:** Research only — no code changes  
**Author:** GitHub Copilot

---

## 1. Problem Statement

In Decantra's bottle-sorting puzzles, the primary difficulty lever at high levels becomes
**optimal-move precision**: the gap between the optimal (BFS-solved) move count and the
allowed move count shrinks to near-zero, making "find the exact optimal sequence" the
*only* challenge that matters.

### Concrete example

| Metric | Level 1 | Level 250 | Level 500 |
|--------|---------|-----------|-----------|
| Slack factor | 2.0× | ≈1.50× | 1.0× |
| Optimal moves (typical) | 4 | 10 | 12 |
| Allowed moves | 8 | 15 | 12 |
| Spare moves | 4 | 5 | 0 |

At level 500 the slack factor reaches **1.0×** (`MoveAllowanceCalculator.ComputeSlackFactor`),
meaning `movesAllowed == optimalMoves`. Other difficulty features (7 colours, 9 bottles,
variable capacities 2–8, up to 5 sinks, high trap scores, low forced-move ratios) are
already maxed out by level 100 (`LevelDifficultyEngine.MaxEffectiveLevel = 100`) and the
intrinsic difficulty curve plateaus at 92 by level 200
(`MonotonicLevelSelector.MaxDifficulty = 92`).

The result: from approximately level 200 onward, levels *feel* increasingly samey despite
having different seeds, colours, and layouts, because the player's *only* viable strategy
is to replay until they discover the single optimal path — other difficulty axes
(more colours, sinks, mixed capacities) are drowned out by the tight move budget.

### Why this matters

- **Engagement decay**: Players in the 200–1000+ range report a "wall" where levels
  blur together and the only feedback loop is "retry until perfect."
- **Star economy collapse**: With `slack ≤ 0`, the star formula
  (`ScoreCalculator.CalculateStars`) returns either 5 (optimal) or 0 (any mistake).
  The middle star tiers (1–4) become unreachable, undermining the graded-reward feel.
- **Score formula underutilisation**: `ScoreCalculator.CalculateLevelScore` multiplies
  base score by a performance multiplier `0.10 + 1.90·x⁴` where
  `x = 1 − delta/slack`. When `slack = 0`, this collapses to 1.0 (perfect) or 0.10
  (any mistake) — the smooth performance curve vanishes.

---

## 2. Current Systems Analysis

### 2.1 Move Budget

`MoveAllowanceCalculator.ComputeSlackFactor(levelIndex)` linearly interpolates from
2.0 (level 1) to 1.0 (level 500). This is the *only* knob controlling the
difficulty-via-precision axis.

**Key formula:**
```
slack = 2.0 - (levelIndex - 1) / 499
allowed = ceil(optimalMoves × slack)
```

### 2.2 Difficulty Profile Generation

`LevelDifficultyEngine.GetProfile` computes per-level parameters:
- **Bands A–E**: A (1–10), B (11–25), C (26–50), D (51–75), E (76–100+)
- **Colour count**: 3 → 7 (steps at levels 6, 10, 17, 19, 20+)
- **Empty bottles**: 2 (tutorial) → 1 → 2 (with sink adjustments)
- **Sinks**: 0 (levels ≤ 19) → up to 5 (level 1000+) via hash-based rolls
- **Reverse moves (scramble depth)**: 6–18, with colour and sink bonuses
- All parameters **clamp at `MaxEffectiveLevel = 100`**; beyond level 100
  only seed-based randomness varies

### 2.3 Capacity Diversity

`CapacityProfile.ForLevel` introduces bottle-size variety:
- Tutorial (≤ 5): uniform capacity 4
- Early (6–15): pool {3, 4, 5}, 2 distinct minimum
- Mid (31–50): pool {2, 3, 4, 5, 6, 7}, 3 distinct, 1 small + 1 large required
- Late (86–100+): pool {2, 3, 4, 5, 6, 7, 8}, 5 distinct, 2 small + 2 large required

### 2.4 Level Selection & Quality Gating

`MonotonicLevelSelector` generates 16 candidates per level and selects the one closest
to the target intrinsic difficulty curve (35 → 92 over levels 1–200, then plateau).

`QualityThresholds.ForBand` gates metrics:
- Forced-move ratio: ≤ 0.70 (A) → ≤ 0.45 (E)
- Branching factor: ≥ 1.2 (A) → ≥ 1.5 (E)
- Trap score: ≥ 0.05 (A) → ≥ 0.20 (E)
- Mixed bottles: ≥ 1 (A) → ≥ 3 (E)

### 2.5 Intrinsic Difficulty Scoring

`DifficultyScorer.ComputeIntrinsicDifficulty100` aggregates:
- **Solution length** (0–40 pts): piecewise linear, plateaus at 12+ moves
- **Branching factor** (0–25 pts): normalized (ABF−1)/3.5
- **Trap score** (0–20 pts): quadratic emphasis (TrapScore²×20)
- **Decision frequency** (0–10 pts): 1−ForcedMoveRatio
- **Solution uniqueness** (0–5 pts): 5 if single solution, 3 if ≤3, else 1

### 2.6 Scoring & Star Economy

`ScoreCalculator.CalculateStars(optimal, used, allowed)`:
- Stars awarded in 20% bands of slack: δ=0→5★, δ≤20%→4★, ..., δ>80%→0★
- When `slack=0`: only 5★ (δ=0) or 0★ (any δ>0) are possible

`ScoreCalculator.CalculateLevelScore`:
- Base score: `60 + 60·d^0.7` where d = (difficulty100−70)/30
- Performance multiplier: `0.10 + 1.90·x⁴` where x = 1−δ/slack
- Clean-solve bonus: +25

`StarEconomy.ResolveAwardedStars`: caps stars based on reset count
(0 resets→5★ max, 1→4★, 2→3★, 3+→2★) and blocks assisted levels.

### 2.7 Persistence

`ProgressData` stores:
- Session: `CurrentLevel`, `CurrentScore`, `StarBalance`, `CurrentSeed`
- Lifetime: `HighestUnlockedLevel`, `HighScore`, `CompletedLevels`,
  `BestPerformances` (per-level: `BestStars`, `BestMoves`, `BestDeviation`,
  `TimesCompleted`, `BestEfficiency`, `BestGrade`)
- Streaks: `SessionCurrentPerfectStreak`, `SessionBestPerfectStreak`,
  `LifetimeBestPerfectStreak`, `LifetimeOptimalCount`
- Themes: `UnlockedThemes`

`ProgressStore` serializes via JSON with no schema version field — any additive
fields require backward-compatible defaults.

---

## 3. Candidate Ideas (17 total)

### Scoring Rubric

| Axis | Weight | Description |
|------|--------|-------------|
| A — Engagement uplift | 0.20 | Variety, excitement, freshness at high levels |
| B — Reduces overshadowing | 0.20 | Makes tight move budget less overwhelmingly central |
| C — Implementation effort | 0.15 | Lower effort = higher score (1=hard, 5=easy) |
| D — Risk | 0.10 | Lower risk = higher score |
| E — Testability | 0.10 | Easier deterministic testing = higher |
| F — Save compatibility | 0.15 | Safer additive persistence = higher |
| G — Mechanic compatibility | 0.10 | Better fit with existing stars/restarts/sinks/gen |

Scale: 1 (poor) to 5 (excellent). Weighted total out of 5.0.

### 3.1 — Slack-Floor Adjustment

**Description:** Set a minimum slack factor (e.g. 1.15×) so the allowed-moves
count never equals the optimal count. Even at level 500+, players get at least
1–2 spare moves.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 5 | 5 | 5 | 5 | 5 | 5 | **4.45** |

**Pros:** Trivially restores graded star tiers; single constant change.  
**Cons:** Reduces raw precision pressure — may feel like a difficulty nerf.

### 3.2 — Bonus-Move Objectives

**Description:** Award 1–3 bonus allowed moves for completing in-level
secondary objectives (e.g. "sort the sink bottle first," "never pour into
an empty bottle," "use ≤ 2 colours of empty staging"). Objectives are
deterministically chosen based on level seed.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 5 | 5 | 2 | 3 | 3 | 4 | 4 | **3.85** |

**Pros:** Adds a strategic layer beyond "find optimal path"; varies by level.  
**Cons:** Requires objective generation + validation logic; UI for objective display.

### 3.3 — Sink-Conversion Spending

**Description:** Let players spend stars (via existing `StarEconomy.ConvertSinksCost = 10`)
to convert sink bottles into normal pourables *before* starting, trading star
economy progress for easier puzzles. Already partially scaffolded.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 3 | 4 | 4 | 4 | 5 | 5 | **3.85** |

**Pros:** Leverages existing constant; gives stars more spending utility.  
**Cons:** Only helps on sink levels; doesn't address non-sink overshadowing.

### 3.4 — Par-Based Grading (Replace Optimal-Only Stars)

**Description:** Replace the optimal-move-only top grade with a "par" system:
define par = optimal + small buffer (e.g. 1–2), award 5★ for meeting par
rather than exact optimal. The existing `CalculateStars` 20%-band system
continues but anchored to par instead of optimal.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 5 | 4 | 4 | 5 | 4 | 5 | **4.35** |

**Pros:** Smooths the all-or-nothing cliff; easy to test; preserves feel.  
**Cons:** Purists may dislike that 5★ no longer requires perfection.

### 3.5 — Multi-Solution Preference in Generation

**Description:** Bias `MonotonicLevelSelector` to prefer candidates with
`SolutionMultiplicity ≥ 2` at high levels, so there are multiple valid
optimal paths. This makes "find *an* optimal path" less needle-in-haystack.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 4 | 4 | 3 | 5 | 5 | 5 | **4.00** |

**Pros:** Purely generation-time change; no persistence impact; testable.  
**Cons:** May conflict with `MinSolutionMultiplicity=1` in quality thresholds;
multi-solution puzzles may feel "easier" to some.

### 3.6 — Capacity-Extremity Escalation

**Description:** Beyond level 100, widen the capacity pool further (e.g.
add capacity 1 or capacity 9+), increasing asymmetric-pour complexity
independently of the move budget.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 3 | 3 | 3 | 4 | 5 | 4 | **3.55** |

**Pros:** New visual variety; interacts with sinks interestingly.  
**Cons:** Capacity 1 bottles may be too restrictive; visual/animation changes.

### 3.7 — Streak-Bonus Slack

**Description:** Award +1 to slack for the *next* level after every N
consecutive optimal solves (e.g. every 3). Uses existing
`SessionCurrentPerfectStreak`. Rewards sustained excellence with
brief breathing room.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 3 | 3 | 3 | 4 | 4 | 5 | **3.55** |

**Pros:** Rewards skill dynamically; uses existing persistence fields.  
**Cons:** Makes difficulty player-dependent, breaking deterministic difficulty.

### 3.8 — Hidden-Colour Bottles (Fog of War)

**Description:** At high levels, some bottles start with their top layer
hidden (rendered as "?" until the player pours from that bottle or
examines it). Adds information-gathering as a new challenge dimension.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 5 | 5 | 1 | 2 | 2 | 5 | 3 | **3.40** |

**Pros:** Radically new gameplay dimension; very engaging.  
**Cons:** Major implementation (new state model, solver changes, animations);
high risk of bugs; solver must handle partial information.

### 3.9 — "Challenge Modifier" System

**Description:** At level 200+, deterministically assign one of several
modifiers per level (e.g. "No Empty Pour," "Max 2 Undos," "Reverse Sink
Polarity — sinks become sources"). Modifiers are cosmetic labels +
constraint checks.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 5 | 4 | 2 | 2 | 3 | 4 | 3 | **3.35** |

**Pros:** High variety; each modifier creates a distinct feel.  
**Cons:** Each modifier needs its own solver support + validation + testing.

### 3.10 — Scoring-Curve Rework (Diminishing Precision Returns)

**Description:** Change `CalculateLevelScore`'s performance multiplier from
`x⁴` (strongly rewards near-optimal) to a softer curve like `x²` or
`0.3 + 0.7·x²` so that 2–3 extra moves still yield meaningful scores.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 4 | 5 | 3 | 5 | 5 | 4 | **4.00** |

**Pros:** Pure formula change; easy to test; no persistence migration.  
**Cons:** Affects all levels retroactively; may inflate historical scores.

### 3.11 — Dynamic Sink-Difficulty Scaling

**Description:** Beyond level 100, let `DetermineSinkCount` scale more
aggressively (e.g. guaranteed 2+ sinks by level 200, 3+ by level 400).
Sinks add qualitatively different planning (one-way pours).

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 3 | 4 | 3 | 4 | 5 | 5 | **3.70** |

**Pros:** Leverages existing sink mechanic; no new concepts.  
**Cons:** Sink counts are already hash-based; deterministic change may
affect existing levels (need seed-conditional rollout).

### 3.12 — "Efficiency Band" Display Enhancement

**Description:** Show the player their efficiency band (S/A/B/C/D/E) with
a progress bar toward the next band, rather than just stars. This reframes
the goal from "get optimal" to "improve my grade" — reducing
perceived binary outcomes.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 3 | 3 | 4 | 4 | 5 | 5 | **3.80** |

**Pros:** Uses existing grade system; additive UI only.  
**Cons:** UI-only change won't alter gameplay; may not satisfy core issue.

### 3.13 — Reverse-Move Depth Escalation

**Description:** Increase `ComputeReverseMoves` beyond its current cap of 18
for levels 200+. Deeper scrambles produce states where the optimal path
is harder for the solver to find quickly, indirectly increasing variety.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 2 | 4 | 3 | 4 | 5 | 4 | **3.40** |

**Pros:** Single constant change; testable.  
**Cons:** May increase generation time significantly; doesn't directly
help the player-side overshadowing problem.

### 3.14 — "Mastery Star" (6th Star for Optimal)

**Description:** Add a gold/platinum 6th star exclusively for optimal
completion, keeping 5★ achievable at slightly-above-optimal. This
preserves the perfection chase as a bonus without making it mandatory.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 4 | 3 | 3 | 4 | 3 | 4 | **3.55** |

**Pros:** Elegant reframing; mastery-seekers still chase; casual players
feel rewarded at 5★.  
**Cons:** Requires persistence migration (star cap change); UI layout
impact (6th star position in banner).

### 3.15 — "Warm-Up" Pre-Level Preview

**Description:** Before starting a level, show the player a 3-second
animation of 2–3 moves of the optimal path, then reset. This gives
a hint without solving the puzzle, reducing frustration from opaque
optimal paths.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 4 | 3 | 2 | 3 | 3 | 5 | 4 | **3.40** |

**Pros:** Reduces frustration; good onboarding feel.  
**Cons:** May trivialise short-optimal levels; animation system work;
raises questions about clean-solve bonus eligibility.

### 3.16 — Trap-Score Emphasis in Selection

**Description:** Modify `MonotonicLevelSelector`'s candidate selection to
weight trap score more heavily at high levels (e.g. prefer candidates with
TrapScore ≥ 0.4 at level 200+). Traps create meaningful wrong-move
consequences that add variety beyond move counting.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 3 | 3 | 4 | 3 | 5 | 5 | 5 | **3.80** |

**Pros:** Generation-time only; no persistence impact; testable.  
**Cons:** May reduce candidate pool; doesn't change fundamental slack issue.

### 3.17 — Level-Theme "Epochs" with Rule Variants

**Description:** Divide the level space into epochs (e.g. 1–100 classic,
101–200 "capacity challenge," 201–300 "sink gauntlet," 301–400
"minimal bottles") where each epoch emphasises a different difficulty
axis. The active epoch modifies generation parameters.

| A | B | C | D | E | F | G | **Weighted** |
|---|---|---|---|---|---|---|-------------|
| 5 | 5 | 2 | 2 | 3 | 5 | 3 | **3.60** |

**Pros:** Maximum variety; each epoch feels fresh; aligns with band themes.  
**Cons:** Complex design; epoch transitions need balance; risk of some
epochs being much harder/easier than intended.

---

## 4. Ranking Summary

| Rank | ID | Idea | Weighted Score |
|------|----|------|---------------|
| 1 | 3.1 | Slack-Floor Adjustment | **4.45** |
| 2 | 3.4 | Par-Based Grading | **4.35** |
| 3 | 3.5 | Multi-Solution Preference | **4.00** |
| 3 | 3.10 | Scoring-Curve Rework | **4.00** |
| 5 | 3.2 | Bonus-Move Objectives | **3.85** |
| 5 | 3.3 | Sink-Conversion Spending | **3.85** |
| 7 | 3.12 | Efficiency Band Display | **3.80** |
| 7 | 3.16 | Trap-Score Emphasis | **3.80** |
| 9 | 3.11 | Dynamic Sink Scaling | **3.70** |
| 10 | 3.17 | Level-Theme Epochs | **3.60** |
| 11 | 3.6 | Capacity-Extremity Escalation | **3.55** |
| 11 | 3.7 | Streak-Bonus Slack | **3.55** |
| 11 | 3.14 | Mastery Star (6th Star) | **3.55** |
| 14 | 3.8 | Hidden-Colour Bottles | **3.40** |
| 14 | 3.13 | Reverse-Move Depth Escalation | **3.40** |
| 14 | 3.15 | Warm-Up Pre-Level Preview | **3.40** |
| 17 | 3.9 | Challenge Modifier System | **3.35** |

---

## 5. Top 5 Recommendations with Implementation Sketches

### Recommendation 1: Slack-Floor Adjustment (Score: 4.45)

**Summary:** Enforce a minimum slack factor of 1.15× so that `movesAllowed`
is always at least ⌈optimalMoves × 1.15⌉, guaranteeing at least one spare
move at every level.

**Implementation sketch:**

```csharp
// MoveAllowanceCalculator.cs
public const float MinimumSlackFactor = 1.15f;

public static float ComputeSlackFactor(int levelIndex)
{
    if (levelIndex <= 1) return 2.0f;
    if (levelIndex >= 500) return MinimumSlackFactor; // was 1.0f

    float t = (levelIndex - 1) / 499f;
    float slack = 2.0f - t * (2.0f - MinimumSlackFactor);
    return Math.Max(MinimumSlackFactor, Math.Min(2.0f, slack));
}
```

**Impact on existing systems:**
- `CalculateStars`: The 20%-band system immediately works again since `slack > 0`
  guarantees star tiers 1–4 are reachable.
- `CalculateLevelScore`: Performance multiplier `x⁴` has a meaningful curve again.
- `BestPerformances`: Existing records remain valid — players who achieved optimal
  under the old system simply have stronger records.
- No persistence migration needed.

**Testing:**
- Unit test: `ComputeSlackFactor(500) >= 1.15f`
- Unit test: `ComputeMovesAllowed(profile500, 10) >= 12` (at least ⌈10×1.15⌉)
- Integration: Verify star distribution at levels 400–500 includes 1–4★ tiers

**Estimated effort:** ~2 hours (1 constant change + tests)

---

### Recommendation 2: Par-Based Grading (Score: 4.35)

**Summary:** Introduce a "par" concept where 5★ is awarded for completing
within `optimalMoves + parBuffer` rather than exactly at optimal. The par
buffer scales inversely with slack: generous when slack is tight.

**Implementation sketch:**

```csharp
// New: ParCalculator.cs (Domain/Rules)
public static class ParCalculator
{
    public static int ComputePar(int optimalMoves, int movesAllowed)
    {
        int slack = movesAllowed - optimalMoves;
        // When slack is ≤ 2, add a 1-move par buffer
        // When slack is ≤ 5, add 1. When slack > 5, par = optimal.
        int buffer = slack <= 2 ? 2 : (slack <= 5 ? 1 : 0);
        return optimalMoves + buffer;
    }
}

// ScoreCalculator.CalculateStars modification:
// Replace optimalMoves with par for star calculation only
int par = ParCalculator.ComputePar(optimalMoves, movesAllowed);
int effectiveDelta = movesUsed - par; // shift baseline
```

**Impact on existing systems:**
- Stars become more achievable at high levels without changing the move limit
- Score formula continues to use raw optimal for `CalculateLevelScore`
  (maintaining score-based differentiation)
- `BestPerformances.BestMoves` is unaffected (still tracks raw moves)
- No persistence migration required

**Synergy with Recommendation 1:** These combine excellently — Slack-Floor
provides the mechanical breathing room, Par-Based Grading provides the
reward-curve smoothing. Together they eliminate the "5★ or 0★" cliff.

**Estimated effort:** ~4 hours (new class + modify CalculateStars + tests)

---

### Recommendation 3: Multi-Solution Preference in Generation (Score: 4.00)

**Summary:** Modify `MonotonicLevelSelector` to prefer candidates with
`SolutionMultiplicity ≥ 2` when the target difficulty is ≥ 70, so that
high-level puzzles have multiple valid optimal paths rather than a single
needle-in-haystack solution.

**Implementation sketch:**

```csharp
// MonotonicLevelSelector.Generate — modify candidate scoring
int score = Math.Abs(candidate.IntrinsicDifficulty - targetDiff);

// At high difficulty, reward multi-solution candidates
if (targetDiff >= 70 && candidate.Metrics.SolutionMultiplicity >= 2)
{
    score = Math.Max(0, score - 3); // 3-point bonus toward target match
}
```

**Impact on existing systems:**
- `QualityThresholds.MinSolutionMultiplicity` (currently 1 for all bands)
  remains the floor; this is an additive preference, not a hard gate
- Changes only which candidate is *selected*, not the generation algorithm
- Deterministic: same level index → same selection (just different preference weights)
- No persistence impact

**Testing:**
- Unit test: Verify selector prefers multi-solution candidate when difficulties
  are equidistant from target
- Statistical test: Sample 100 levels at index 200+ and verify average
  `SolutionMultiplicity` increases vs. baseline

**Estimated effort:** ~3 hours (selection logic + tests)

---

### Recommendation 4: Scoring-Curve Rework (Score: 4.00)

**Summary:** Soften the `x⁴` exponent in `CalculateLevelScore`'s performance
multiplier to `x²`, so that being 2–3 moves above optimal still yields
meaningful (non-trivial) scores.

**Implementation sketch:**

```csharp
// ScoreCalculator.CalculateLevelScore
// Before: double perfMult = 0.10 + 1.90 * Math.Pow(x, 4.0);
// After:
double perfMult = 0.20 + 1.80 * Math.Pow(x, 2.0);
```

**Score impact analysis:**

| x (performance) | Old multiplier (x⁴) | New multiplier (x²) |
|-----------------|---------------------|---------------------|
| 1.0 (optimal) | 2.00 | 2.00 |
| 0.8 | 0.87 | 1.35 |
| 0.6 | 0.35 | 0.85 |
| 0.4 | 0.15 | 0.49 |
| 0.2 | 0.10 | 0.27 |
| 0.0 | 0.10 | 0.20 |

Near-optimal play (x ≥ 0.6) is much better rewarded, reducing the
cliff feel.

**Impact on existing systems:**
- `CalculateTotalScore` decay formula is unaffected
- `BestPerformances.BestGrade` uses `CalculateGrade` (efficiency-based),
  which is separate and unchanged
- Historical scores in `HighScore` remain valid (they were earned under
  the old curve; new scores are simply different)

**Estimated effort:** ~1 hour (constant change + test updates)

---

### Recommendation 5: Bonus-Move Objectives (Score: 3.85)

**Summary:** Introduce per-level deterministic objectives that, when met,
grant +1 to +2 bonus allowed moves. Objectives add a strategic planning
layer that diversifies gameplay beyond raw optimal-path discovery.

**Candidate objectives (each deterministically selected via level seed):**

| Objective | Bonus | Condition |
|-----------|-------|-----------|
| "Sink First" | +2 | First move pours into a sink bottle |
| "No Empty Staging" | +2 | Never pour into an empty bottle |
| "Monochrome Sequence" | +1 | Complete all bottles of one colour before starting another |
| "Minimal Restarts" | +1 | Complete on first attempt (no undo) |
| "Diverse Pours" | +1 | Pour from ≥ N distinct source bottles |

**Implementation sketch:**

```csharp
// New: LevelObjective.cs (Domain/Rules)
public sealed class LevelObjective
{
    public string Id { get; }
    public string Description { get; }
    public int BonusMoves { get; }
    // ... condition evaluator
}

// New: ObjectiveSelector.cs (Domain/Rules)
public static class ObjectiveSelector
{
    public static LevelObjective ForLevel(int levelIndex, int seed)
    {
        if (levelIndex < 50) return null; // no objectives for early levels
        // Deterministic hash to pick from objective pool
        int roll = HashCombine(levelIndex, seed) % ObjectivePool.Length;
        return ObjectivePool[roll];
    }
}
```

**Impact on existing systems:**
- `MoveAllowanceCalculator.ComputeMovesAllowed` signature would accept an
  optional bonus parameter: `int allowed = ceil(optimal × slack) + bonus`
- `LevelState` needs a field for active objective and completion status
- `LevelPerformanceRecord` could optionally track objective completions
- UI: Banner or HUD element showing the active objective
- Solver doesn't need to solve *with* objectives — they're player-optional

**Estimated effort:** ~2–3 days (objective framework + generation + UI + tests)

---

## 6. Recommended Implementation Order

```
Phase 1 (Quick Wins — 1 day):
  ├── 1. Slack-Floor Adjustment     [~2 hours]
  └── 2. Scoring-Curve Rework       [~1 hour]

Phase 2 (Grading Improvement — 1 day):
  └── 3. Par-Based Grading          [~4 hours]

Phase 3 (Generation Quality — 1 day):
  └── 4. Multi-Solution Preference  [~3 hours]

Phase 4 (Engagement Layer — 2-3 days):
  └── 5. Bonus-Move Objectives      [~2-3 days]
```

**Rationale:** Phases 1–2 address the core problem (binary rewards, zero
slack) with minimal code risk. Phase 3 improves puzzle quality
at the generation layer. Phase 4 adds the most engagement but requires
the most implementation effort.

**Phases 1–3 together** (estimated 2–3 days) eliminate the overshadowing
problem for the vast majority of players. Phase 4 is recommended for a
subsequent release.

---

## 7. Backward Compatibility Analysis

| Change | Persistence Impact | Migration Needed? |
|--------|-------------------|-------------------|
| Slack-Floor | None — only affects future `movesAllowed` computation | No |
| Par-Based Grading | Adds `ParBuffer` concept; `BestStars` remains comparable | No |
| Multi-Solution Preference | Changes which candidate seed is selected for new levels | No (existing completed levels unaffected) |
| Scoring-Curve Rework | Future `CalculateLevelScore` values differ from historical | No (`HighScore` is cumulative; individual level scores not stored) |
| Bonus-Move Objectives | New optional field in `LevelPerformanceRecord` | No (additive field; `null`/absent = no objective) |

All five recommendations are **fully additive** — no existing persisted
data needs migration, deletion, or reinterpretation. `ProgressStore`'s
JSON deserialization handles missing fields gracefully via defaults.

---

## 8. Open Questions & Future Work

1. **Slack-floor constant tuning:** Should `MinimumSlackFactor` be 1.10×,
   1.15×, or 1.20×? Playtest data from levels 400+ is needed to determine
   the sweet spot.

2. **Par-buffer scaling:** Should the par buffer be a fixed 1–2 moves, or
   scale with optimal move count (e.g. ⌈optimalMoves / 8⌉)?

3. **Historical score normalization:** If scoring curve changes, should
   the banner show "personal best under current rules" vs. absolute best?

4. **Epoch system (3.17):** Ranked #10 but could be a high-engagement
   long-term project. Worth prototyping after Phase 4 if player retention
   data supports it.

5. **Hidden-colour bottles (3.8):** Highest engagement potential but
   requires solver redesign for partial information. Could be explored as
   a separate "challenge mode" outside the main progression.

6. **Clean-solve bonus interaction:** If par-based grading is adopted,
   should the +25 clean-solve bonus apply when the player meets par
   (not optimal), or only for true optimal completion?

7. **A/B testing framework:** Any of these changes would benefit from
   server-side A/B testing. Since Decantra is offline-only, consider
   a seed-based cohort split (even seeds = control, odd = treatment)
   for future measurement.
