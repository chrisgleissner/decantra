# PLANS

Last updated: 2026-02-17  
Owner: Copilot autonomous run

## Execution Plan

- [x] Replace this file as the authoritative plan for the current redesign.
- [ ] Implement deterministic progressive multi-sink count selection with max 5 and no sinks before level 20.
- [ ] Enforce deterministic sink structural class (required vs avoidable) during generation with bounded retries and diagnostics.
- [ ] Extend solver API with `allowSinkMoves` mode while preserving gameplay semantics.
- [ ] Expand HUD to two aligned rows with `RESET | OPTIONS | STARS` and dynamic star balance text.
- [ ] Add Star Trade-In modal with:
  - [ ] Convert all sinks (10 stars) + confirmation + disabled states.
  - [ ] Auto-solve (15/25/35 by difficulty tier) + confirmation + refund on failure + readable move playback.
- [ ] Add assisted-level flags so assisted runs award 0 score and 0 stars.
- [ ] Implement reset-count anti-farming multiplier (1.00 / 0.75 / 0.50 / 0.25) applied once at completion.
- [ ] Update tutorial with STARS guidance step text.
- [ ] Add deterministic validation outputs in:
  - [ ] `doc/sink-progression-validation.md`
  - [ ] `doc/stars-economy-validation.md`
- [ ] Update/add focused tests for progression, solver modes, and star economy constraints.
- [ ] Run test pass and resolve regressions introduced by this change set.

## Constraints Checklist

- [ ] Deterministic behavior by level number.
- [ ] Sink count never exceeds 5.
- [ ] No sinks before level 20.
- [ ] No negative star balance.
- [ ] Gameplay move semantics unchanged.
