# Level Generation: Increasing Solution Diversity and Decision Depth

## Scope
This document reviews the current Decantra level generation logic and summarizes research-backed approaches to make levels feel less linear and more decision-heavy. It ends with concrete, code-adjacent recommendations.

## Current Level Generation Review (Code)

### Difficulty Profile
- `LevelDifficultyEngine.GetProfile()` sets `colorCount`, `emptyBottleCount`, `reverseMoves`, and a band-based theme. Empty bottles are usually 1 (levels 7-17) or 2 (<=6 and >=18). Reverse moves grows linearly and is capped at 60.

### Generation Pipeline
- `LevelGenerator.Generate()` builds a solved state, then scrambles it via reverse moves (`ScrambleState`).
- Scramble moves are *always* maximum-amount reverse pours to respect the game's "pour all" rule (`TryApplyScrambleMove` uses `GetMaxReverseAmount` and applies the maximum).
- Rejections only check:
  - No solved bottle early (first 6 levels only).
  - Empty count stays near target.
  - Level integrity and start validator.
  - Optimal solution length >= 2 (via `BfsSolver.SolveOptimal`).
- The solver is used as a pass/fail gate and for move limit sizing; it does not measure branching, solution multiplicity, or forced-move streaks.

### Move Rules
- Forward moves always pour the maximum contiguous top amount into a compatible target (`MoveRules.GetPourAmount` -> `Bottle.MaxPourAmountInto`). There are no partial pours.

### Observed Risk Factors for Linear Play
- Scramble is a random walk that is close to a single reversed solution path. That tends to produce a single, obvious undo sequence.
- Too many empty bottles (and similar capacities) create low-risk mechanical chains.
- No constraints on decision density (choice points) or on the number of alternative optimal solutions.
- `IsStructurallyComplex(...)` exists but is not used, so complexity constraints are not enforced.

## Research Findings (Relevant to Water/Sort Puzzles)

1) **Water/ball sort is NP-complete and empty-bottle availability changes solvability**
Theoretical work shows these puzzles are NP-complete and that the number of empty bins/bottles strongly affects solvability and solution length. This implies that empty-bottle count is a major difficulty lever, and excessive empties can collapse challenge by making many configurations trivially solvable.

2) **Search-based generation benefits from simulated play and heuristic objectives**
Sokoban generation research uses MCTS guided by heuristic metrics and simulated gameplay to generate solvable puzzles that vary in difficulty. This supports shifting from pure random scrambling to search-guided generation with explicit objectives (e.g., decision density, trap rate).

3) **Data-driven puzzle generation correlates features with perceived difficulty**
A data-driven Sokoban study found features correlated with perceived difficulty, built an evaluation function, and used MCTS to generate harder levels. This suggests we can learn or handcraft a difficulty objective from solver stats and player telemetry, then generate levels that optimize it.

4) **Difficulty modeling must go beyond completion rate**
Puzzle difficulty modeling research shows that completion rate alone is limited and that distributions of actions provide a richer difficulty descriptor. This supports measuring the *shape* of action distributions (e.g., forced-move streaks, branching factor, near-miss rates) rather than only optimal length.

5) **Experience-driven PCG emphasizes player-model-driven content**
Experience-driven PCG argues that content generation should adapt to player experience via computational models, not just static parameters. This encourages using player data to tune generation targets (e.g., decision density, time-to-solve, failure rate).

## Recommendations for Decantra

### A) Add Decision-Density and Branching Metrics
Measure and enforce multiple choice points instead of only optimal length.

Proposed metrics:
- **Forced-move ratio**: fraction of states along an optimal path with exactly one legal move.
- **Branching factor**: average number of legal moves along the optimal path.
- **Decision depth**: number of steps until the first state with >=2 legal moves.
- **Empty-bottle usage ratio**: percent of optimal moves that pour into empty bottles.

Suggested gate:
- Reject levels where forced-move ratio > 0.60 or decision depth > 2 (tunable per band).

### B) Require Multiple Optimal (or Near-Optimal) Solutions
Instead of accepting the first solvable scramble, estimate solution multiplicity.

