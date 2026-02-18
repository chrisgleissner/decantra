# PLANS

Last updated: 2026-02-18
Owner: GitHub Copilot (GPT-5.3-Codex)

## Mission

Implement a minimal but clearly visible sink-bottle indicator redesign that remains fully inside bottle bounds, preserves bottle/grid layout and hitboxes, adds automated visual safety tests, produces screenshot evidence, and passes local + CI validation without touching gameplay/star-award logic.

## Constraints (Hard)

- Keep gameplay/domain behavior unchanged (solver, star awards, move rules).
- Do not change bottle spacing, grid alignment, transforms, colliders, or hitboxes.
- Indicator remains minimalist, bottom-anchored, in-bounds, and non-overlapping.
- No icons, lock graphics, floor shadows, or out-of-bounds glow/animation.
- Use shared constants for thickness and color strategy (no scene overrides, no magic numbers).

## Phase 1 - Baseline + Design Lock

- [ ] Locate current sink indicator rendering path and dimensions.
- [ ] Choose one compliant strategy: dual-tone in-bounds stripe.
- [ ] Define shared constants for thickness scaling + max ratio + color tones.

## Phase 2 - Implementation

- [ ] Refactor sink indicator rendering in `BottleView` to in-bounds dual-tone stripe.
- [ ] Ensure indicator width does not exceed bottle/liquid width.
- [ ] Ensure indicator is anchored at bottle bottom and does not alter bottle transform/layout.
- [ ] Keep non-sink visuals unchanged.

## Phase 3 - Automated Safeguards

- [ ] Add PlayMode layout test: indicator width <= bottle width and height <= max ratio.
- [ ] Add PlayMode overlap test: indicator stays out of adjacent bottle rects; bottle spacing unchanged.
- [ ] Add PlayMode contrast test under light and dark backgrounds with pixel-difference threshold.
- [ ] Add screenshot-output assertions for light, dark, and side-by-side sink-vs-normal evidence.

## Phase 4 - Non-Regression

- [ ] Add/extend regression test proving level solvability outputs unchanged by visual sink indicator changes.
- [ ] Confirm no edits to domain generation/solver logic.

## Phase 5 - Validation + Artifacts

- [ ] Run local EditMode tests.
- [ ] Run local PlayMode tests (including new visual safety coverage).
- [ ] Build and capture screenshots via `./build --screenshots`.
- [ ] Verify required sink-indicator artifacts exist in capture output.

## Phase 6 - CI Green

- [ ] Push code and wait for CI checks to complete.
- [ ] Confirm all checks green.
- [ ] Update this plan to fully checked state.

## Exit Criteria

- [ ] Sink indicator is clearly visible on light and dark backgrounds.
- [ ] Indicator remains minimal, in-bounds, and base-aligned.
- [ ] No bottle overlap/spacing/layout regression.
- [ ] Automated layout/overlap/contrast tests pass.
- [ ] Screenshot artifacts include light/dark + side-by-side sink evidence.
- [ ] Local tests pass.
- [ ] CI is green.
