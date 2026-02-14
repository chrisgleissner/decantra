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
- [x] ALL 9 bottles shift uniformly upward by the same pixel amount.

## Root Cause Analysis

### Problem Statement
When the first playable level appears and the user drags a bottle and releases it, all 9 bottles
shift upward by ~10 px uniformly. The shift is permanent and only occurs once per session.

### Root cause: Double-rebuild race between HudSafeLayout and CanvasUpdateRegistry

When `AnimateReturn` re-enables the `GridLayoutGroup` (`gridLayout.enabled = true`),
`GridLayoutGroup.OnEnable()` calls `SetDirty()` → `MarkLayoutForRebuild()`, which registers a
pending rebuild with Unity's `CanvasUpdateRegistry`.

The frame then proceeds:

1. **LateUpdate** → `HudSafeLayout.ApplyLayout()`:
   - Sets grid spacing, padding, sizeDelta
   - Calls `ForceRebuildLayoutImmediate(bottleGrid)` — creates a **separate** LayoutRebuilder
     from pool, rebuilds, releases it. Does NOT remove the pending rebuilder from
     `CanvasUpdateRegistry`.
   - Calls `ApplyTopRowsDownwardOffset()` — shifts top 2 rows down by ~35px (scaled).

2. **Canvas.willRenderCanvases** → `CanvasUpdateRegistry.PerformUpdate()`:
   - Finds the **original** pending rebuild from `OnEnable.SetDirty()`.
   - Runs `GridLayoutGroup.SetLayoutVertical()` → positions ALL children to canonical grid
     positions, **overwriting** the top-row offset from step 1.

3. **Render** → bottles at canonical positions (offset lost).

### Additional contributing factor: RectOffset reference inequality

`ApplyLayout()` created `new RectOffset(...)` each frame for `bottleGridLayout.padding`. Since
`RectOffset` is a class (reference type), `LayoutGroup.SetProperty<T>` may use `Object.Equals`
(reference comparison), finding the new instance ≠ old instance, which calls `SetDirty()` →
`MarkLayoutForRebuild()` every frame. This could cause the same double-rebuild race on every
frame (not just on re-enable), making `ApplyTopRowsDownwardOffset()` NEVER visible.

### Three compounding bugs identified

**Bug 1 — CanvasUpdateRegistry double-rebuild (PRIMARY CAUSE)**
See above. `GridLayoutGroup.OnEnable()` → `SetDirty()` → pending rebuild overwrites the top-row
offset applied in `LateUpdate`.

**Bug 2 — `hudSafeLayout` is always `null` in `BottleInput`**
Bottles live under `Canvas_Game`. `HudSafeLayout` lives on `Canvas_UI`.
`GetComponentInParent<HudSafeLayout>()` never finds it. The `MarkLayoutDirty()` call in
`AnimateReturn` was a **silent no-op**.

**Bug 3 — Cumulative offset during drag**
While `GridLayoutGroup` is disabled (during drag), `ForceRebuildLayoutImmediate` is a no-op
(disabled grid skipped by `LayoutRebuilder.PerformLayoutCalculation`), yet the deferred
`willRenderCanvases` callback still ran `ApplyTopRowsDownwardOffset()`, reading CURRENT positions
(already offset) and subtracting again — cumulatively pushing top-2-row bottles down over drag
frames.

### Fix

1. **Flush pending rebuild on re-enable**: In `AnimateReturn`, call `Canvas.ForceUpdateCanvases()`
   immediately after `gridLayout.enabled = true`. This processes the `CanvasUpdateRegistry` pending
   rebuild NOW, so it no longer overwrites the offset later during `willRenderCanvases`.

2. **Cache `RectOffset` instance**: In `HudSafeLayout.ApplyLayout()`, reuse the same `RectOffset`
   instance when padding values haven't changed, preventing spurious `SetDirty()` →
   `MarkLayoutForRebuild()` from reference inequality.

3. **Use `yield return null` instead of `WaitForEndOfFrame`**: In `AnimateReturn`, the animation
   loop now uses `yield return null` so the grid re-enable happens during the Update phase (before
   LateUpdate), giving `HudSafeLayout.ApplyLayout()` a chance to apply ForceRebuild + TopRowOffset
   in the SAME frame's LateUpdate — before rendering.

4. **Guard against cumulative offset**: In `ApplyTopRowsDownwardOffset()`, return early if
   `GridLayoutGroup` is disabled. Removes cumulative offset accumulation during drag.

5. **Inline offset application**: `ApplyTopRowsDownwardOffset()` is called directly in
   `ApplyLayout()` after `ForceRebuildLayoutImmediate`, no deferred callback.

6. **Fix null reference**: In `BottleInput`, use `FindFirstObjectByType<HudSafeLayout>()` instead
   of `GetComponentInParent<HudSafeLayout>()` for cross-canvas discovery.

## Execution Checklist

### Phase 1 — Investigation
- [x] Inspect lifecycle paths and first-interaction code path.
- [x] Inspect layout systems (GridLayoutGroup, HudSafeLayout, ApplyTopRowsDownwardOffset).
- [x] Identify root cause with code-level explanation.
- [x] Identify CanvasUpdateRegistry double-rebuild as primary cause of uniform shift.

### Phase 2 — Fix
- [x] `BottleInput.cs`: `Canvas.ForceUpdateCanvases()` after grid re-enable; `yield return null`.
- [x] `HudSafeLayout.cs`: cached `_cachedPadding`; inline offset; grid-disabled guard.
- [x] `BottleInput.cs`: `FindFirstObjectByType<HudSafeLayout>()` for cross-canvas.
- [x] Ensure fix is deterministic and layout-equivalent.

### Phase 3 — Automated Verification
- [x] `FirstDragRelease_DoesNotShiftBottlePositions` PlayMode test with per-bottle delta assertion.
- [x] Drag-release simulation + JSON report + screenshot evidence in `RuntimeScreenshot.cs`.
- [x] Test sequence matches actual `AnimateReturn` flow: re-enable → ForceUpdateCanvases → MarkDirty.

### Phase 4 — Regression Safety
- [x] EditMode tests: **207 passed, 0 failed** (2026-02-14).
- [x] PlayMode tests: **54 passed, 0 failed, 2 skipped** (2026-02-14).
- [ ] Re-run tests after latest changes (Canvas.ForceUpdateCanvases + RectOffset caching).
- [ ] On-device screenshot verification via `./build --screenshots`.

## Artifact Paths

- `Artifacts/drag-release-test/` — PlayMode test artifacts (before/after screenshots, report.json)
- `Artifacts/first-move-shift/` — RuntimeScreenshot evidence (requires device)

## Verification Log

- **2026-02-14 10:03**: Initial fix committed (3a19483): inline offset, guard, null reference fix.
- **2026-02-14 10:06**: Test committed (d8bbb3e): drag-release regression test + RuntimeScreenshot.
- **2026-02-14 10:16**: PlayMode tests 54/54 passed (prior to latest CanvasUpdateRegistry fix).
- **2026-02-14 10:47**: EditMode tests 207/207 passed.
- **2026-02-14**: Root cause refined: CanvasUpdateRegistry double-rebuild as primary cause.
- **2026-02-14**: Additional fixes: `Canvas.ForceUpdateCanvases()` flush, `RectOffset` caching,
  `yield return null` timing improvement. Pending re-test.
