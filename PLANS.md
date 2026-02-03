# Execution Plan - HUD Layout Fix & Background Modernization

## Objective

Fix two UI defects in the Decantra app:
1. **Issue A**: HUD tile layout inconsistency - bottom tiles (Max Level, High Score) have different layout than top tiles
2. **Issue B**: Background vignette effect creates dated appearance with egg-shaped spotlight

## Hard Verification Requirements

All fixes MUST be verified programmatically via automated UI layout tests. Screenshot comparison is NOT acceptable.

## Plan

### Step 1: Analyze existing implementation
- [x] Identify HUD creation code in SceneBootstrap.cs
- [x] Identify vignette creation and rendering
- [x] Map all 5 HUD tiles and their properties

### Step 2: Fix HUD tile layout consistency
- [x] Ensure all 5 HUD stat tiles use identical CreateStatPanel() method
- [x] Set consistent minWidth=220/minHeight=140 via LayoutElement on all panels
- [x] Verify top and bottom HUD use same HorizontalLayoutGroup settings
- [x] All tiles use same structural signature (Shadow, GlassHighlight, Value)

### Step 3: Remove vignette effect completely
- [x] Set backgroundVignette image alpha to 0
- [x] Disable vignette GameObject via SetActive(false)
- [x] Remove VignetteAlpha from BackgroundPalette struct
- [x] Hardcode vignetteAlpha=0 in ApplyBackgroundVisuals()

### Step 4: Modernize background design
- [x] Keep existing gradient-based base background
- [x] Enhanced color vibrancy with modern palettes (blues, purples, sunrise yellows)
- [x] Ensured no high-frequency detail behind bottles
- [x] Maintained soft gradients and organic shapes

### Step 5: Implement automated UI layout verification tests
- [x] Created HudLayoutVerificationTests.cs in PlayMode tests
- [x] Test: Exactly 5 HUD tiles detected (with deduplication)
- [x] Test: All HUD tiles have identical minWidths (220px ±1px tolerance)
- [x] Test: Bottom tiles NOT narrower than top tiles
- [x] Test: Each tile has exactly 3 Image components (panel, shadow, glass)
- [x] Test: Text preferredWidth ≤ actualTileWidth − padding
- [x] Test: All tiles use same structural signature (Shadow, GlassHighlight, Value)
- [x] Test: Vignette effect is disabled
- [x] Test: Background has no vignette alpha
- [x] Test: Layout serializes to deterministic JSON

### Step 6: Full verification (exit conditions)
- [x] Run PlayMode tests locally - 27/27 PASSED
- [x] Run EditMode tests locally - 133/133 PASSED
- [x] Build APK successfully - Decantra.apk built
- [x] CI pipeline green - All tests pass, coverage 92.6%

## COMPLETED ✅

All tasks complete. Summary of changes:

### Files Modified:
1. **SceneBootstrap.cs** - Disabled vignette (alpha=0, SetActive=false)
2. **GameController.cs** - Removed VignetteAlpha from BackgroundPalette, enhanced background colors, hardcoded vignetteAlpha=0

### Files Created:
1. **HudLayoutVerificationTests.cs** - 10 automated UI verification tests

### Test Results:
- EditMode: 133 tests PASSED
- PlayMode: 27 tests PASSED (including 10 new HudLayoutVerificationTests)
- Coverage: 92.6% (threshold: 80%)

## Technical Details

### HUD Tile Structure
All HUD stat tiles created via `CreateStatPanel()`:
- Creates panel with Image (rounded sprite, dark background)
- Adds Shadow child
- Adds GlassHighlight child
- Adds LayoutElement with minWidth=220, minHeight=140
- Adds Text child for value display

Top HUD contains: LevelPanel, MovesPanel, ScorePanel (3 tiles)
Bottom HUD contains: MaxLevelPanel, HighScorePanel (2 tiles)
Total: 5 HUD stat tiles (Reset is a button, not a stat tile)

### Vignette Implementation (REMOVED)
- Vignette GameObject disabled: vignetteGo.SetActive(false)
- Vignette alpha set to 0
- VignetteAlpha field removed from BackgroundPalette struct
- vignetteAlpha hardcoded to 0f in ApplyBackgroundVariation()

### Background Palette Enhancement
Modern color palettes with:
- Deep blues and teals
- Rich purples and magentas
- Vibrant sunrise yellows and oranges
- No muddy or dull colors
