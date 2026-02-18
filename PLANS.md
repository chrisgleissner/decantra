# PLANS

Last updated: 2026-02-18
Owner: Codex

## Discovery and Audit
- [x] Audit Star Trade-In construction/wiring in `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs`.
- [x] Audit dialog lifecycle/state logic in `Assets/Decantra/Presentation/Runtime/StarTradeInDialog.cs`.
- [x] Audit controller integration and star spend execution in `Assets/Decantra/Presentation/Controller/GameController.cs`.
- [x] Audit existing test coverage and identify missing regression assertions.

## UX Redesign Specification
- [x] Replace ambiguous terminology with explicit copy: `Convert All Sink Bottles`.
- [x] Add concise sink explanation (what a sink bottle is) without paragraph-heavy text.
- [x] Define card-based structure for each action: title, subtitle, explicit cost block.
- [x] Define typography hierarchy and centralize font-size constants.

## UI Refactor Implementation
- [x] Refactor Star Trade-In layout to card-based panels with clear spacing and hierarchy.
- [x] Replace parenthesized costs with explicit `Costs` + `N stars` labels.
- [x] Increase non-header text sizes and make `Choose an option below` prominently readable.
- [x] Ensure responsive layout behavior via layout groups and explicit preferred sizes.

## Interaction and State Logic Fixes
- [x] Ensure overlay is hidden by default and never auto-shown on startup.
- [x] Ensure overlay only opens via Stars button action.
- [x] Ensure disabled action cards are greyed out, non-clickable, and show `Not enough stars` when star-gated.
- [x] Implement confirmation state switch: select action -> confirm prompt -> confirm/cancel.
- [x] Confirm flow correctness: spend stars, execute action, close overlay.
- [x] Cancel/Close reliability: always dismiss overlay and restore interactive gameplay state.
- [x] Ensure overlay ordering places it above competing overlays when visible.

## Accessibility and Layout Validation
- [x] Validate readable hierarchy: Header > Card Title > Subtitle > Cost > Helper.
- [x] Validate no text wall and clear grouping.
- [x] Validate low-star and sufficient-star visual states.
- [x] Validate level 1 and higher-level behavior for trade-in availability and clarity.

## Automated Test Coverage
- [x] Add/update tests for action button enabled/disabled state.
- [x] Add/update tests for star deduction and conversion execution.
- [x] Add/update tests for action gating on insufficient stars.
- [x] Add/update tests proving Star Trade-In is hidden on startup.
- [x] Add/update tests proving overlay is dismissible and does not block tutorial flow.

## Regression Validation
- [ ] Validate fresh-install behavior (0 stars baseline).
- [x] Run EditMode tests locally.
- [x] Run PlayMode tests locally.
- [x] Run coverage gate locally.
- [x] Confirm no regressions/soft-locks from overlay lifecycle changes.
- [ ] Confirm CI status is green.

## Production-Grade Modal UX System
### Phase 1: Full Modal Audit
- [x] Audit Options modal structure, CTA grouping, and overlay lifecycle.
- [x] Audit Tutorial modal structure, readability, and layering behavior.
- [x] Audit Terms and Conditions modal structure, scroll behavior, and dismiss flow.
- [x] Audit Privacy Policy modal structure, scroll behavior, and dismiss flow.
- [x] Audit Stars (Star Trade-In) modal structure, CTA distinction, and state feedback.

### Phase 2: Typography and Accessibility System
- [x] Define centralized modal typography roles: `ModalHeader`, `SectionTitle`, `BodyText`, `HelperText`, `ButtonText`, `CostText`.
- [x] Define centralized modal palette and state colors (normal/disabled/warning/confirm).
- [x] Enforce readable minimum text sizing for 5.5-inch equivalent layouts.
- [x] Ensure disabled and warning states are not communicated by color alone.

### Phase 3: Layout and Responsiveness Rules
- [x] Introduce shared modal sizing logic for small screens and varying aspect ratios.
- [x] Ensure no forced clipping/overflow for modal content.
- [x] Ensure vertically long content uses intentional `ScrollRect` containers.
- [x] Ensure consistent outer padding, inner padding, and vertical rhythm across modals.

### Phase 4: CTA Grouping and Interaction Model
- [x] Separate primary and secondary CTAs with explicit spacing and sectioning.
- [x] Avoid ambiguous adjacency of unrelated actions in Options and Star Trade-In.
- [x] Ensure star-spending actions remain visually distinct and clearly gated.
- [x] Ensure all modal dismiss actions are consistent and obvious.

### Phase 5: Refactor and Template Consolidation
- [x] Introduce shared `BaseModal` behavior component where feasible.
- [x] Consolidate modal construction helpers in `SceneBootstrap`.
- [x] Remove duplicated per-modal typography/spacing literals.
- [x] Preserve existing controller wiring and deterministic game behavior.

### Phase 6: Multi-Resolution Validation
- [ ] Validate fresh-install state.
- [x] Validate tutorial-active state.
- [x] Validate mid-game state with modal interactions.
- [x] Validate low-star and high-star Star Trade-In states.
- [ ] Validate low-resolution / 5.5-inch equivalent layout behavior.
- [x] Validate no clipped text, broken wrapping, or overlapping controls.

### Phase 7: Automated Tests and Regression Validation
- [x] Add/update tests for modal hidden-by-default lifecycle.
- [x] Add/update tests for no auto-show and clean dismissal/raycast behavior.
- [x] Add/update tests for button enable/disable and no-soft-lock outcomes.
- [x] Add/update tests for required layout components (scroll/layout groups) where feasible.
- [x] Run local EditMode and PlayMode suites.
- [x] Run coverage gate (>=80% domain coverage).
- [ ] Confirm CI is green.
