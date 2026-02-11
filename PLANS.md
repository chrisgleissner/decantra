
# PLANS — Ensure Correct Padding Above Top Row Bottles (2026-02-11)

## Status: COMPLETE

Goal: Fix the visual defect where tallest bottles in row 1 touch or overlap the Reset/Options buttons above them, by increasing the button-clearance target in HudSafeLayout.

## Root Cause

`HudSafeLayout.TargetButtonClearanceFactor` = 0.30 (30% of button height) produced insufficient vertical clearance between the bottom of the Reset/Options buttons and the top of row-1 bottles.

The existing `ApplyTopRowsDownwardOffset()` algorithm already detects and corrects insufficient clearance, but the target gap was too small. Increasing the constant makes the algorithm enforce a larger gap, using the existing row-shift and whole-grid-shift mechanisms.

## Plan

### 1) Implement fix (single constant change)
- [x] Increase `TargetButtonClearanceFactor` from `0.30f` to `0.55f` in `HudSafeLayout.cs`

### 2) Verify
- [x] Run EditMode + PlayMode tests via `./scripts/test.sh` — 207 EditMode passed, 52 PlayMode passed, 0 failures
- [x] All tests green, including `BottleGrid_MaintainsRowPaddingAndHudClearance`
- [x] Regenerate screenshots via `./build --screenshots` — all 10 screenshots captured
- [x] Screenshots confirm visible gap above row 1, no bottle-to-bottle or bottle-to-UI contact
- [x] Push and await CI

## Invariants preserved
- Column center alignment: unchanged (no X-coordinate changes)
- Row base alignment: unchanged (only vertical offset of rows 1-2 within the grid)
- Inter-row spacing: unchanged (shift is capped by `maxSafeOffset`)
- Bottle width/height/scaling/liquid rendering: unchanged
- Game mechanics: unchanged

## Files changed
- `HudSafeLayout.cs`: 1 constant (`TargetButtonClearanceFactor` 0.30 → 0.55)

---

# PLANS — Consistent Liquid Volume Rendering (2026-02-10)

## Status: COMPLETE

All tests pass (253 EditMode, 56 PlayMode). Installed on device and verified.

---

## Root Cause Analysis

### Problem
In 0.9.4, the same K-slot liquid stack renders at different pixel heights in bottles of
different capacities because the denominator in `localUnitHeight = H / capacity`
normalizes to the bottle's own capacity instead of a global reference, producing
per-bottle pixel-per-slot sizes that vary up to 3.3× across capacities.

### 0.9.4 measured pixel-per-slot values (H = slotRoot.rect.height):
| Cap | scaleY | pixels/slot      | Ratio vs cap-8 |
|-----|--------|------------------|-----------------|
|   2 |   0.88 | H×0.88/2 = 0.440H | 3.32× |
|   3 |   0.88 | H×0.88/3 = 0.293H | 2.21× |
|   4 |   1.00 | H×1.00/4 = 0.250H | 1.89× |
|   5 |   1.06 | H×1.06/5 = 0.212H | 1.60× |
|   6 |   1.06 | H×1.06/6 = 0.177H | 1.33× |
|   7 |   1.06 | H×1.06/7 = 0.151H | 1.14× |
|   8 |   1.06 | H×1.06/8 = 0.133H | 1.00× |

### Fix: Compensated Local Unit Height
`localUnitHeight = (H / refCapacity) × (refScaleY / bottleScaleY)`

This ensures `pixelPerSlot = (H / refCap) × refScaleY × parentScales` = CONSTANT.

---

## Checklist

### A) Preserve current work
- [x] Saved patch: `fix-branch-v2-full.patch` (1643 lines)

### B) Reset to baseline
- [x] Hard-reset to tag 0.9.4 (commit f429838)

### C) Implement fix
- [x] Added `BottleVisualMapping.cs` with canonical pixel mapping
- [x] Modified `BottleView.cs` to use canonical mapping in all liquid rendering
- [x] Modified `GameController.cs` (+9 lines: compute and set level max capacity)
- [x] Implemented sink-bottle foot (basePlate beneath bottle, 7% height, 8% wider)

### D) Tests (8 new tests, all passing)
- [x] CrossBottleInvariance_SameSlotCount_SamePixelHeight
- [x] PourInvariance_Cap3ToCap6_SamePixelHeight
- [x] PourInvariance_AllCapacityPairs_SamePixelHeight
- [x] BaselineLayoutLock_ScaleMatchesTag094
- [x] MaxCapacityBottle_FillsEntireSlotRoot
- [x] UniformCapacity_MatchesOriginalFormula
- [x] ZeroSlots_ZeroHeight
- [x] OldFormula_ProducesDifferentPixelHeights_ConfirmsBugExists

### E) Verification
- [x] 253 EditMode tests passed
- [x] 56 PlayMode tests passed (including 8 new tests)
- [x] Built and installed on device

---

## Changes from 0.9.4 (total: 6 files, +424 -27 lines)
- `GameController.cs`: +9 lines (max capacity loop before render)
- `BottleView.cs`: +83 lines, -27 lines (canonical mapping, sink foot)
- `BottleVisualMapping.cs`: +90 lines (NEW — canonical pixel mapping)
- `BottleVisualMapping.cs.meta`: +11 lines (NEW)
- `BottleVisualMappingTests.cs`: +220 lines (NEW — 8 tests)
- `BottleVisualMappingTests.cs.meta`: +11 lines (NEW)

## Unchanged from 0.9.4
- HudSafeLayout.cs — IDENTICAL
- Camera/CanvasScaler settings — IDENTICAL
- Grid layout, cell sizes — IDENTICAL
- Bottle scaleX/scaleY per capacity tier — IDENTICAL
- Level generation and population — IDENTICAL
