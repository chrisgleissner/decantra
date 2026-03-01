# Layout Regression Root Cause Report

**Tags affected:** 1.4.2-rc2 (regression) vs 1.4.2-rc1 (correct)  
**Commit:** `61f73f7` — "feat: Improve WebGL portrait rendering and tutorial highlighting (#49)"  
**Date:** 2026-03-01

---

## Symptoms

1. **Playfield overflow** — Entire gameplay area rendered larger than the screen.
   The outer border/margin separating gameplay elements from physical screen
   edges disappeared.
2. **Bottle overlap** — Bottles overlap each other. E.g. Level 506, tall bottle #7
   (row 3, column 1) overlaps the bottom of the bottle above it.
3. **Scaling change** — The playfield appears approximately 25% larger on modern
   phones (9:20 aspect ratio).

## Root Cause

Two interacting changes in `SceneBootstrap.cs`:

### Change 1: CanvasScaler height matching

```csharp
// rc1 (implicit width matching — Unity default matchWidthOrHeight = 0)
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1080, 1920);

// rc2 (explicit height matching)
scaler.matchWidthOrHeight = 1f;
```

With width matching (rc1), `scaleFactor = screenWidth / 1080`. On a 1080×2400
phone, `scaleFactor = 1.0` and the canvas logical size is `1080 × 2400`.

With height matching (rc2), `scaleFactor = screenHeight / 1920 = 2400/1920 = 1.25`.
Canvas logical size becomes `864 × 1920`. Everything is scaled up by 25%.

### Change 2: GameplayContainer with HeightControlsWidth

```csharp
// New in rc2
aspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
aspect.aspectRatio = 1080f / 1920f;
```

The container is anchored full-height, so its height = canvas height = 1920.
The fitter computes `width = 1920 × (9/16) = 1080`. But the canvas is only
864 logical units wide. The container overflows by `1080 / 864 = 1.25×`.

### Combined effect

The gameplay rect is 25% wider than the visible canvas. This pushes all content
beyond screen edges, eliminating margins and causing bottle overlap.

## Fix Applied

| Change | Before (rc2) | After (fix) |
|--------|-------------|-------------|
| `matchWidthOrHeight` | `1f` (height) | `0f` (width) |
| `AspectRatioFitter` mode | `HeightControlsWidth` | `FitInParent` |
| Container anchors | `(0.5, 0)–(0.5, 1)` | `(0, 0)–(1, 1)` |

### Why FitInParent

`FitInParent` computes the largest rect that:
- Maintains the 9:16 aspect ratio
- Fits entirely within the parent canvas rect

This guarantees the gameplay area **never overflows** regardless of device
aspect ratio, while centering it within available space.

### Layout math verification

| Device | Canvas (logical) | Container | Margin |
|--------|-----------------|-----------|--------|
| Phone 1080×2400 (9:20) | 1080 × 2400 | 1080 × 1920 | 240 px top+bottom |
| Phone 1080×1920 (9:16) | 1080 × 1920 | 1080 × 1920 | Exact fit |
| Web landscape 1920×1080 | 1080 × 607 | 341 × 607 | 370 px left+right |
| Web portrait 1080×1920 | 1080 × 1920 | 1080 × 1920 | Exact fit |

## What was preserved

- WebGL fullscreen template (`DecantraResponsive/index.html`, `style.css`)
- GameplayContainer architecture (just corrected the sizing mode)
- Tutorial spotlight shader and overlay
- iOS audio session fix
- All other rc2 features

## Files changed

- `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs` (3 line changes)
- `Assets/Decantra/Tests/PlayMode/ModalSystemPlayModeTests.cs` (1 assertion update)
