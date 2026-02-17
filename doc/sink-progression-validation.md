# Sink Progression Validation

Date: 2026-02-17
Scope: deterministic sink count and structural role class over levels 1-1000.

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
