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
- [ ] Run EditMode tests locally.
- [ ] Run PlayMode tests locally.
- [ ] Run coverage gate locally.
- [ ] Confirm no regressions/soft-locks from overlay lifecycle changes.
- [ ] Confirm CI status is green.
