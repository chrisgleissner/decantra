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
- [x] Add domain tests for optimal solver correctness on known configs.
- [x] Add domain tests for optimal solver determinism and seed consistency.
- [x] Add domain tests for slack factor progression and allowed moves.
- [x] Add domain tests for level complexity progression (bottles/colors/empties).
- [x] Add domain tests for solver solvability on generated levels.
- [x] Add domain tests for scoring commit/rollback and efficiency ranking.
- [x] Add domain tests for grades + best-performance persistence rules.
- [x] Add PlayMode tests for move exhaustion failure UX + input block.
- [x] Add PlayMode tests for level reset on failure (state parity).
- [x] Add PlayMode tests for progression lock until success.
- [x] Implement optimal solver (pure domain) + caching on level config.
- [x] Implement slack factor curve and allowed move calculation.
- [x] Implement failure state flow and fly-in message.
- [x] Implement efficiency-based scoring with provisional commit/rollback.
- [x] Implement level generation complexity scaling (bottles/colors/empties).
- [x] Implement performance feedback (best moves, efficiency, grade).
- [x] Implement persistence for current level and best performance.
- [x] Run EditMode + PlayMode tests.
- [x] Run clean Android build and produce APK.
- [x] Final verification and checklist tick-off.

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
- 2026-01-30: Tests failed to start because Unity Editor was not found. Action: install Unity 6000.3.5f2 and set UNITY_PATH to the editor executable, then rerun tools/test.sh.
- 2026-01-30: Release APK build crashed (segfault) without toolchain env. Action: rerun with ANDROID_SDK_ROOT, ANDROID_NDK_ROOT, and JAVA_HOME set explicitly.

## Verification checklist (global)
- [x] Optimal solver produces correct minimum moves on known configs.
- [x] Allowed moves tighten with level; slack reaches 1.0 at high levels.
- [x] Failure on move exhaustion blocks input and restarts level.
- [x] Efficiency-based scoring only commits on success.
- [x] Level complexity increases and all generated levels are solvable.
- [x] Best performance persists and never regresses.

## APK deployment checklist
- [x] Unity build (APK) completed within timeouts.
- [x] Gradle/signing completed within timeouts.
- [x] APK installed within 2 minutes (verified via package list).
- [x] App launches and runs on device/emulator.

## Final acceptance checklist
- [x] All requirements satisfied and verified by tests.
- [x] No build/deploy stalls; failures documented with retries.
- [x] plans.md updated with completed tasks and verification notes.
