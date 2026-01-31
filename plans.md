# Decantra Physical Brain-Teaser Redesign Plan

Status: In progress

Single source of truth: This file governs all work for the redesign.

## Scope summary
- Introduce variable bottle capacities with visible size differences.
- Remove slack via sparse empties and tight initial fills.
- Optional sink bottle (irreversible when full).
- Increase structural complexity early (level scaling).
- Update solver + move limits for capacity constraints.
- Failure state on move exhaustion with restart.
- Efficiency-only scoring on success.
- Learnable progression and visual cues.
- Full automated test coverage + Android APK build.

## Phase 0: Recon + constraints
- [x] Confirm Unity version, packages, and project settings.
- [x] Audit current domain model for bottles, capacities, solver, and level gen.
- [x] Identify current UX hooks for failure messaging and reset.

## Phase 1: Tests first (TDD)
- [x] Add domain tests for variable capacities and overfill prevention.
- [x] Add domain tests for initial fill legality and reduced slack.
- [x] Add domain tests for sink bottle irreversibility (if implemented).
- [x] Add domain tests for solver correctness under capacity constraints.
- [x] Add domain tests for allowed move limits with tightening slack.
- [x] Add domain tests for level scaling (bottle count, capacity variance, empties).
- [x] Add domain tests for solvability of generated levels.
- [x] Add domain tests for scoring commit only on success + efficiency ranking.
- [x] Add PlayMode tests for move exhaustion failure and input block.
- [x] Add PlayMode tests for restart restoring exact initial state.

## Phase 2: Implementation
- [x] Implement variable capacities with visible bottle sizing.
- [x] Implement reduced slack initial fills and legality checks.
- [x] Implement sink bottle type + visuals (if adopted).
- [x] Update solver + state hashing to include capacity constraints.
- [x] Update allowed moves to use optimal + tightening slack.
- [x] Implement failure state + neutral UX + restart flow.
- [x] Implement efficiency-only scoring commit on success.
- [x] Update level generator to scale earlier (9+ bottles by ~20-25).
- [x] Add learning curve staging for capacity asymmetry.

## Phase 3: Validation
- [x] Run EditMode tests.
- [x] Run PlayMode tests.
- [x] Verify coverage >= 80% on domain logic.
- [x] Validate solver minimality on capacity-constrained configs.
- [x] Verify failure resets and no score/progression persists on failure.

## Phase 4: Build
- [x] Run clean Android build and produce APK.
- [x] Confirm build success in output.
- [x] Ensure no test-only code ships.

## Final acceptance checklist
- [x] All new mechanics tested and passing.
- [x] Difficulty scales as required by level 20-25.
- [x] Solver + move limits remain tight and fair.
- [x] APK built successfully.
