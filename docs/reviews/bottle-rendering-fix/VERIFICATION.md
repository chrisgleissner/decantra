# Bottle Rendering Fix Verification

## Evidence

- [bottles_normal.png](docs/reviews/bottle-rendering-fix/bottles_normal.png) shows the normal gameplay state on the production background. This screenshot is also the visually complex background proof.
- [bottles_dark_background.png](docs/reviews/bottle-rendering-fix/bottles_dark_background.png) shows the bottles against a forced black background.
- [bottles_highlight_active.png](docs/reviews/bottle-rendering-fix/bottles_highlight_active.png) shows a valid pour target with the faint edge glow active.
- [bottles_highlight_removed.png](docs/reviews/bottle-rendering-fix/bottles_highlight_removed.png) shows the same scene after the target highlight is removed.
- [bottles_full_board.png](docs/reviews/bottle-rendering-fix/bottles_full_board.png) shows a full board with the top row below Reset, Options, and Stars.

## Findings

- Persistent bottle presence restored without a permanent overlay: all 9 bottle visuals include a `GlassBody` child and an active `BottleStopper`, so each bottle remains a closed 3D object while normal-state visibility comes from the glass shader rather than an always-on outline shell.
- Bottles remain translucent: the renderer still uses `Decantra/BottleGlass` with capped glass alpha and an empty-bottle edge boost, preserving visible liquid through the glass body while making empty bottles readable on dark backgrounds.
- Outline state mapping is correct: the default outline shell is hidden=True; the valid-pour highlight switches to width 0.050 and color (0.94, 0.98, 1.00, 0.38); the sink-only state hides the outline shell (sink body looks like a regular bottle, only neck+dome bands are dark).
- Valid-pour glow only appears during valid pours: the active capture used source bottle 0 -> target bottle 6 with `GetPourAmount(...) = 2`.
- Top row is fully visible: on the full-board capture the minimum measured vertical clearance between the top-row bottles and the HUD buttons is -4.2 screen pixels. Overlap detected = True. Fully on-screen = True.
- HUD overlap status: fail for Reset, Options, and Stars under the measured full-board scene.

## Requirement Check

- Glass appearance restored: pass
- Cork peeks out of each closed bottle: pass
- Empty bottles remain visible on dark backgrounds without a permanent white overlay: pass
- Sink bottles: neck and dome dark, body looks like a regular empty bottle (no inverted-hull outline): pass
- Valid-pour target uses a faint glow only when valid: pass
- Highlight reverts when removed: pass
- Top row visible beneath HUD buttons: fail
