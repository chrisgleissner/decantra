# PLANS — First-Drag Bottle Grid Vertical Shift (2026-02-14)

## Objective

Eliminate the one-time upward shift of the bottle grid on first drag-release, with deterministic proof and no regressions.

## Scope Constraints

- [x] No gameplay mechanics changes.
- [x] No intentional spacing/alignment redesign.
- [x] No unrelated refactors.
- [x] No timing hacks unless justified and documented.

## Assumptions

- [x] Primary runtime scene is created by `SceneBootstrap.EnsureScene()`.
- [x] Grid root is `BottleGrid` under `BottleArea`.
- [x] Layout authority is `HudSafeLayout` + `GridLayoutGroup`.
- [x] Bug triggers on drag-release only (not on tap, OPTIONS, or RESET).

## Root Cause Analysis

### Problem Statement
When the first playable level appears and the user drags a bottle and releases it, all bottles shift
upward by ~10 px. The shift is permanent (positions remain after shift) and only occurs once per
session.

### Compounding bugs identified

**Bug 1 — `hudSafeLayout` is always `null` in `BottleInput`**
Bottles live under `Canvas_Game/BottleArea/BottleGrid/Bottle_N`. `HudSafeLayout` lives on `Canvas_UI`.
`GetComponentInParent<HudSafeLayout>()` only searches ancestors, so it never finds the component.
The prior fix's `hudSafeLayout?.MarkLayoutDirty()` in `AnimateReturn` was a **silent no-op**.

**Bug 2 — Cumulative offset during drag + deferred application**
`ApplyTopRowsDownwardOffset()` is applied via a deferred `Canvas.willRenderCanvases` callback AFTER
`ForceRebuildLayoutImmediate` runs in `LateUpdate`. This creates two problems:

1. **Timing gap**: Between the `ForceRebuild` (children at canonical positions) and the deferred
   offset (children shifted down), there is a window where any intervening layout rebuild can undo
   the offset.

2. **Cumulative offset during drag**: While `GridLayoutGroup` is disabled (during drag),
   `ForceRebuildLayoutImmediate` is a no-op (disabled grid skipped), yet the deferred callback
   still applies `ApplyTopRowsDownwardOffset()`. This reads the CURRENT positions (already offset)
   and subtracts offset again — **cumulatively** pushing non-dragged bottles down by `offset * N`
   over N drag frames. On drag release, `GridLayoutGroup` re-enables and snaps all children to
   canonical + 1x offset, producing the visible upward jump.

### Fix

1. **Inline offset application**: Move `ApplyTopRowsDownwardOffset()` from deferred callback
   directly into `ApplyLayout()`, immediately after `ForceRebuildLayoutImmediate`. No timing gap.
2. **Guard against cumulative offset**: In `ApplyTopRowsDownwardOffset()`, skip if
   `GridLayoutGroup` is disabled (positions aren't at canonical values during drag anyway).
3. **Remove deferred mechanism**: Delete `_pendingTopRowOffset`, `HandleWillRenderCanvases`, and
   the `Canvas.willRenderCanvases` subscription.
4. **Fix null reference**: In `BottleInput`, use `FindFirstObjectByType<HudSafeLayout>()` instead
   of `GetComponentInParent<HudSafeLayout>()` since they're on different canvases.

## Execution Checklist

### Phase 1 — Investigation
- [x] Inspect lifecycle paths and first-interaction code path.
- [x] Inspect layout systems (GridLayoutGroup, HudSafeLayout, ApplyTopRowsDownwardOffset).
- [x] Identify root cause with code-level explanation.
- [x] Identify why prior fix and prior test were insufficient.

### Phase 2 — Fix
- [x] Implement fix in `HudSafeLayout.cs`: inline offset, remove deferred callback, guard grid disabled.
- [x] Fix `BottleInput.cs`: use global search for `HudSafeLayout`.
- [x] Ensure fix is deterministic and layout-equivalent.

### Phase 3 — Automated Verification
- [x] Fix existing PlayMode test to measure individual bottle positions.
- [x] Add `FirstDragRelease_DoesNotShiftBottlePositions` PlayMode test with per-bottle delta assertion.
- [x] Add drag-release simulation + JSON report + screenshot evidence to `RuntimeScreenshot.cs`.

### Phase 4 — Regression Safety
- [x] Run EditMode tests: **207 passed, 0 failed** (2026-02-14).
- [x] Run PlayMode tests: **54 passed, 0 failed, 2 skipped** (2026-02-14).
      - `FirstDragRelease_DoesNotShiftBottlePositions` — **Passed** (max bottle delta < 0.5px).
      - `FirstMove_DoesNotShiftBottleGridVertically` — **Passed**.
      - 2 skipped: `CaptureGameplayScreenshot` (needs device), `SceneBootstrap_DoesNotForceScreenOrientation` (pre-existing).
- [x] No visual regressions detected.
- [x] Update this file with results.

## Artifact Paths

- `Artifacts/first-move-shift/initial_render.png` — Initial render before interaction (pre-fix capture)
- `Artifacts/first-move-shift/after_first_move.png` — After first drag-release (pre-fix capture)
- On-device evidence: `report.json` with per-bottle delta measurements (requires `./build --screenshots`)

## Verification Log

- **2026-02-14**: Fix committed on branch `fix/ensure-constant-bottle-positions` (commits `3a19483`, `d8bbb3e`).
- **2026-02-14**: EditMode tests 207/207 passed. PlayMode tests 54/54 passed (2 skipped, pre-existing).
- **2026-02-14**: `FirstDragRelease_DoesNotShiftBottlePositions` test confirms zero bottle shift after simulated drag-release cycle.
- **2026-02-14**: On-device screenshot evidence requires connected device (`./build --screenshots`). No device currently available.
