# PLANS - Android Black Screen After Logo Fade (Rendering Regression)

## Scope
Fix the critical Android runtime regression where the app renders a black screen immediately after the logo/splash fade-out. The SFX indicator remains visible in the top-right corner but no gameplay content is rendered. The app does not crash and Unity continues running.

## Root Cause (Confirmed)
`CreateIntroBanner()` in `SceneBootstrap.cs` creates a full-screen UI overlay with a solid black background at `alpha = 1.0`. During normal gameplay (non-screenshot mode), `IntroBanner.Play()` is **never called** — it is only invoked by `RuntimeScreenshot` in screenshot capture mode. Without `Play()`, `SetBackgroundAlpha(0f)` is never executed, leaving the opaque black overlay permanently covering all game/background camera content.

### Why the SFX indicator remains visible
The settings panel (containing the SFX toggle) is created *after* the IntroBanner in the UI canvas hierarchy, giving it a higher sibling index. Canvas rendering draws later siblings on top, so the SFX toggle renders above the black overlay.

### Why automated screenshots were correct
`RuntimeScreenshot.CaptureIntroScreenshot()` calls `intro.Play()`, which runs the full fade sequence and ends with `SetBackgroundAlpha(0f)`. By the time level screenshots are captured, the overlay is transparent.

### Why existing tests did not catch this
- Unity EditMode/PlayMode tests don't instantiate the full runtime UI hierarchy.
- Screenshot capture uses `RuntimeScreenshot` which explicitly calls `Play()` — a code path that never runs during normal app startup.
- No test asserted the IntroBanner's initial visual state or its effect on the render output.

## Hypotheses (Validated)
- [x] H1: **IntroBanner overlay blocks rendering** — CONFIRMED. Full-screen black Image at alpha 1.0 on UI canvas, never dismissed.
- [x] H2: Camera lifecycle issue — REFUTED. All three cameras (Background, Game, UI) are created correctly in `EnsureRenderCameras()`.
- [x] H3: URP/render pipeline misconfiguration — REFUTED. Project uses built-in pipeline (no SRP asset assigned).
- [x] H4: Scene transition issue — REFUTED. Single scene (`Main.unity`), no scene transitions.
- [x] H5: Android surface issue — REFUTED. Surface is fine; rendering continues underneath the overlay.

## Fix
Change `CreateIntroBanner()` to initialise the background Image with `alpha = 0` instead of `alpha = 1`. The `PrepareForIntro()` method (called inside `Play()`) already sets background alpha to 1 before the fade sequence, so screenshot mode continues to work correctly.

## Plan (Ordered, Checkable)
- [x] 1. Diagnose root cause: audit IntroBanner creation, lifecycle, and Play() call sites.
- [x] 2. Confirm `Play()` is never called outside screenshot mode.
- [x] 3. Confirm IntroBanner background starts at alpha 1.0 in `CreateIntroBanner()`.
- [x] 4. Update PLANS.md with findings.
- [x] 5. Fix: Change IntroBanner background initial color to `(0,0,0,0)` in `CreateIntroBanner()`.
- [x] 6. Add EditMode test: IntroBanner background must start transparent.
- [x] 7. Add emulator-native screenshot validation (adb screencap + luminance check).
- [x] 8. Run EditMode + PlayMode tests (146/146 passed, coverage 91.3%).
- [x] 9. Build APK and verify on Android emulator (no black screen).
- [x] 10. Capture emulator screenshot evidence and validate (median luma 69.3, 0.3% near-black).
- [x] 11. Confirm all CI gates pass (tests green, coverage gate passed).

## Emulator Screenshot Validation
- Capture via `adb exec-out screencap -p` after logo fade-out.
- Validate: reject if median luminance < 15 (on 0-255 scale).
- Validate: reject if >95% of pixels are near-black (R+G+B < 30).
- Store screenshots as build artifacts under `Builds/Android/emulator-screenshots/`.
- Deterministic, headless, runs on CI.

## Acceptance Criteria
- Logo fades in and out correctly.
- Gameplay renders immediately after splash.
- No black screen on Android emulator.
- Emulator-native screenshot shows visible gameplay (bottles, background, UI).
- Orientation correct (portrait).
- All unit tests pass.
- All existing screenshots unchanged.
- Coverage gate passes.
- No tests weakened, skipped, or disabled.

## Previous Fix (Orientation — Resolved)
The prior orientation regression (`defaultScreenOrientation: 1` = PortraitUpsideDown) was fixed on `main` by setting `defaultScreenOrientation: 0` (Portrait) and removing the runtime `Screen.orientation` call and duplicate AndroidManifest orientation attribute.
