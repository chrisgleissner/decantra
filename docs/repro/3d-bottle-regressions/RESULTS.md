# 3D Bottle Regressions — Results

**Branch**: `feat/3d-bottles`  
**Date**: 2026-03-04  
**Tests**: 361 / 361 passed (EditMode)

---

## Fixed Regressions

### Block A — Liquid colours washed out by glass shader

**Root cause**: `BottleGlass.shader` accumulated alpha up to 0.48 at grazing angles (`_GlassTint.a=0.18 + fresnel * _FresnelColor.a(0.6) * 0.5`). Near-white glass at ~50% opacity visually dominates the transparent liquid colour behind it.

**Fix** (`Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader`):
- Reduced `_GlassTint` default alpha `0.18 → 0.15`
- Reduced `_FresnelColor` default alpha `0.6 → 0.42`
- Added `_MaxGlassAlpha` property (default `0.26`) — hard cap on total glass face alpha  
  `alpha = min(alpha, _MaxGlassAlpha)` replaces `alpha = saturate(alpha)`
- Added `_SpecMaxContrib` property (default `0.12`) — cap on additive specular luminance  
  `specCol = _SpecColor2.rgb * min(spec * _SpecColor2.a, _SpecMaxContrib)`

**Result**: At least 74% liquid colour shows through (`1 - 0.26 = 0.74`) at all angles.

---

### Block B — All bottles appear the same height (size variance lost)

**Root cause**: `Bottle3DView.EnsureInitialised()` always creates `_worldRoot` with `localScale = Vector3.one`. The 2D canvas equivalent (`BottleView.ApplyCapacityScale()`) modifies canvas element heights — invisible in 3D mode (canvas alpha = 0).

**Fix** (`Assets/Decantra/Presentation/View3D/Bottle3DView.cs`, `Render()`):
```csharp
float ratio = _levelMaxCapacity > 0
    ? Mathf.Clamp01((float)bottle.Capacity / _levelMaxCapacity)
    : 1f;
_worldRoot.transform.localScale = new Vector3(1f, ratio, 1f);
```
Y-only scaling matches 2D behaviour: height varies with capacity, width stays constant.

---

### Block C — Auto-solver screenshots show bottles at rest (not mid-move)

**Root cause**: `CaptureAutoSolveEvidence` fired screenshot on `pourCompletedCount > lastCapturedPour` — this fires after the pour animation ends and the bottle has settled.

**Fix** (`Assets/Decantra/Presentation/Runtime/RuntimeScreenshot.cs`):
- Changed trigger from `PourCompleted` to 0.3 s after `PourStarted`
- `AutoSolveMinDragSeconds = 0.35 s`, so 0.3 s ≈ 85% through minimum drag (bottle visibly mid-arc)
- Files now named `auto_solve_NN_mid.png` (was `_pour.png`)
- Diagnostic log: `displacementSampleTime` reports how long after pour start the capture fired

---

### Block D — Level 10 shows 9 overlapping 3D bottles instead of 5 (3+2)

**Root cause**: `Bottle3DView._worldRoot` is a **scene-root** `GameObject` (not parented under the Canvas, to avoid lossyScale=0.005 corruption). When `GameController.Render()` calls `bottleViews[i].gameObject.SetActive(false)` for positions beyond the active level count, `Bottle3DView.Update()` stops (MonoBehaviour lifecycle), but `_worldRoot` stays active at its last tracked world-space position. For Level 10 (5 bottles), positions 5–8 retained their visible `_worldRoot` meshes → 9 overlapping 3D bottle meshes rendered at grid positions 0–8.

**Fix** (`Assets/Decantra/Presentation/View3D/Bottle3DView.cs`):
```csharp
private void OnEnable()
{
    if (_worldRoot != null)
        _worldRoot.SetActive(true);
}

private void OnDisable()
{
    if (_worldRoot != null)
        _worldRoot.SetActive(false);
}
```
`GameController` calls `bottleViews[i].gameObject.SetActive(false)` → `OnDisable()` fires → `_worldRoot.SetActive(false)` hides the 3D mesh.

---

### Block E — Sink-only (black) bottles not visually distinguishable in 3D mode

**Root cause**: `BottleView.ApplySinkStyle()` applies 2D canvas styling (heavy black bands, colour overlays) — invisible in 3D mode (canvas alpha = 0). `Bottle3DView` had no sink marking.

**Fix**:

`Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader`:
- Added `_SinkOnly` float property (Range 0–1, default 0)
- When `_SinkOnly > 0.5`: renders dark near-black bands at rim (UV.y > 0.82) and base (UV.y < 0.07)  
  Front pass: `float3(0.04, 0.04, 0.06)` at alpha 0.92  
  Back pass: `float3(0.05, 0.05, 0.07)` at alpha 0.88  
  Band edges use `saturate(delta / 0.04)` for smooth 4% UV transition

`Assets/Decantra/Presentation/View3D/Bottle3DView.cs`:
- Added `_isSinkOnly` field, `PropSinkOnly` shader property ID
- Added `SetSinkOnly(bool)` public API
- `Render()` syncs sink state from `bottle.IsSink` on every call
- `ApplySinkOnlyToGlass()`: sets `_SinkOnly` via `MaterialPropertyBlock` (no material instance created)

---

## Test Results

| Run | Tests | Passed | Failed |
|-----|-------|--------|--------|
| EditMode 2026-03-04 | 361 | 361 | 0 |

---

## Files Changed

| File | Blocks |
|------|--------|
| `Assets/Decantra/Presentation/View3D/Bottle3DView.cs` | B, D, E |
| `Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader` | A, E |
| `Assets/Decantra/Presentation/Runtime/RuntimeScreenshot.cs` | C |
| `PLANS.md` | — (Section 19 added) |

---

## Evidence Directories

Screenshots require an Android build + runtime capture (`./build --screenshots-only`).

| Block | Evidence Path |
|-------|---------------|
| A (liquid brilliance) | `liquid-brilliance/{before,after}/` |
| B (bottle size) | `bottle-size-variance/after/` |
| C (solver capture) | `solver-capture/` |
| D (level 10 layout) | `level-10-layout/` |
| E (sink visual) | `sink-only-visual/` |
