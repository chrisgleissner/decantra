# Bottle Rendering Fix Verification

## Evidence

- [bottles_normal.png](docs/reviews/bottle-rendering-fix/bottles_normal.png) shows the normal gameplay state on the production background. This screenshot is also the visually complex background proof.
- [bottles_dark_background.png](docs/reviews/bottle-rendering-fix/bottles_dark_background.png) shows the bottles against a forced black background.
- [bottles_highlight_active.png](docs/reviews/bottle-rendering-fix/bottles_highlight_active.png) shows a valid pour target with the white curved highlight active.
- [bottles_highlight_removed.png](docs/reviews/bottle-rendering-fix/bottles_highlight_removed.png) shows the same scene after the target highlight is removed.
- [bottles_full_board.png](docs/reviews/bottle-rendering-fix/bottles_full_board.png) shows a full board with the top row below Reset, Options, and Stars.

## Findings

- Persistent bottle presence restored without a permanent overlay: all 9 bottle visuals include a `GlassBody` child, and solved bottles render a dedicated 3D cork instead of the old flat hovering UI stopper.
- Bottles remain translucent: the renderer still uses `Decantra/BottleGlass` with capped glass alpha and an empty-bottle edge boost, preserving visible liquid through the glass body while making empty bottles readable on dark backgrounds.
- Hovering white rectangle regression fixed: enabling 3D presentation now disables the legacy 2D stopper, the 3D cork base references the bottle rim/flange top so the cork sits flush across different bottle sizes, and unsolved bottles no longer render the pale placeholder cap.
- Outline state mapping is correct: the default outline shell is hidden=True; the valid-pour highlight switches to width 0.055 and color (1.00, 1.00, 1.00, 1.00); the sink state switches to width 0.043 and color (0.00, 0.00, 0.00, 0.94).
- Valid-pour white outline only appears during valid pours: the active capture used source bottle 0 -> target bottle 6 with `GetPourAmount(...) = 2`.
- Top row is fully visible: on the full-board capture the minimum measured vertical clearance between the top-row bottles and the HUD buttons is -0.5 screen pixels. Overlap detected = False. Fully on-screen = True.
- HUD overlap status: pass for Reset, Options, and Stars under the measured full-board scene.
- Bottle shadows are shorter and subtler in the latest emulator regeneration: the refreshed layout report recorded `shadowLengthRatioMax=0.0597` with `shadowOverlapDetected=false` in the 2026-03-08 10:32 UTC run.
- Full emulator capture now completes with the UI-tail outputs present: the rebuilt APK produced fresh `help_overlay.png`, `options_panel_typography.png`, `options_audio_accessibility.png`, `options_starfield_controls.png`, `options_legal_privacy_terms.png`, `star_trade_in_low_stars.png`, and `screenshot-10-options.png` in the 2026-03-08 10:32 UTC run.
- The top-level phone screenshot set was regenerated end-to-end in the same emulator-backed pass: core gameplay captures, cork/completion evidence, layout reports, and the Play Store numbered screenshots now share fresh 2026-03-08 10:32-10:33 UTC timestamps.
- Screenshot write path is now deterministic: `RuntimeScreenshot.CaptureScreenshot(...)` captures a texture and writes the PNG directly instead of relying on asynchronous Android file writes, removing the prior false-success/missing-file failure mode.

## Requirement Check

- Glass appearance restored: pass
- Cork peeks out of each closed bottle: pass
- Empty bottles remain visible on dark backgrounds without a permanent white overlay: pass
- Sink bottles use black outlines: pass
- Valid-pour target uses white curved outline only when valid: pass
- Highlight reverts when removed: pass
- Top row visible beneath HUD buttons: pass
- Hovering white rectangle removed from solved bottles: pass
- Solved bottles use liquid-tinted corks instead of neutral placeholder caps: pass
- Full emulator screenshot regeneration including options/help/star-trade tail: pass
