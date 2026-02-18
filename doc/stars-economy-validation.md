# Stars Economy Validation

Date: 2026-02-18
Scope: stars HUD, trade-in actions, auto-solve pricing, reset multiplier, assisted suppression, and persistence behavior.

## Difficulty-based auto-solve pricing

Validated by `StarEconomy.ResolveAutoSolveCost(int difficulty100)` and `StarEconomy.ResolveDifficultyTier(int difficulty100)`:

| difficulty tier | `difficulty100` range | cost |
|---:|---|---:|
| 1 | `<= 65` | 15 |
| 2 | `66..85` | 25 |
| 3 | `>= 86` | 35 |

Tests: `StarEconomyTests.AutoSolveCost_MatchesDifficultyTier`, `DifficultyTier_MatchesBoundaries`.

## Reset multiplier correctness

Validated by `StarEconomy.ResolveResetMultiplier(int resetCount)` and `StarEconomy.ResolveAwardedStars(int, int, bool)`:

| resetCount | multiplier |
|---:|---:|
| 0 | 1.00 |
| 1 | 0.75 |
| 2 | 0.50 |
| 3+ | 0.25 |

Final stars are applied as `floor(baseStars * multiplier)`.
Tests: `StarEconomyTests.ResetMultiplier_MatchesSpec`, `AwardedStars_*` family.

## Disabled states

Validated in `GameController.ShowStarTradeInDialog()` + `StarTradeInDialog.Show(...)`:

- Convert all sinks (10 stars): disabled if no sink exists or balance < 10.
- Auto-solve: disabled if balance < tier cost.

## Star deduction atomicity

Validated by `StarEconomy.TrySpend(int, int, out int)` and `GameController.TrySpendStars(int)`:

- Balance check and subtraction occur in one guarded path.
- Save is persisted immediately after mutation.
- Negative balances are prevented via `Math.Max(0, ...)` and `ProgressStore` sanitization.
Tests: `StarEconomyTests.TrySpend_*` family, `TrySpend_NeverProducesNegativeBalance`.

## Assisted-level suppression

Validated by `GameController` completion flow:

- `_isCurrentLevelAssisted` set on convert/auto-solve use.
- Assisted completion awards `0` stars (`ResolveAwardedStars`).
- Assisted completion awards `0` score (`HandleLevelComplete` skips score commit).

## Animation correctness (auto-solve)

Validated by `GameController.RunAutoSolveTradeIn()`:

- Uses real gameplay pipeline (`TryStartMove`), not direct state teleportation.
- Per-move duration forced to `0.8..1.2s` (`ResolveAutoSolveMoveDuration`, `ResolvePourWindowDuration`).
- Inter-move pause of `0.2s`.

## Performance and timing checks

- Local regression gate (`2026-02-18`) passed with:
  - EditMode: `273/273` passing
  - PlayMode: `69` passing, `0` failing, `2` skipped
  - Domain coverage gate: line coverage `0.921` (threshold `0.800`)
- Auto-solve remains intentionally readable (non-instant) with deterministic timing window:
  - move playback: `0.8..1.2s`
  - inter-move pause: `0.2s`
- Solver-backed assistance is protected by refund-on-failure path (`RunAutoSolveTradeIn` + `RefundStars`) so timing failures do not leak stars.

## Persistence checks

Validated by `ProgressData.StarBalance` + `ProgressStore`:

- Star balance is persisted in progress JSON.
- Load/save enforces non-negative balance.
- HUD STARS readout reflects persisted balance each render.
Tests: `ProgressPersistenceTests.ProgressStore_PersistsStarBalance`, `NegativeStarBalance_ClampedToZeroOnLoad`.

## Edge-case coverage

- No sinks present: convert option disabled.
- Insufficient stars: both actions disabled as applicable.
- Multiple resets: multiplier drops across attempts.
- Auto-solve failure: refund path executes (`RefundStars`).
- Auto-solve interruption/failure-to-start-move: flow resets level state for consistency.