Options:
- Run BFS/A* to count optimal solutions up to a cap (e.g., count up to 3) and require >=2.
- Use a limited-depth "divergence test": from the optimal path, check if at least N distinct prefixes of length K can still solve within +1 or +2 moves.

### C) Inject Trap Potential and Dead-End Risk
The current generator has little penalty for wrong moves. Add a "trap score" so that some wrong choices lead to deeper or unsolvable states.

Example test:
- From the initial state, sample M legal moves not on the optimal path.
- For each, attempt to solve with a tight node budget.
- Require that at least T% are harder or unsolved (within the budget) to ensure stakes.

### D) Reduce Mechanical Empty-Bottle Chains
Use the empty-bottle count as a primary difficulty lever (supported by complexity results).

Ideas:
- For mid-band levels, prefer 1 empty bottle, but increase other complexity to avoid frustration.
- If 2 empties are used, ensure their capacities differ (3 vs 4) or add a black bottle that can't be used as a staging bin.
- Penalize states where more than one empty bottle exists *and* both are immediately fillable by multiple sources (chain risk).

### E) Switch from Pure Random Scramble to Objective-Guided Scramble
Replace the blind random walk with an objective function and search.

Lightweight option:
- Hill-climb over scramble sequences: keep the scramble if it improves (branching factor, trap score, solution multiplicity).

Heavier option (research-backed):
- Use MCTS in reverse-move space, scoring intermediate states by the difficulty objective. This aligns with puzzle generation studies that use simulated play and heuristic objectives to generate interesting puzzles.

### F) Use Player Data to Calibrate Difficulty Targets
Track per-level action distributions and near-fail signals, then fit targets by band.

Reasoning:
- Difficulty modeling research shows action distributions provide a richer descriptor than completion rate.
- Experience-driven PCG recommends tuning content to player experience.

Minimal viable telemetry:
- Moves used (win/loss).
- Count of reversals/undo.
- Number of legal moves per turn (approx).
- Number of times an empty bottle is used.

### G) Enforce Structural Complexity (Already Implemented but Unused)
`IsStructurallyComplex(...)` exists in `LevelGenerator` but is never used. Plug it into the acceptance gate, or extend it with:
- Minimum number of mixed bottles.
- Minimum number of distinct bottle signatures.
- Minimum number of colors appearing in top positions simultaneously.

## Implementation Touchpoints (Suggested)

- `LevelGenerator.Generate(...)`:
  - After `SolveOptimal`, compute branching metrics and solution multiplicity. Reject if below threshold.
  - Add a "trap score" by sampling non-optimal moves and limited solver runs.

- `BfsSolver`:
  - Add an optional mode to count number of optimal solutions (cap it to a small number).
  - Track stats: nodes expanded, max frontier, avg branching. Use as difficulty proxies.

- `LevelDifficultyEngine`:
  - Use empty bottle count as a stronger difficulty lever (fewer empties in mid-band).

## Expected Outcome
These changes should prevent long forced-move chains, increase decision points, and create "real" stakes by making wrong choices costly. This should convert the game feel from mechanical empty-bottle shuffling to meaningful branching choices.

## Sources
- Ito et al., *Sorting balls and water: Equivalence and computational complexity*, Theoretical Computer Science, 2023. DOI: 10.1016/j.tcs.2023.114158.
- Kristensen et al., *Statistical Modelling of Level Difficulty in Puzzle Games* (arXiv:2107.03305).
- Kartal et al., *Generating Sokoban Puzzle Game Levels with Monte Carlo Tree Search*, U. Minnesota Tech Report, 2016.
- Kartal et al., *Data-Driven Sokoban Puzzle Generation with Monte Carlo Tree Search*, U. Minnesota page, 2016.
- Yannakakis & Togelius, *Experience-driven procedural content generation*, IEEE Trans. Affective Computing, 2011.

```
https://doi.org/10.1016/j.tcs.2023.114158
https://ar5iv.labs.arxiv.org/html/2107.03305
https://conservancy.umn.edu/items/a38495a0-b17d-4337-be34-d7855b81a8bc
https://motion.cs.umn.edu/r/sokoban-pcg/
https://www.um.edu.mt/library/oar/handle/123456789/29274
```
