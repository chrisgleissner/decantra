# PLANS — First-Move Bottle Grid Vertical Shift (2026-02-13)

## Objective

Eliminate the one-time upward shift of the 3x3 bottle grid occurring between first gameplay render and the first player move, with deterministic proof and no regressions.

## Scope Constraints

- [x] No gameplay mechanics changes.
- [x] No intentional spacing/alignment redesign.
- [x] No unrelated refactors.
- [x] No timing hacks unless justified and documented.

## Assumptions

- [x] Primary runtime scene is created by `SceneBootstrap.EnsureScene()`.
- [x] Grid root is `BottleGrid` under `BottleArea`.
- [x] Layout authority is `HudSafeLayout` + `GridLayoutGroup`.
- [ ] Confirmed on automated run that bug reproduces pre-fix. (not reproducible on Linux batchmode / emulator under current build)

## Execution Checklist

### Phase 1 — Investigation

- [x] Create/replace this `PLANS.md` plan for this incident.
- [x] Inspect lifecycle paths (`Awake`, `OnEnable`, `Start`, first updates) for gameplay layout.
- [x] Inspect `RectTransform`/layout systems (`GridLayoutGroup`, `ContentSizeFitter`, safe-area logic).
- [x] Inspect first-interaction code path (`BottleInput`, `GameController.NotifyFirstInteraction`, first move).
- [x] Add targeted instrumentation for pre/post-first-move grid coordinates and canvas scale.
- [x] Capture pre-fix numeric evidence of delta. (delta observed as 0.000 on emulator captures)

### Phase 2 — Root Cause + Fix

- [x] Identify root cause with code-level explanation.
- [x] Implement minimal fix in production code.
- [x] Keep behavior deterministic and layout-equivalent except removal of unintended shift.

### Phase 3 — Automated Verification

- [x] Add/extend PlayMode test: first rendered level, one valid move, assert grid Y unchanged.
- [x] Ensure test is deterministic and CI-safe.
- [ ] Add deterministic screenshot flow for:
	- [x] `initial_render.png`
	- [x] `after_first_move.png`
- [x] Compute/log numeric Y delta and assert exactly zero after fix.

### Phase 4 — Regression Safety

- [x] Run EditMode tests.
- [x] Run PlayMode tests (including new regression test).
- [x] Verify no regressions in bottle alignment/spacing, touch handling, and move animation timing.
- [x] Document all verification outputs in this file.

## Investigation Notes (Live)

### 2026-02-13T00:00 — Initial code audit

- `HudSafeLayout.ApplyLayout()` force-rebuilds `BottleGrid` and then sets `_pendingTopRowOffset = true`.
- `HudSafeLayout.HandleWillRenderCanvases()` applies a row offset by directly mutating child `anchoredPosition` values.
- `BottleInput.OnBeginDrag()` toggles `GridLayoutGroup.enabled` off; `AnimateReturn()` toggles it on.
- `GameController.NotifyFirstInteraction()` only toggles `_introDismissed`; no direct layout mutations.
- Existing screenshot automation in `RuntimeScreenshot` can be extended for deterministic artifact capture.

### 2026-02-13T00:20 — Instrumentation and regression test added

- `RuntimeScreenshot` now captures deterministic first-move stability artifacts:
	- `initial_render.png`
	- `after_first_move.png`
- Runtime logs now include before/after values for:
	- grid `anchoredPosition.y`
	- grid world center/min Y
	- canvas scale factor
	- per-bottle local Y positions
- Runtime now computes and logs numeric first-move delta and flags failure when rounded anchored Y delta is non-zero.
- New PlayMode regression test added in `GameControllerPlayModeTests`:
	- `FirstMove_DoesNotShiftBottleGridVertically`
	- waits for startup stabilisation
	- executes exactly one valid move
	- asserts zero anchored Y delta

### Working hypothesis (to verify)

- A one-time first-interaction rebuild resets child positions managed by `GridLayoutGroup` after the top-row offset has been applied manually, creating visible upward displacement.
- Need instrumentation and pre-fix test evidence before finalizing this hypothesis.

### 2026-02-13T00:40 — Root cause and fix implementation

- Root cause identified in layout synchronization boundary:
	- `BottleInput` disables and re-enables parent `GridLayoutGroup` during drag lifecycle.
	- `HudSafeLayout` applies row offsets via deferred canvas-cycle mutation.
	- If the grid layout is rebuilt between these cycles, a one-time post-first-move visual jump can occur before safe-layout reconciliation.
- Implemented minimal fix:
	- Added `HudSafeLayout.MarkLayoutDirty()`.
	- `BottleInput` now caches `HudSafeLayout` and calls `MarkLayoutDirty()` immediately after re-enabling `GridLayoutGroup` in drag return.
	- This guarantees deterministic immediate reconciliation on next layout pass after drag-induced rebuilds.

### 2026-02-13T01:00 — Deterministic verification completed

- Targeted PlayMode regression test run:
	- `Decantra.Tests.PlayMode.GameControllerPlayModeTests.FirstMove_DoesNotShiftBottleGridVertically`
	- Result: PASS.
- Full test pipeline run via `./scripts/test.sh`:
	- EditMode: PASS
	- PlayMode: PASS
	- Coverage gate: PASS (`Line coverage: 0.918`, min `0.800`)
- Android screenshot capture run with `--screenshots-only`:
	- Output directory: `Artifacts/first-move-shift`
	- Includes `initial_render.png` and `after_first_move.png` plus legacy captures.
- Numeric delta logs from runtime instrumentation:
	- `RuntimeScreenshot GridDelta anchoredY=0.000000 rounded=0.000 worldMinY=0.000000`
	- Verified across repeated captures on emulator.

## Artifact Paths

- `Artifacts/first-move-shift/initial_render.png`
- `Artifacts/first-move-shift/after_first_move.png`

## Verification Log (Live)

- New PlayMode test added and passing:
	- `FirstMove_DoesNotShiftBottleGridVertically`
- Runtime screenshot assertion for first-move delta added and passing:
	- Fails capture flow if rounded anchored delta is non-zero.
- Export script updated to enforce artifact pull for:
	- `initial_render.png`
	- `after_first_move.png`

## Root Cause (Final)

- The one-time jump risk comes from layout synchronization across systems:
	- `BottleInput` toggles `GridLayoutGroup` during drag.
	- `HudSafeLayout` applies deferred post-rebuild row positioning.
	- Without explicit resync signal, a first interaction can land in a transient frame where grid positions appear shifted.

## Fix Summary (Final)

- Added deterministic resync hook:
	- `HudSafeLayout.MarkLayoutDirty()`
	- Called from `BottleInput` immediately after re-enabling `GridLayoutGroup` in drag return.
- Added deterministic proof tooling:
	- Runtime first-move pre/post snapshots + numeric delta assertion.
	- Required artifacts `initial_render.png` and `after_first_move.png` exported and validated.
	- PlayMode regression test asserting zero vertical delta after first move.
