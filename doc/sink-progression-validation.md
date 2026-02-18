# Sink Progression Validation

Date: 2026-02-17
Scope: deterministic sink count and structural role class over levels 1-1000.
This report is intentionally bounded to levels 1-1000 (inclusive) and is not a global sink-count proof for all future levels.

## Deterministic sink histogram (levels 1-1000)

| sinkCount | levels |
|---:|---:|
| 0 | 177 |
| 1 | 348 |
| 2 | 313 |
| 3 | 116 |
| 4 | 46 |
| 5 | 0 |

Computed from `LevelDifficultyEngine.DetermineSinkCount(levelNumber)`.
Note: `sinkCount=5` may appear at sufficiently high levels beyond this analyzed range.

## Structural class distribution (sink levels only)

Class source: `LevelDifficultyEngine.IsSinkRequiredClass(levelNumber)`.

| class | levels |
|---|---:|
| required | 417 |
| avoidable | 406 |
| total sink levels | 823 |

Split is approximately 50/50 by deterministic parity hash.

## Cross table: sinkCount × class

| sinkCount | required | avoidable | total |
|---:|---:|---:|---:|
| 1 | 169 | 179 | 348 |
| 2 | 170 | 143 | 313 |
| 3 | 54 | 62 | 116 |
| 4 | 24 | 22 | 46 |

## Solver-mode verification constraints

Generation classification enforcement in `LevelGenerator` validates:

- Normal solve: `_solver.SolveWithPath(... allowSinkMoves: true)` must solve.
- No-sink solve: `_solver.Solve(... allowSinkMoves: false)` determines class compliance.
- Required class: no-sink solve must fail within bounded search.
- Avoidable class: no-sink solve must succeed within bounded search.
- Retry path: deterministic seed offsets per attempt/candidate.
- Hard cap: sink-class mismatch cap logs diagnostics and stops strict class enforcement after cap.

Solver API support is implemented in `BfsSolver` overloads with `allowSinkMoves`.

## Constraint checks

- No sinks before level 20: satisfied (`DetermineSinkCount` returns 0 for 1-19).
- Maximum sinks per level: satisfied (clamped distribution output 0..5).
- Deterministic by level number: satisfied (hash-only selection, no runtime RNG).
- No gameplay move-rule changes: satisfied (domain move rules unchanged).

## Performance measurements

Source: `doc/transition-benchmark-levels-1-1000.csv` (levels 1..1000, deterministic seed schedule).

- Overall generation timing: `n=1000`, mean `432.1ms`, p50 `224.4ms`, p90 `1097.8ms`, p95 `1396.6ms`, p99 `2396.1ms`, max `4909.5ms`.
- Sink-level generation timing: `n=823`, mean `390.1ms`, p95 `1349.8ms`, max `4909.5ms`.
- No-sink generation timing: `n=177`, mean `627.6ms`, p95 `1772.3ms`, max `3713.9ms`.

These measurements keep worst observed generation under the 5-second device target in this benchmark set.

## Solver timing statistics and bounds

- Generation-time solvability checks use bounded solver calls (`ResolveSolveNodeLimit`, `ResolveSolveTimeLimitMs`) with deterministic limits by level band.
- Sink-class validation uses stricter bounded checks (`ResolveSinkClassNodeLimit`, `ResolveSinkClassTimeLimitMs`) to prevent search blow-up.
- Regression gate `LevelSolvabilityRegressionTests.Levels_1_To_200_AreSolvableWithinValidationBounds` enforces:
  - bounded solve call: `maxNodes=2_000_000`, `maxMillis=10_000`
  - max observed revalidation bound: `<= 10_000ms` (asserted)
  - average revalidation bound: `<= 5_000ms` (asserted)

## Edge-case coverage

- Hard bounds: `DetermineSinkCount_RespectsHardBounds` verifies no sinks before level 20 and max sink cap 5.
- Band transitions: `DetermineSinkCount_BandBoundaries_ProduceDifferentMaximums` verifies per-band maxima.
- 5-sink emergence: `DetermineSinkCount_DistributionBands_1000Plus` verifies level 1000+ reaches sink count 5.
- Class balance: `SinkRoleClass_HasApproximatelyEqualDistribution` verifies deterministic ~50/50 required/avoidable split.
- Mode correctness: `SolverSinkModeTests` and `ReproductionSpec` verify `allowSinkMoves` behavior.
