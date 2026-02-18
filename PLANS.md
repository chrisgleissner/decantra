# PLANS

Last updated: 2026-02-18
Owner: Copilot autonomous run

## Execution Plan

- [x] Replace this file as the authoritative plan for the current redesign.
- [x] Implement deterministic progressive multi-sink count selection with max 5 and no sinks before level 20.
- [x] Enforce deterministic sink structural class (required vs avoidable) during generation with bounded retries and diagnostics.
- [x] Extend solver API with `allowSinkMoves` mode while preserving gameplay semantics.
- [x] Expand HUD to two aligned rows with `RESET | OPTIONS | STARS` and dynamic star balance text.
- [x] Add Star Trade-In modal with:
  - [x] Convert all sinks (10 stars) + confirmation + disabled states.
  - [x] Auto-solve (15/25/35 by difficulty tier) + confirmation + refund on failure + readable move playback.
- [x] Add assisted-level flags so assisted runs award 0 score and 0 stars.
- [x] Implement reset-count anti-farming multiplier (1.00 / 0.75 / 0.50 / 0.25) applied once at completion.
- [x] Update tutorial with STARS guidance step text.
- [x] Add deterministic validation outputs in:
  - [x] `doc/sink-progression-validation.md`
  - [x] `doc/stars-economy-validation.md`
- [x] Update/add focused tests for progression, solver modes, and star economy constraints.
- [x] Run test pass and resolve regressions introduced by this change set.

## Constraints Checklist

- [x] Deterministic behavior by level number.
- [x] Sink count never exceeds 5.
- [x] No sinks before level 20.
- [x] No negative star balance.
- [x] Gameplay move semantics unchanged.

## CI Failure Log

### Issue 1: Solvability step — Reproduction.csproj missing from git
- **Error**: `Couldn't find a project to run` in CI solvability step.
- **Root cause**: `*.csproj` in `.gitignore` excluded `Reproduction/Reproduction.csproj` from git tracking.
- **Fix**: Added `!Reproduction/Reproduction.csproj` negation to `.gitignore` and force-added the file.
- **Test**: CI solvability step now finds the project and runs `dotnet run --project Reproduction`.

### Issue 2: PlayMode test — wrong level for sink bottle test
- **Error**: `SinkBottle_CannotStartDrag_ButNormalBottleCan` failed: "Expected a sink bottle by level 24."
- **Root cause**: Test hard-coded level 24, which deterministically has 0 sinks under the current hash.
- **Fix**: Test now scans levels 20-99 for the first level with `DetermineSinkCount > 0`.
- **Test**: `GameControllerPlayModeTests.SinkBottle_CannotStartDrag_ButNormalBottleCan` passes.

## Test Coverage Added

### StarEconomyTests (EditMode) — new
- Reset multiplier: 7 boundary cases + negative resets
- Auto-solve cost: 7 boundary cases across difficulty tiers
- Awarded stars: assisted suppression, multiplier application, floor truncation
- TrySpend: sufficient/exact/insufficient balance, zero/negative cost, exhaustive non-negative sweep
- Refund: positive/zero/negative amount, exhaustive non-negative sweep
- Constants: ConvertSinksCost = 10

### DifficultyEngineTests (EditMode) — enhanced
- Distribution bands 20-99, 100-299, 1000+
- Band boundary transitions (max sink counts per band)
- ~50/50 required/avoidable class distribution

### ProgressPersistenceTests (PlayMode) — enhanced
- StarBalance round-trip persistence
- Negative StarBalance clamping on load

## Architecture Change: StarEconomy Domain Class

Extracted pure star economy functions from `GameController` (Presentation) to `StarEconomy` (Domain).
`GameController` delegates to `StarEconomy`, keeping domain logic testable without Unity.

## Stop Conditions

All satisfied:
- [x] Progressive multi-sink scaling implemented.
- [x] ~50/50 sink-required distribution enforced.
- [x] STARS UI integrated and aligned.
- [x] Convert-sinks costs 10 stars.
- [x] Auto-solve costs 15/25/35.
- [x] Reset multiplier active.
- [x] Tutorial updated with STARS step.
- [x] Validation documents produced.
- [x] All features deterministic and stable.
- [x] EditMode tests: 273 passed, 0 failed (local run 2026-02-18).
- [x] PlayMode tests: 69 passed, 0 failed, 2 skipped (local run 2026-02-18).
- [x] Coverage gate: line coverage 0.921 (threshold 0.800).
- [x] CI green: GitHub Actions run `22121553633` completed success on 2026-02-18.
