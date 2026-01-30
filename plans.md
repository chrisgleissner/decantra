# Decantra Brain-Training Upgrade Plan (Phase 1)

Status: In progress

Single source of truth: This file governs all work for the current upgrade.

## Scope summary
- Add deterministic optimal solver with cached optimal move counts.
- Replace move caps with optimal-based allowed moves and deterministic slack.
- Implement failure state on move exhaustion with reset.
- Rework scoring to be efficiency-based and non-farmable.
- Increase level structural complexity with solvability guarantees.
- Add performance feedback, persistence, and fair progression.
- Full test coverage + Android APK build.

## Phase 1: Brain-Training Puzzle Upgrade (TDD)
- [ ] Add domain tests for optimal solver correctness on known configs.
- [ ] Add domain tests for optimal solver determinism and seed consistency.
- [ ] Add domain tests for slack factor progression and allowed moves.
- [ ] Add domain tests for level complexity progression (bottles/colors/empties).
- [ ] Add domain tests for solver solvability on generated levels.
- [ ] Add domain tests for scoring commit/rollback and efficiency ranking.
- [ ] Add domain tests for grades + best-performance persistence rules.
- [ ] Add PlayMode tests for move exhaustion failure UX + input block.
- [ ] Add PlayMode tests for level reset on failure (state parity).
- [ ] Add PlayMode tests for progression lock until success.
- [ ] Implement optimal solver (pure domain) + caching on level config.
- [ ] Implement slack factor curve and allowed move calculation.
- [ ] Implement failure state flow and fly-in message.
- [ ] Implement efficiency-based scoring with provisional commit/rollback.
- [ ] Implement level generation complexity scaling (bottles/colors/empties).
- [ ] Implement performance feedback (best moves, efficiency, grade).
- [ ] Implement persistence for current level and best performance.
- [ ] Run EditMode + PlayMode tests.
- [ ] Run clean Android build and produce APK.
- [ ] Final verification and checklist tick-off.

## Explicit timeout policy
- Unity editor build step: 10 minutes max.
- Gradle build step: 8 minutes max.
- APK signing/packaging: 3 minutes max.
- Emulator boot: 5 minutes max.
- APK install: 2 minutes max.
- Any script/tool invocation: 2 minutes without output.

## Deadlock detection rules
- Poll for progress at least every 30 seconds.
- No new output or state change within 60 seconds = stalled.
- Treat any silent/stalled operation as failed and abort immediately.

## Abort and retry strategy
- Abort stalled/timeout operations immediately.
- Capture last logs/state and note in "Build Failure" or "Deadlock Prevention".
- Adjust approach (smaller scope, alternate command, reduced workload).
- Retry once with explicit safeguards and tighter scope; no blind retries.

## Build Failure / Deadlock Prevention
- 2026-01-30: Unity build aborted because another Unity instance was running on the project. Action: terminate Unity batchmode processes and retry build with guarded timeout.
- 2026-01-30: Android build failed due to JAVA_HOME pointing to JDK 25. Action: use JDK 17 and explicit SDK/NDK paths.
- 2026-01-30: APK install stalled with streamed install. Action: use --no-streaming and verify via package list if output stalls.

## Verification checklist (global)
- [ ] Optimal solver produces correct minimum moves on known configs.
- [ ] Allowed moves tighten with level; slack reaches 1.0 at high levels.
- [ ] Failure on move exhaustion blocks input and restarts level.
- [ ] Efficiency-based scoring only commits on success.
- [ ] Level complexity increases and all generated levels are solvable.
- [ ] Best performance persists and never regresses.

## APK deployment checklist
- [ ] Unity build (APK) completed within timeouts.
- [ ] Gradle/signing completed within timeouts.
- [ ] APK installed within 2 minutes (verified via package list).
- [ ] App launches and runs on device/emulator.

## Final acceptance checklist
- [ ] All requirements satisfied and verified by tests.
- [ ] No build/deploy stalls; failures documented with retries.
- [ ] plans.md updated with completed tasks and verification notes.
