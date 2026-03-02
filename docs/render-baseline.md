# Render Baseline Metrics

Last updated: 2026-03-02  
Purpose: Document canvas-scaling model and measurement methodology for Web landscape
regression fix.

---

## 1. Canvas Scaling Model

All canvases created by `SceneBootstrap.CreateCanvas` use `CanvasScaler.ScaleMode.ScaleWithScreenSize`
with reference resolution `1080 × 1920`.

The Unity CanvasScaler `ScaleWithScreenSize` formula:

```
scaleFactor = lerp(screenWidth / refWidth, screenHeight / refHeight, matchWidthOrHeight)
canvasWidth  = screenWidth  / scaleFactor
canvasHeight = screenHeight / scaleFactor
```

### Default (mobile, `matchWidthOrHeight = 0f`, width-matching)

| Device / orientation | screenW | screenH | scaleFactor | canvasW | canvasH |
|----------------------|---------|---------|-------------|---------|---------|
| Android portrait (1080×1920) | 1080 | 1920 | 1.000 | 1080 | 1920 |
| Android portrait (1080×2400) | 1080 | 2400 | 1.000 | 1080 | 2400 |
| Web portrait (1080×1920) | 1080 | 1920 | 1.000 | 1080 | 1920 |
| Web landscape (1920×1080) | 1920 | 1080 | 1.778 | 1080 | **607.5** ← broken |

### Web landscape fix (`matchWidthOrHeight = 1f`, height-matching)

| Device / orientation | screenW | screenH | scaleFactor | canvasW | canvasH |
|----------------------|---------|---------|-------------|---------|---------|
| Web portrait (1080×1920) | 1080 | 1920 | 1.000 | 1080 | 1920 |
| Web landscape (1920×1080) | 1920 | 1080 | 0.5625 | 3413 | **1920** ✓ |

In landscape with height-matching the canvas height stays at the 1920-unit
reference. `HudSafeLayout` therefore sees identical available gameplay height as
portrait, and renders bottles at their full design size.

---

## 2. Per-Platform Effective Scaler Settings

| Platform | Component | `matchWidthOrHeight` |
|----------|-----------|---------------------|
| Android | (built-in — no `WebCanvasScalerController`) | `0f` (default) |
| iOS | (built-in — no `WebCanvasScalerController`) | `0f` (default) |
| WebGL portrait | `WebCanvasScalerController` → `Apply()` | `0f` at runtime |
| WebGL landscape | `WebCanvasScalerController` → `Apply()` | `1f` at runtime |

`WebCanvasScalerController` is guarded by `#if UNITY_WEBGL && !UNITY_EDITOR` and
never compiled into Android or iOS builds.

---

## 3. Key Layout Invariants

The following values are measured from `HudSafeLayout`'s "equal-gaps" model and
must hold on all four validated targets.

| Metric | Portrait reference | Tolerance |
|--------|-------------------|-----------|
| `BottleGrid.cellSize` | (220, 420) logical units | exact |
| `BottleGrid.sizeDelta` (design) | (820, 1300) logical units | exact |
| Row count (9-bottle level) | 3 | exact |
| Ideal inter-row gap | `(availH − 3×420) / 4` | ≤ 0.5 px delta |
| Bottle top-Y row 1 (canvas-local) | `desiredTop − idealGap` | ≤ 0.5 px delta |
| Bottle centre-X (centre column) | `canvasWidth / 2` | ≤ 0.5 px delta |
| HUD bottom-Y (TopHud lower edge) | `−218 − 150` logical from top of canvas | no delta |
| HUD top-Y (BottomHud) | `0` (zero-height spacer) | exact |

---

## 4. Measurement Methodology

### Automated (LayoutProbe)

`Assets/Decantra/Tests/PlayMode/Layout/LayoutProbe.cs` captures:

```csharp
public LayoutMetrics Capture(GameController controller)
```

- Iterates all active `BottleView` children of `BottleGrid`.
- Uses `RectTransform.GetWorldCorners()` converted to canvas-local coordinates via
  `canvas.transform.InverseTransformPoint(corner)`.
- Serialises full metrics to `Artifacts/layout/layout-metrics-<tag>.json`.

Compare files:
- `Artifacts/layout/layout-metrics-1.4.1.json` — known-good baseline
- `Artifacts/layout/layout-metrics-current.json` — current build under test
- `Artifacts/layout/layout-metrics-compare.md` — delta report

Pass threshold: `|delta| ≤ 1 px` and `|normalised delta| ≤ 0.001` for all geometry
metrics.

### Manual verification checklist

1. Web portrait at 1080 × 1920 viewport:
   - [ ] Bottles fill full viewport height (same as Android portrait screenshot).
   - [ ] HUD visible at top and bottom.
   - [ ] No overlap between HUD and bottles.

2. Web landscape at 1920 × 1080 viewport:
   - [ ] Gameplay area is centred horizontally.
   - [ ] Background fills the full viewport (no black bars).
   - [ ] Bottle height is the same proportion of viewport height as in portrait.
   - [ ] HUD elements remain centred and readable.
   - [ ] No bottle overlap.

3. Android / iOS portrait:
   - [ ] Visual output identical to pre-fix build (pixel baseline screenshots unchanged).

---

## 5. Reference Screenshots

| Tag | File | Notes |
|-----|------|-------|
| Web (broken) | `/mnt/data/Screenshot_20260302_223628.png` | Tiny bottles vs normal HUD |
| Android (correct) | `/mnt/data/screenshot-07-level-36.png` | Authoritative baseline |

---

## 6. Regression Guard

`ModalSystemPlayModeTests.TutorialAndStarModals_UseResponsiveAndScrollableStructures`
asserts `scaler.matchWidthOrHeight == 0f` for `Canvas_UI`.  This test runs in the
Unity Editor where `UNITY_WEBGL` is not defined, so `WebCanvasScalerController` is
absent and the assertion holds.  Any regression that removes the compile-time guard
and changes Editor behaviour would immediately fail this test.
