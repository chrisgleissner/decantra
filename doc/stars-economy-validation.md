# Stars Economy Validation

Date: 2026-02-17
Scope: stars HUD, trade-in actions, auto-solve pricing, reset multiplier, assisted suppression, and persistence behavior.

## Difficulty-based auto-solve pricing

Validated by `GameController.ResolveAutoSolveCost()` + `ResolveDifficultyTier()`:

| difficulty tier | `difficulty100` range | cost |
|---:|---|---:|
| 1 | `<= 65` | 15 |
| 2 | `66..85` | 25 |
| 3 | `>= 86` | 35 |

## Reset multiplier correctness

Validated by `GameController.ResolveResetMultiplier(int resetCount)` and `ResolveAwardedStars(int baseStars)`:

| resetCount | multiplier |
|---:|---:|
| 0 | 1.00 |
| 1 | 0.75 |
| 2 | 0.50 |
| 3+ | 0.25 |

Final stars are applied as `floor(baseStars * multiplier)`.

## Disabled states

Validated in `GameController.ShowStarTradeInDialog()` + `StarTradeInDialog.Show(...)`:

- Convert all sinks (10 stars): disabled if no sink exists or balance < 10.
- Auto-solve: disabled if balance < tier cost.

## Star deduction atomicity

Validated by `GameController.TrySpendStars(int cost)`:

- Balance check and subtraction occur in one guarded path.
- Save is persisted immediately after mutation.
- Negative balances are prevented via `Math.Max(0, ...)` and `ProgressStore` sanitization.

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

## Persistence checks

Validated by `ProgressData.StarBalance` + `ProgressStore`:

- Star balance is persisted in progress JSON.
- Load/save enforces non-negative balance.
- HUD STARS readout reflects persisted balance each render.

## Edge-case coverage

- No sinks present: convert option disabled.
- Insufficient stars: both actions disabled as applicable.
- Multiple resets: multiplier drops across attempts.
- Auto-solve failure: refund path executes (`RefundStars`).
- Auto-solve interruption/failure-to-start-move: flow resets level state for consistency.
