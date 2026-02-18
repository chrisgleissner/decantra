# PLANS

Last updated: 2026-02-18
Owner: GitHub Copilot (GPT-5.3-Codex)

## Mission

Validate and fix regressions end-to-end: prove screenshot provenance from the latest APK, remove startup overlay auto-show risk, enforce Star Trade-In UX requirements, validate modal consistency and tutorial highlights, regenerate screenshots, and ensure local + CI green.

## Phase 1 - Build Verification & Provenance

- [x] Confirm active branch and clean working tree.
- [x] Perform clean Android build from local branch.
- [x] Record APK path, modification timestamp, and package/version metadata.
- [x] Verify screenshot automation consumes the freshly built APK artifact.
- [x] Ensure install flow clears prior app state and handles uninstall/reinstall when needed.
- [x] Capture evidence trail: build time, install time, screenshot output times.

## Phase 2 - Startup Overlay Regression

- [x] Trace all startup lifecycle paths (`Awake`, `Start`, `OnEnable`, bootstrap wiring) for overlay activation.
- [x] Remove/guard any automatic Star Trade-In or modal activation at startup.
- [x] Verify overlay visibility defaults and non-blocking raycast state.
- [x] Verify tutorial flow remains unobstructed at first launch and replay.

## Phase 3 - Star Trade-In Redesign Compliance

- [x] Validate action cards include title, subtitle, and explicit `Costs` + `N stars` section.
- [x] Ensure no parenthesized cost indicators remain in UI copy/layout.
- [x] Ensure disabled state: greyed card, `Not enough stars`, non-clickable, cost still visible.
- [x] Ensure enabled state remains clearly interactive.
- [x] Validate confirm/cancel flow text and behavior (`Spend X stars to [action]?`).

## Phase 4 - Tutorial Highlight Validation

- [x] Validate highlight targets for bottles, HUD buttons, and stars/options references.
- [x] Validate highlight ring position/size mapping on scaled canvases.
- [x] Verify no modal/overlay obscures active tutorial highlight.

## Phase 5 - Global Modal Consistency

- [x] Validate typography roles (`ModalHeader`, `SectionTitle`, `BodyText`, `HelperText`, `ButtonText`, `CostText`) across all modals.
- [x] Validate modal responsiveness, padding rhythm, and scroll usage for long content.
- [x] Validate CTA grouping and primary/secondary separation.
- [x] Validate lifecycle: hidden by default, explicitly invoked, dismissible, no stale raycast blockers.

## Phase 6 - Multi-Resolution Validation

- [x] Validate 5.5-inch equivalent layout for tutorial and modals.
- [x] Validate standard modern Android layout.
- [x] Validate low-star/high-star states with Star Trade-In open.
- [x] Check for clipping, overlap, truncation, and anchor/scale drift.

## Phase 7 - Screenshot Regeneration

- [x] Run clean build then install fresh APK.
- [x] Regenerate all screenshot artifacts from runtime capture.
- [x] Add explicit Star Trade-In screenshot capture artifact for regression proof.
- [x] Verify captured screenshots show redesigned Star Trade-In and modal consistency.
- [x] Confirm no screenshot contains outdated parenthesized-cost UI.

## Phase 8 - Testing & CI

- [x] Run full local EditMode suite.
- [x] Run full local PlayMode suite.
- [x] Run coverage gate and confirm threshold compliance.
- [x] Add/update tests for overlay lifecycle and star trade-in state where missing.
- [ ] Push changes and verify CI checks are green on active PR.

## Exit Criteria

- [x] No overlay auto-appears on startup.
- [x] Tutorial highlights are correct and unobstructed.
- [x] Star Trade-In layout is updated and cost labels are explicit.
- [x] Disabled states and confirmation flow behave as specified.
- [x] All modal families are visually and behaviorally consistent.
- [x] Fresh screenshots are generated from latest APK and verified.
- [ ] Local tests and CI are green.
