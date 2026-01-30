# Decantra Stabilization Plan (Phase 0)

Status: In progress

Single source of truth: This file governs all work for the current fixes.

## Scope summary
- Fix source bottle color during pour (rendering).
- Ensure background changes on every new level (logic + performance).
- Increase interstitial celebration energy with star-based scaling (animation + UX).
- Make scoring monotonic and consistent (game logic).
- Build, deploy, and verify APK on device/emulator.

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
- 2026-01-30: Soak test aborted because level log marker not observed within 20s. Action: rebuild APK with log marker and re-run soak test.
- 2026-01-30: APK install failed because no adb devices detected after rebuild. Action: restart adb server and reconnect device before re-install.

## Issue-by-issue task checklist

### 1) Source Bottle Turns Black During Pour (Rendering bug)
- Expected behavior:
  - Source liquid color remains consistent during pour.
  - Visible liquid volume smoothly reduces with amount poured.
- Failure modes to guard against:
  - Shader/material resets to default/black during animation.
  - Mask or mesh swaps without preserving color state.
  - SpriteRenderer or MaterialPropertyBlock cleared mid-pour.
- Root-cause analysis steps:
  - Trace pour animation to identify where material, sprite, or mask updates occur.
  - Inspect any per-frame color or property block writes during pour.
  - Confirm color state persists on source liquid object across animation frames.
- Rendering/animation strategy:
  - Preserve source liquid color in a stable state container.
  - Apply volume reduction by mask/mesh/height parameter only.
- Data/state ownership:
  - Color owned by liquid state model; renderer reads from model.
  - Animation drives only fill amount/mask, not color.
- Implementation checklist:
  - [x] Locate pour animation and liquid renderer paths.
  - [x] Identify color loss/replacement point.
  - [x] Preserve or reapply color during pour.
  - [x] Ensure volume reduction matches poured amount.
- Verification checklist:
  - [ ] Visual/log check: no black/placeholder color in any frame.
  - [ ] Smooth volume reduction during pour.
  - [ ] Verify on device/emulator.

### 2) Background Must Change on Every New Level (Logic + performance)
- Expected behavior:
  - Each new level uses a different background.
  - Background selection deterministic or intentionally varied.
  - No stalls if background generation is heavy.
- Failure modes to guard against:
  - Background only set once at app start.
  - Level start race where background is overwritten late.
  - Expensive generation blocking gameplay.
- Root-cause analysis steps:
  - Identify background selection hook and confirm level index usage.
  - Verify background state reset when loading next level.
  - Check for async race between level load and background assignment.
- Rendering/animation strategy:
  - Assign background on level start with deterministic seed.
  - If generation is heavy, use async + fallback background.
- Data/state ownership:
  - Background selection derived from level index/seed.
  - Background renderer owns current background instance only.
- Implementation checklist:
  - [x] Locate background system and level start hook.
  - [x] Tie selection to level index/seed on every load.
  - [x] Add safe fallback while async work completes (if needed).
  - [x] Ensure no gameplay stall.
- Verification checklist:
  - [ ] Visual/log check across multiple consecutive levels.
  - [ ] Confirm no stall/freeze at level start.
  - [ ] Verify on device/emulator.
  - [ ] Use dev-only level-advance shortcut (Menu key) for on-device background verification.
  - [ ] Pull debug verification log from device for level/background changes.

### 3) Level Progression Interstitial Needs More Cheer (Animation + UX)
- Expected behavior:
  - Joyful, dynamic, celebratory interstitial.
  - Clear escalation with star count (few → subtle, many → lively).
- Failure modes to guard against:
  - Flat visuals with minimal motion.
  - Overly noisy flicker or performance drops.
  - Star count not influencing effect intensity.
- Root-cause analysis steps:
  - Locate interstitial controller and animation/particle setup.
  - Inspect star count usage and effect scaling parameters.
- Rendering/animation strategy:
  - Layer sparkles, glisten, flying stars with depth and parallax.
  - Scale spawn rate, velocity, and count with star total.
- Data/state ownership:
  - Interstitial view reads star count from score/level result model.
  - Effects are view-only; do not mutate game state.
- Implementation checklist:
  - [x] Locate interstitial UI/effects controllers.
  - [x] Add/enable sparkles, glisten, flying stars.
  - [x] Scale intensity by star count with safe caps.
  - [x] Guard against flicker; profile for mid-range devices.
- Verification checklist:
  - [ ] Visual/log check: escalation obvious with 1/2/3 stars.
  - [ ] Eye-friendly (no excessive flicker).
  - [ ] Verify on device/emulator.

### 4) Scoring Logic Is Inconsistent (Game logic bug)
- Expected behavior:
  - Score is strictly monotonically increasing.
  - More stars = higher bonus; more poured = higher gain.
- Failure modes to guard against:
  - Score decreases due to recalculation or negative deltas.
  - UI animation shows temporary dips.
  - Multiple mutation sites conflict.
- Root-cause analysis steps:
  - Identify all score mutation points and score display logic.
  - Confirm whether scoring is recalculated or incrementally added.
- Rendering/animation strategy:
  - Decouple UI tweening from underlying score state if needed.
- Data/state ownership:
  - Single authoritative score in model/state layer.
  - UI reads score and animates toward it without lowering.
- Implementation checklist:
  - [x] Enumerate score mutation points.
  - [x] Enforce monotonic updates in model layer.
  - [x] Fix UI animation to never display lower values.
  - [x] Ensure star and pour bonuses scale correctly.
- Verification checklist:
  - [ ] Log check: score only increases across gameplay and replays.
  - [ ] Bonuses reflect star/pour amounts.
  - [ ] Verify on device/emulator.
  - [ ] Pull debug verification log for score monotonicity.

### 5) README Build/Install/Sharing Instructions
- Expected behavior:
  - README contains clear, reproducible steps to build APK.
  - README contains clear device install steps.
  - README explains practical ways to share a 200MiB APK.
- Failure modes to guard against:
  - Missing environment variables or tooling steps.
  - Install steps that rely on Play Store.
  - Sharing advice that violates size or policy limits.
- Implementation checklist:
  - [ ] Add explicit build steps and environment setup.
  - [ ] Add ADB install steps and manual install options.
  - [ ] Add guidance for sharing large APKs (Drive, S3, TestFlight-style alternatives, GitHub Releases limits).
- Verification checklist:
  - [ ] README steps are complete and consistent with scripts.

## Verification checklist (global)
- [ ] Source pour color stable across full animation.
- [ ] Background changes every level; no stalls.
- [ ] Interstitial celebration scales with stars.
- [ ] Score never decreases; bonuses scale.

## APK deployment checklist
- [x] Unity build (APK) completed within timeouts.
- [ ] Gradle/signing completed within timeouts.
- [ ] APK installed within 2 minutes (verified via package list).
- [ ] App launches and runs on device/emulator.

## Final acceptance checklist
- [ ] All issues resolved and verified on device/emulator.
- [ ] No build/deploy stalls; failures documented with retries.
- [ ] plans.md updated with completed tasks and verification notes.
