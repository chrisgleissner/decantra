# PLANS

Last updated: 2026-03-01 UTC  
Execution engineer: GitHub Copilot (Claude Opus 4.6)

---

## Active Track — Layout Regression Fix (1.4.2-rc1 → 1.4.2-rc2)

### Root Cause

Commit `61f73f7` (tag 1.4.2-rc2) introduced two interacting changes to
`SceneBootstrap.cs` that together cause the playfield to overflow the screen:

1. **CanvasScaler `matchWidthOrHeight` changed from `0` (width) to `1` (height).**
   - rc1 set `ScaleWithScreenSize` with `referenceResolution = 1080×1920` and
     left `matchWidthOrHeight` at the Unity default of `0` (width matching).
   - rc2 explicitly set `matchWidthOrHeight = 1f` (height matching).
   - On a modern tall phone (e.g. 1080×2400, aspect 9:20), height matching
     yields `scaleFactor = 2400/1920 = 1.25`, shrinking the canvas logical
     width to `1080/1.25 = 864` while filling height at 1920.

2. **New `GameplayContainer` with `AspectRatioFitter` in `HeightControlsWidth` mode.**
   - The container is anchored full-height (0→1), and the fitter computes
     `width = canvasHeight × (9/16) = 1920 × 0.5625 = 1080`.
   - But the canvas logical width is only 864 → **container overflows by 25%.**

Combined effect:
- The gameplay rect is 1080 logical units wide on an 864-wide canvas →
  extends 108 units past each screen edge.
- All bottles, HUD, and margins are rendered at 125% of intended size.
- Outer border disappears; bottles overlap.

### Fix

| # | Change | File | Rationale |
|---|--------|------|-----------|
| 1 | `matchWidthOrHeight = 1f` → `0f` | `SceneBootstrap.cs` | Restore rc1 width-matching. Canvas logical width = 1080 always. Height varies with device. |
| 2 | `HeightControlsWidth` → `FitInParent` | `SceneBootstrap.cs` | Container never overflows. On portrait phones (9:20), container = 1080×1920 with vertical margins. On landscape web, container = portrait strip centered horizontally. |
| 3 | Container anchors `(0.5,0)-(0.5,1)` → `(0,0)-(1,1)` | `SceneBootstrap.cs` | `FitInParent` needs a fill-parent reference rect to compute fit. |
| 4 | Update test assertion `matchWidthOrHeight` from 1f → 0f | `ModalSystemPlayModeTests.cs` | Test must match new canvas configuration. |

### What Is Preserved

- **WebGL fullscreen** — `DecantraResponsive` template, CSS, and JS: untouched.
- **GameplayContainer / AspectRatioFitter architecture** — kept, just corrected.
- **`EnsureRuntimeCanvasConfiguration` / `EnsurePortraitGameplayContainers`** — kept.
- **Tutorial overlay, highlight shader, audio session** — untouched.

### Invariants Restored

| Invariant | Mechanism |
|-----------|-----------|
| Outer border exists | `FitInParent` + width-match → container ≤ canvas on all axes |
| No bottle overlap | Container width = 1080 (matches design); `HudSafeLayout` gap math valid again |
| Web fullscreen | WebGL template + JS unchanged |
| Android/iOS portrait | Width-match at 1080 ref = identical to rc1 |
| Web landscape | `FitInParent` produces centered portrait strip |

### Layout Math Proof

**Portrait phone 1080×2400 (9:20):**
- `scaleFactor = 1080/1080 = 1.0`. Canvas = 1080×2400.
- `FitInParent` at 9:16: fit by width → 1080×1920. Vertical margin = 240 px each side.

**Portrait phone 1080×1920 (9:16):**
- Canvas = 1080×1920. Container = 1080×1920. Exact fit.

**Web landscape 1920×1080:**
- `scaleFactor = 1920/1080 ≈ 1.778`. Canvas = 1080×607.
- `FitInParent` at 9:16: fit by height → 341×607. Horizontal margin ≈ 370 each side.
- Portrait gameplay strip centered. Physical size ≈ 607×1080 px.

**Web portrait 1080×1920:**
- Identical to phone 9:16 case.

### Test Matrix

| Platform | Window | Levels | Status |
|----------|--------|--------|--------|
| Android emulator | portrait | 1, 506, 1000, tutorial | pending |
| iOS simulator | portrait | 1, 506, 1000, tutorial | pending |
| Web portrait | browser | 1, 506 | pending |
| Web landscape wide | browser | 1, 506 | pending |
| Web narrow resize | browser | 1, 506 | pending |
| Web fullscreen | browser | 1, 506 | pending |

### Rollback Criteria

Revert the two SceneBootstrap changes if:
- Any test failure in EditMode or PlayMode suite.
- Bottle overlap observed on any tested level/platform combination.
- Web fullscreen ceases to function.

### Risk Register

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `FitInParent` causes portrait gameplay to be smaller than rc1 | Low — width-match keeps 1080 baseline identical | Verified via math proof above |
| HUD elements misaligned in GameplayContainer | Low — HUD uses relative anchors within container | Existing `HudSafeLayout` tests validate gaps |
| Web landscape gameplay too narrow | Medium — 341 logical width is small | Expected per spec: "centered portrait-style region" |
| `AspectRatioFitter.FitInParent` anchor override conflicts | Low | Unity FitInParent is stable; no manual anchor manipulation after setup |

### Files Modified

- `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs` — canvas scaler + container fix.
- `Assets/Decantra/Tests/PlayMode/ModalSystemPlayModeTests.cs` — test assertion update.
- `doc/research/layout-regression-1.4.2/report.md` — root cause documentation.
- `PLANS.md` — this file.

### Definition of Done

- [x] Root cause identified and documented.
- [ ] Fix implemented in SceneBootstrap.cs.
- [ ] Test assertion updated.
- [ ] EditMode tests pass.
- [ ] Root cause report written.
- [ ] All changes committed.

---

## Completed Track — Cinematic Level Transition Upgrade

Moved to completed. See git history for details.

---

## Completed Track — iOS Production Issues

### Summary (completed 2026-02-28)

- iOS display name fixed: `Decantra` enforced in build + plist.
- iOS audio fixed: `AVAudioSession` configured at startup/focus/unpause.
- EditMode guardrail tests added.
- Remaining: device verification (outside Linux workspace scope).
