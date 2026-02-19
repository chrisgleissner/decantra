# PLANS

Last updated: 2026-02-18
Owner: GitHub Copilot (GPT-5.3-Codex)

## Mission

Implement a minimal but clearly visible sink-bottle heavy-glass redesign that preserves bottle/grid layout and hitboxes, adds automated visual safety tests, produces screenshot evidence, and passes local + CI validation without touching gameplay/star-award logic.

## Constraints (Hard)

- Keep gameplay/domain behavior unchanged (solver, star awards, move rules).
- Do not change bottle spacing, grid alignment, transforms, colliders, or hitboxes.
- Sink visuals remain minimal, with no marker stripe, no added glow, and no extra overlays.
- No icons, lock graphics, floor shadows, or out-of-bounds glow/animation.
- Use shared constants for thickness and color strategy (no scene overrides, no magic numbers).

## Phase 1 - Baseline + Design Lock

- [x] Locate current sink rendering path and contour dimensions.
- [x] Choose one compliant strategy: heavier thicker/darker glass for sinks.
- [x] Define shared constants/rules for sink stroke and brightness adjustments.

## Phase 2 - Implementation

- [x] Refactor sink rendering in `BottleView` to heavy-glass contour styling.
- [x] Ensure sink contour thickening does not alter liquid width/height.
- [x] Ensure sink visuals do not alter bottle transform/layout.
- [x] Keep non-sink visuals unchanged.

## Phase 3 - Automated Safeguards

- [x] Add PlayMode layout test coverage for sink heavy-glass rendering safety.
- [x] Add PlayMode overlap test coverage to keep bottle spacing unchanged.
- [x] Add visual contrast checks for sink glass against varied backgrounds.
- [x] Add screenshot-output assertions for sink-vs-normal evidence.

## Phase 4 - Non-Regression

- [x] Add/extend regression test proving level solvability outputs unchanged by sink visual changes.
- [x] Confirm no edits to domain generation/solver logic.

## Phase 5 - Validation + Artifacts

- [x] Run local EditMode tests.
- [x] Run local PlayMode tests (including new visual safety coverage).
- [x] Build and capture screenshots via `./build --screenshots`.
- [x] Verify required sink visual artifacts exist in capture output.

## Phase 6 - CI Green

- [x] Push code and wait for CI checks to complete.
- [x] Confirm all checks green.
- [x] Update this plan to fully checked state.

## Exit Criteria

- [x] Sink glass visuals are clearly visible on light and dark backgrounds.
- [x] Sink visuals remain minimal, with no base indicator stripe.
- [x] No bottle overlap/spacing/layout regression.
- [x] Automated layout/overlap/contrast tests pass.
- [x] Screenshot artifacts include dark/light + side-by-side sink heavy-glass evidence.
- [x] Local tests pass.
- [x] CI is green.
