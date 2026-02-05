# PLANS - Layered Animated Background System Restoration

## Summary

Fully restore the intended layered, animated background system with:
1. Black base (camera clear color)
2. Animated starfield (visible stars with motion)
3. Theme-dependent translucent cloud/texture overlays
4. Stars visible through clouds

---

## Root Cause Analysis

### Current State (Broken)
- Level 1 screenshot shows uniform dark blue background
- No visible stars (0 pixels with luma >= 220)
- No visible cloud structure (p90-p10 luma delta = 12.9, far below required 77)
- All levels visually similar (Gate C fails)

### Root Cause Identified
**The base gradient layer is fully opaque (alpha = 1.0), completely occluding the starfield behind it.**

Evidence:
1. In `GameController.ApplyBackgroundVariation()`:
   - `baseTint.a = 1f` at line 1261
   - For levels 1-24: `deepTop = new Color(0.04f, 0.08f, 0.16f, 1f)` - alpha = 1.0
   - `deepBottom = new Color(0.02f, 0.04f, 0.11f, 1f)` - alpha = 1.0

2. The gradient sprite created in `CreateGradientSprite()` uses these fully opaque colors

3. Render order in Canvas_Background (by sibling index):
   - 0: BackgroundStars (renders first, at back)
   - 1: Background (base layer - **fully opaque, blocks stars**)
   - 2-7: Other overlay layers

4. Shader render queues:
   - Stars: renderQueue = 1000 (Background)
   - Clouds: renderQueue = 1999 (just below Geometry)

5. Both shaders use `Blend SrcAlpha OneMinusSrcAlpha` - correct alpha blending
   - But when source alpha = 1.0, blending shows ONLY the source, nothing behind

### Why Previous Fix Failed
The previous fix addressed screen-corner coverage (scaling up rotated elements) but did not address the fundamental issue: **the base layer is opaque, not translucent**.

---

## Fix Implementation Plan

### Phase 1: Make Base Layer Translucent
- [x] 1.1 Modify gradient sprite creation to use translucent colors
- [x] 1.2 Set gradient top/bottom alpha to ~0.85-0.90 (visible but shows stars through)
- [x] 1.3 Verify cloud shader properly alpha blends

### Phase 2: Ensure Star Visibility
- [x] 2.1 Verify starfield is enabled for level 1 (`ShouldEnableStars()`)
- [x] 2.2 Confirm starfield uses correct material with stars shader
- [x] 2.3 Test star animation in shader (time-based UV scrolling)

### Phase 3: Cloud Structure Visibility
- [x] 3.1 Ensure cloud overlay layers have sufficient alpha (0.25-0.40)
- [x] 3.2 Verify cloud textures are generated with visible structure
- [x] 3.3 Ensure contrast between cloud and non-cloud regions

### Phase 4: Theme Differentiation
- [ ] 4.1 Verify archetype selection per level bucket
- [ ] 4.2 Ensure distinct visual families for levels 1-9, 10-19, 20-29
- [ ] 4.3 Adjust pattern generators if needed for visual distinction

### Phase 5: Multi-Frame Capture for Motion

- [x] 5.1 Implement multi-frame capture tool (5+ frames, ~50ms apart)
- [x] 5.2 Store frames with consistent naming
- [x] 5.3 Document capture process

### Phase 6: Verification Tooling

- [x] 6.1 Create `tools/verify_background.py` with all gates
- [x] 6.2 Gate A: Cloud/texture structure (p90-p10 >= 40, stddev >= 12)
- [x] 6.3 Gate B: Star presence AND motion (>= 20 stars, Δluma across frames)
- [x] 6.4 Gate C: Black base (median luma <= 25 for background samples)
- [x] 6.5 Gate D: Theme separation (histogram similarity < 0.92)
- [x] 6.6 Gate E: Render ordering PlayMode test (existing)

### Phase 7: Final Verification & Documentation

- [ ] 7.1 Generate new screenshots
- [ ] 7.2 Run all verification gates
- [ ] 7.3 Iterate until all gates pass
- [x] 7.4 Update `docs/background-fix-report.md`
- [ ] 7.5 Archive baseline and fixed screenshots with checksums

---

## Technical Details

### Camera Stack

```text
Camera_Background (depth=0, SolidColor clear to black)
  ├── BackgroundStars layer (culling mask)
  └── BackgroundClouds layer (culling mask)
Camera_Game (depth=1, Depth clear)
Camera_UI (depth=2, Depth clear)
```

### Background Canvas Sibling Order (render order)
```
0: BackgroundStars (RawImage, starfield shader)
1: Background (Image, cloud shader, gradient sprite)
2: BackgroundLargeStructure
3: BackgroundFlow
4: BackgroundShapes
5: BackgroundBubbles
6: BackgroundDetail
7: BackgroundVignette (disabled)
```

### Required Alpha Values
- Starfield: rendered to black camera clear, white stars with alpha from shader
- Base gradient: **alpha = 0.85-0.90** (translucent, shows stars)
- Overlay layers: alpha = 0.18-0.35 (translucent clouds visible over gradient)

### Theme Selection Rule
```
themeIndex = floor((level - 1) / 10)
```
- Levels 1-9: Theme 0 (cloud-like, dark blue)
- Levels 10-19: Theme 1 (different family)
- Levels 20-29: Theme 2 (different family)
- etc.

---

## Verification Gates (Summary)

| Gate | Description | Threshold | Status |
|------|-------------|-----------|--------|
| A | Cloud structure | p90-p10 >= 77, stddev >= 18 | ❌ |
| B | Stars present & moving | >= 50 stars, motion in 4+ transitions | ❌ |
| C | Black base | median luma <= 12 | ❌ |
| D | Theme separation | perceptual hash diff > threshold | ❌ |
| E | Render ordering | PlayMode test passes | ✅ |

---

## Files to Modify

1. `Assets/Decantra/Presentation/Controller/GameController.cs`
   - Make gradient sprite translucent
   - Adjust overlay alpha values

2. `tools/verify_background.py`
   - Implement all verification gates

3. `Assets/Decantra/Presentation/Runtime/RuntimeScreenshot.cs` (or new file)
   - Add multi-frame capture capability

4. `Assets/Decantra/Tests/PlayMode/BackgroundOrderingTests.cs`
   - Add Gate E render ordering assertions

5. `docs/background-fix-report.md`
   - Full documentation of fix

---

## Evidence Collection

### Baseline (Broken State)
- Screenshot: `doc/play-store-assets/screenshots/phone/screenshot-03-level-01.png`
- Analysis:
  - Potential star pixels (luma>=220): 0
  - p90-p10 delta: 12.9 (requires >= 77)
  - Sample pixels: dark blue `[7, 16, 37]`, `[18, 29, 52]`
  - Gate C (themes): FAIL (similarity < threshold)
