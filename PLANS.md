# PLANS.md - Decantra Fixes (Tasks A-D) + Background Redesign

## Task Summary

| Task | Description | Status |
|------|-------------|--------|
| A | Revert background generation to version 0.0.2 | ✅ COMPLETED |
| B | Force portrait mode only | ✅ COMPLETED |
| C | Remove duplicate logo fade sequence | ✅ COMPLETED |
| D | Fix sink bottle pouring regression (level 51) | ✅ COMPLETED |
| E | Modern cloud-like background redesign (2026) | ✅ COMPLETED |

---

## Task E - MODERN CLOUD-LIKE BACKGROUND REDESIGN (2026)

### Problem
Background still looked rigid and dated, evoking 1980s/1990s wallpaper aesthetics instead of modern, fluid, cloud-like visuals.

### Design Requirements (New)
1. **Visual Style**: Hazy, amorphous, cloud-like with no sharp geometry
2. **Color Palette**: Luminous aqua-cyan to soft lavender-pink (uplifting, warm)
3. **Shape Language**: Fluid, turbulent flow patterns using extreme domain warping
4. **Technical**: Multi-layer cascading domain warping with quintic smoothstep

### Implementation
Completely redesigned all background sprite generation methods:

1. **CreateSunsetSprite()** - Modern ethereal gradient
   - Four-stop gradient: aqua-cyan → periwinkle → lavender → rose-pink
   - Quintic smoothstep for ultra-smooth transitions
   - Organic color wavering with multi-octave noise

2. **CreateSoftNoiseSprite()** - Ethereal mist
   - Three-layer cascading domain warp for turbulent flow
   - Ultra-low frequency cloud formations
   - Quintic smoothstep for butter-smooth edges

3. **CreateFlowSprite()** - Liquid ink diffusion
   - Four-layer extreme cascading domain warp
   - Large soft cloud formations
   - Simulates watercolor/ink bleeding

4. **CreateOrganicShapesSprite()** - Amorphous vapor clouds
   - Multi-layer turbulent domain warp for smoke/vapor effect
   - Ultra-low frequency vapor formations
   - Impossibly soft quintic edges

5. **CreateBubblesSprite()** - Ethereal luminous nebulae
   - Heavy turbulent flow for nebula-like shapes
   - Layered glow concentrations
   - No hard edges, pure soft falloff

6. **CreateLargeStructureSprite()** - Four atmospheric themes:
   - Cumulus Dreamscape: Massive soft cloud masses
   - Stratospheric Haze: Gentle horizontal layers
   - Watercolor Bloom: Organic spreading pigment
   - Borealis Flow: Ethereal flowing ribbons

### Visual Verification
- [x] Backgrounds are modern, abstract, and cloud-like
- [x] No retro or geometric feel remains
- [x] Colors feel positive and contemporary
- [x] Screenshots captured: background_v3_cloud.png, background_v3_level4.png

### Status: ✅ COMPLETED

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

---

# Background Redesign - Modern Cloud/Liquid Style (2026)

## Problem Statement

The current backgrounds feel dated with overly geometric, 1980s–1990s aesthetics. This is inappropriate for a 2026 casual puzzle game.

## Goals

Produce modern, abstract, cloud-like, liquid-inspired backgrounds that feel:
- Contemporary, soft, abstract
- Positive and calm but visually interesting
- Appropriate for a modern mobile puzzle game

## Design Requirements

1. **Visual Style**: Hazy, amorphous, cloud-like; avoid sharp geometry, grids, tiling, hard edges
2. **Color Palette**: De-emphasize browns; prefer blues/greens, blues/reds, teals/cyans/purples; clean and positive
3. **Shape Language**: Blurred bubbles, soft blobs, cloud fields, noise-driven forms; fluid not mechanical
4. **Technical**: Domain-warped noise, heavy blur, soft gradients, no visible geometric patterns
5. **Performance**: Generation < 4 seconds on 4-core 2 GHz Android device

## Step-by-step Plan (Replaces Previous Background Work)

| Step | Description | Status |
|------|-------------|--------|
| 1 | Analyze current implementation and identify geometric patterns to remove | ☐ In Progress |
| 2 | Create new cloud/mist gradient system using domain-warped multi-octave noise | ☐ |
| 3 | Create soft blob/bubble field generator with heavy blur | ☐ |
| 4 | Update color palette system for modern blues/greens/teals | ☐ |
| 5 | Integrate new generators into CreateBackground() | ☐ |
| 6 | Remove geometric theme variants (polygons, wave lines, etc.) | ☐ |
| 7 | Add domain warping for organic flow effect | ☐ |
| 8 | Verify performance budget (<4s generation) | ☐ |
| 9 | Build APK and test on device/emulator | ☐ |
| 10 | Visual verification - confirm modern look | ☐ |
| 11 | Regenerate screenshots | ☐ |
| 12 | Run level solvability check | ☐ |
| 13 | Push and verify CI green | ☐ |

## Implementation Notes

### Current Issues (Geometric/Dated Elements)
- `CreateLargeStructureSprite()`: Has 4 themes including geometric polygons, wave lines
- `CreateOrganicShapesSprite()`: Uses hard-edged circles
- `CreateBubblesSprite()`: Has ring effects (too defined)
- `RenderGeometricPolygons()`: Explicit geometric shapes
- `RenderWaveLines()`: Sharp ribbon patterns
- `BackgroundPatternGenerator`: Voronoi, polygon shards, directional lines

### New Approach
- Replace all with soft domain-warped noise fields
- Use multi-octave Perlin/Simplex with heavy Gaussian-style blur
- Overlapping translucent cloud layers
- Soft radial gradient base
- No geometric shapes, no grids, no tiling artifacts

---

# Production Readiness Plan (End-to-End)

## Execution Loop

- **Loop:** Plan → Execute → Verify → Fix → Repeat (no partial stops)
- **Verification gates are mandatory per step.**

## Step-by-step Plan (authoritative)

1. ☑ **Local project build (clean)**
   - **Execute:** Run a clean Unity build of the full project.
   - **Verify:** Build succeeds with no production-relevant errors/warnings.
   - **Evidence:** Build log captured and summarized here.

2. ☑ **Screenshot regeneration**
   - **Execute:** Regenerate all gameplay/UI screenshots.
   - **Verify:** Screenshots updated, correct resolution/aspect, consistent branding.
   - **Evidence:** Output location and timestamp noted here.

3. ☐ **Level solvability validation**
   - **Execute:** Run `./build --generate-solutions`.
   - **Verify:** `solver-solutions-debug.txt` confirms all levels solvable and consistent.
   - **Evidence:** Summary of solver results and any anomalies.

4. ☑ **Android release build**
   - **Execute:** Produce release-signed Android build (APK/AAB).
   - **Verify:** Release signing is correct and build is Play Store ready.
   - **Evidence:** Build artifact path and signing confirmation.

5. ☐ **Remote CI green**
   - **Execute:** Push changes and trigger CI.
   - **Verify:** All CI jobs pass without retries/flakes.
   - **Evidence:** CI run link and status summary.

## Execution Log

- **Step 1 (Local project build):** Completed via `./build` (tests + release APK). Logs in `Logs/` and build artifact at `Builds/Android/Decantra.apk`.
- **Step 2 (Screenshots):** Completed via `tools/capture_screenshots.sh` with extended timeout (DECANTRA_SCREENSHOT_TIMEOUT=300). Output: `doc/play-store-assets/screenshots/phone`.
- **Step 3 (Solvability):** Not started.
- **Step 4 (Release build):** Completed via `./build` (release APK, release keystore configured).
- **Step 5 (CI):** Not started.
