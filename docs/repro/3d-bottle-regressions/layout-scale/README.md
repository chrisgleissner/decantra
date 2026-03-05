# Layout Scale Regression Evidence

## Issues
- Level 20: tallest bottle (row 2, col 1) overlapped the bottle above it.
- Level 36: tallest bottles (row 1, cols 2-3) extended behind the HUD.

## Fix (2026-03-05)

Added `VisualScale = 0.92f` constant in `Bottle3DView`.
Applied as `_worldRoot.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f)`.

**Effect**: All bottles reduced uniformly by 8%.
- Relative capacity-ratio height differences preserved (ratio applied via mesh body
  height scaling, not transform scale).
- Bottle positions unchanged — canvas anchor sync drives XY.
- Liquid fill proportions unchanged — shader-driven by capacity ratio.

## Diagnostic log output
`CheckLayoutSafety()` runs 0.5 s after each level loads and logs:
```
[Bottle3DView] LayoutDiag spawnedBottleVisualCount=9  maximumBottleHeight=...  verticalSpacing=...
```
Any `LAYOUT VIOLATION` error in logcat/editor console indicates a regression.

## Expected screenshots (captured after Android build)

- `level_20_before.png` — tallest bottle overlaps row above
- `level_20_after.png`  — no overlap, proper spacing
- `level_36_before.png` — bottles behind HUD
- `level_36_after.png`  — all bottles below HUD line
- `level_grid_3x3.png`  — representative 3×3 grid, even spacing

Screenshots are captured via `./build --screenshots`.
