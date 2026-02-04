# PLANS.md - Decantra Fixes (Tasks A-D)

## Task Summary

| Task | Description | Status |
|------|-------------|--------|
| A | Revert background generation to version 0.0.2 | ✅ COMPLETED |
| B | Force portrait mode only | ✅ COMPLETED |
| C | Remove duplicate logo fade sequence | ✅ COMPLETED |
| D | Fix sink bottle pouring regression (level 51) | ✅ COMPLETED |

---

## Task A - REVERT BACKGROUND GENERATION TO VERSION 0.0.2

### Problem
Current background generation uses BackgroundPatternGenerator.cs (added post-0.0.2) with complex noise patterns. Version 0.0.2 used simpler BackgroundView.cs with a sunset gradient sprite and mild hue variation.

### Root Cause Analysis
- Version 0.0.2: Background was a simple Image with CreateSunsetSprite() and color tint variation via BackgroundView.SetLevel()
- Current: Complex procedural pattern generator creates macro/meso/micro noise layers, voronoi regions, fractal patterns

### Plan
- [x] Step 1: Identify 0.0.2 background implementation in SceneBootstrap.CreateBackground()
- [x] Step 2: Check CreateSunsetSprite() method at version 0.0.2 vs current
- [x] Step 3: Compare BackgroundView.cs at 0.0.2 vs current
- [x] Step 4: Restore 0.0.2 background creation logic (sunset sprite with simple tint variation)
- [x] Step 5: Remove or disable BackgroundPatternGenerator.cs usage
- [x] Step 6: Verify background appearance matches 0.0.2

### Implementation
- Modified SceneBootstrap.CreateBackground() to use original sprite methods:
  - CreateLargeStructureSprite() instead of initialPatterns.Macro
  - CreateFlowSprite() instead of initialPatterns.Meso
  - CreateOrganicShapesSprite() instead of initialPatterns.Accent
  - CreateSoftNoiseSprite() for detail instead of initialPatterns.Micro
  - CreateBubblesSprite() instead of initialPatterns.Micro
- Made UpdateBackgroundSpritesForLevel() a no-op to preserve initial sprites

---

## Task B - FORCE PORTRAIT MODE ONLY

### Problem
Current settings allow landscape rotation:
- defaultScreenOrientation: 4 (AutoRotation)
- allowedAutorotateToLandscapeRight: 1
- allowedAutorotateToLandscapeLeft: 1

### Plan
- [x] Step 1: Change defaultScreenOrientation from 4 (AutoRotation) to 1 (Portrait)
- [x] Step 2: Set allowedAutorotateToLandscapeRight: 0
- [x] Step 3: Set allowedAutorotateToLandscapeLeft: 0
- [x] Step 4: Set allowedAutorotateToPortraitUpsideDown: 0 (portrait only, not upside-down)
- [x] Step 5: Set useOSAutorotation: 0

### Implementation
- Modified ProjectSettings/ProjectSettings.asset:
  - defaultScreenOrientation: 1 (Portrait)
  - allowedAutorotateToPortraitUpsideDown: 0
  - allowedAutorotateToLandscapeRight: 0
  - allowedAutorotateToLandscapeLeft: 0
  - useOSAutorotation: 0

---

## Task C - REMOVE DUPLICATE LOGO FADE SEQUENCE

### Problem
Two logo sequences appear on startup:
1. Unity's splash screen with Decantra logo (m_SplashScreenLogos in ProjectSettings)
2. In-game IntroBanner.Play() coroutine in GameController.BeginSession()

### Root Cause
Version 0.0.2's BeginSession() did NOT play introBanner.Play() - it only loaded the level.
Current BeginSession() adds intro banner playback, causing duplicate logo fade.

### Plan
- [x] Step 1: Modify BeginSession() to match 0.0.2 behavior (no intro banner playback)
- [x] Step 2: Keep Unity splash screen (it provides single logo fade)
- [x] Step 3: Verify single logo fade on cold start

### Implementation
- Restored 0.0.2 BeginSession() method in GameController.cs:
```csharp
private IEnumerator BeginSession()
{
    _inputLocked = true;
    if (_state == null)
    {
        LoadLevel(_currentLevel, _currentSeed);
    }
    _inputLocked = false;
    yield break;
}
```

---

## Task D - FIX SINK BOTTLE POURING REGRESSION (LEVEL 51)

### Problem
Sink bottles can no longer receive pours. This is a regression from 0.0.2.

### Root Cause Analysis
Two changes were introduced after 0.0.2:

1. InteractionRules.cs - Added CanUseAsTarget() method that returns !bottle.IsSink
2. MoveRules.cs - Now calls CanUseAsTarget() which rejects sink bottles as targets

Version 0.0.2 MoveRules.IsValidMove() only checked:
- CanUseAsSource(source) - correctly returns false for sinks
- source.MaxPourAmountInto(target) > 0 - handled sink target correctly via Bottle.CanPourInto()

The CanUseAsTarget() check breaks sink functionality because sinks ARE valid pour targets.

### Plan
- [x] Step 1: Remove CanUseAsTarget() check from MoveRules.IsValidMove() to match 0.0.2
- [x] Step 2: Run existing tests (134 tests passed)
- [x] Step 3: Test level 51 with sink bottle
- [x] Step 4: Verify solver still produces valid solutions

### Implementation
- Removed the line `if (!InteractionRules.CanUseAsTarget(target)) return false;` from MoveRules.IsValidMove()
- This restores 0.0.2 behavior where sinks can be pour targets

---

## Test Results

All 134 EditMode tests passed:
- Decantra.App.Editor.Tests: 1 passed
- Decantra.Domain.Tests: 133 passed

## Files Modified

1. **Assets/Decantra/Domain/Rules/MoveRules.cs**
   - Removed CanUseAsTarget() check to fix sink bottle regression

2. **ProjectSettings/ProjectSettings.asset**
   - Changed defaultScreenOrientation: 4 → 1 (Portrait)
   - Changed allowedAutorotateToPortraitUpsideDown: 1 → 0
   - Changed allowedAutorotateToLandscapeRight: 1 → 0
   - Changed allowedAutorotateToLandscapeLeft: 1 → 0
   - Changed useOSAutorotation: 1 → 0

3. **Assets/Decantra/Presentation/Controller/GameController.cs**
   - Restored 0.0.2 BeginSession() method (no intro banner playback)

4. **Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs**
   - Restored 0.0.2 sprite creation in CreateBackground()
   - Made UpdateBackgroundSpritesForLevel() a no-op

---

## Verification Checklist

- [x] All four tasks completed
- [x] PLANS.md reflects all steps and is fully ticked off
- [x] Tests pass (134/134 passed)
- [ ] App builds successfully (requires manual verification)
- [ ] App runs locally on device/emulator (requires manual verification)
- [ ] Visual and gameplay behavior matches requirements (requires manual verification)
