# PLANS — Startup Fade, How to Play, Options Typography, Share Removal (2026-02-13)

## Status

- [x] Implement startup black fade-in path
- [x] Implement readable How to Play overlay
- [x] Increase options typography by approximately +2
- [x] Normalize version info to label font, size, and color
- [x] Remove short-tap level share functionality from runtime code
- [ ] Capture required screenshots to Artifacts/ (blocked: no adb device)
- [ ] Verify Android build in batch mode (blocked: Unity project already open)

## Audit Results (Before Changes)

- Options panel text system: UnityEngine.UI.Text (no TextMeshPro)
- Options font: LegacyRuntime.ttf
- Auto Size in options: disabled
- Canvas scaler mode: ScaleWithScreenSize
- Canvas scaler reference resolution: 1080 x 1920
- Canvas scaler match width/height: not explicitly set (Unity default)
- Options title size: 48
- Section header size: 45
- Toggle label size: 39
- Slider label size: 39
- Close button size: 45
- Version text size: 24
- Standard options label color: new Color(1f, 0.98f, 0.92f, 0.9f)
- Version color before: new Color(0.7f, 0.75f, 0.85f, 0.9f)

## Exact Changes Applied

- Startup fade now uses black overlay during initialization and fades gameplay in uniformly.
- Added IntroBanner.ShowBlackOverlayImmediate().
- Added IntroBanner.FadeToClear(float duration).
- Updated GameController.BeginSession() to run deterministic black-to-clear startup fade.
- Added HowToPlayOverlay with title exactly How to Play.
- Added readable scrollable help content derived from README.md (without markdown symbols).
- Added controller API: ShowHowToPlayOverlay(), HideHowToPlayOverlay(), IsHowToPlayOverlayVisible.
- Increased options typography:
- Title: 48 -> 50
- Section header: 45 -> 47
- Toggle label: 39 -> 41
- Slider labels: 39 -> 41
- Close button: 45 -> 47
- Version text: 24 -> 41
- Normalized version style to match labels exactly (font, size, color).
- Expanded options layout for clipping safety:
- Panel: 820x1020 -> 860x1160
- Option row height: 78 -> 90
- Slider container height: 64 -> 70
- Removed share behavior for short tap on LevelPanel.
- Removed share wiring and handlers from SceneBootstrap and GameController.
- Kept long-press level jump behavior intact.
- Updated PlayMode test to assert short tap does not show share UI.
- Extended runtime screenshot generation with:
- startup_fade_in_midpoint.png
- help_overlay.png
- options_panel_typography.png

## Verification and Validation

- [x] Compile diagnostics clean for modified C# files.
- [x] Runtime screenshot automation updated for required artifact names.
- [ ] EditMode/PlayMode tests after change (blocked by Unity project lock in batchmode).
- [ ] Android build verification after change (blocked by Unity project lock in batchmode).
- [ ] Screenshot capture to Artifacts/ (blocked by missing adb device/emulator).

## Screenshot Capture Steps

- Run: ./scripts/capture_screenshots.sh --apk Builds/Android/Decantra.apk --output-dir Artifacts --screenshots-only
- Verify:
- Artifacts/startup_fade_in_midpoint.png
- Artifacts/help_overlay.png
- Artifacts/options_panel_typography.png

## Regression Checklist

- [x] Startup fade runs from black with gameplay hidden until initialized.
- [x] No version footer blue emphasis remains.
- [x] Options typography consistently increased.
- [x] How to Play overlay is scrollable and high contrast.
- [x] Short-tap level share functionality removed.
- [ ] Device-level small-screen safe-area validation pending external device.

## Blockers Encountered

- Batchmode test/build blocked: another Unity instance has this project open.
- Screenshot capture blocked: no adb device connected.
