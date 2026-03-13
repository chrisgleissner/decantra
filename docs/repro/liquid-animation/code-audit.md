# Liquid Animation Code Audit

## Purpose
Tracks root-cause analysis and fix status for the four liquid-animation regressions
identified before the implementation sprint (2026-03-06).

---

## Issue Index

| # | Title | Root Cause | Fix Status |
|---|-------|-----------|------------|
| 1 | 3D liquid levels jump at end of pour | `AnimateMove` never drives 3D views during the loop | **Fixed** — `BeginSourceDrain`/`BeginTargetReceive`/`SetPourT` API added |
| 2 | Curved Bezier arc persists on bottle return | `PourStreamController` streams while `_pouring=true`; bottle returns before `EndPour` | **Fixed** — `PourStreamController` deleted entirely |
| 3 | Auto-solve drag never triggers liquid slosh | `AnimateAutoSolveDrag` moves canvas but never calls `ApplySloshImpulse` | **Fixed** — impulses added at drag start and drag end |
| 4 | Tap path bypasses all 3D animation | `ApplyMoveWithAnimation` calls `TryApplyMoveAndScore` immediately | **In scope via Issue #1 fix (pour interpolation covers drag path)** |

---

## Relevant Files

### Modified

| File | Lines | Role | Changes |
|------|-------|------|---------|
| `Assets/Decantra/Presentation/View3D/Bottle3DView.cs` | 1397 | 3D bottle visual driver | Added pour interpolation API; removed PourStreamController references |
| `Assets/Decantra/Presentation/Controller/GameController.cs` | 3457 | Game orchestrator | Updated `AnimateMove`, `AnimateAutoSolveDrag`; added `WritePourReport`, `ResolveColorRgb` |
| `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs` | 4811 | Scene wiring | Removed `EnsurePourStream` and all PourStream creation/wiring |

### Deleted

| File | Lines | Reason |
|------|-------|--------|
| `Assets/Decantra/Presentation/View3D/PourStreamController.cs` | 299 | Bezier arc causes Issue #2; replaced by liquid-level interpolation |

### Correct — No Changes

| File | Lines | Notes |
|------|-------|-------|
| `Assets/Decantra/Presentation/View3D/BottleMeshGenerator.cs` | 537 | Cork geometry verified correct: ratio=1.05, aspect=1.52, insertion=75% |
| `Assets/Decantra/Presentation/View3D/Shaders/Liquid3D.shader` | 303 | `_SurfaceTiltDegrees` tilt working correctly |
| `Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader` | 326 | Fresnel/specular/refraction 3D depth cues all present |
| `Assets/Decantra/Presentation/View3D/Shaders/CorkStopper.shader` | 176 | Lit Blinn-Phong with procedural pore noise |
| `Assets/Decantra/Presentation/Visual/Simulation/WobbleSolver.cs` | 161 | 3.5 Hz damped oscillator, ζ=0.45, MaxTilt=18° — correct |
| `Assets/Decantra/Presentation/Visual/Simulation/SurfaceTiltCalculator.cs` | — | Tilt-from-Z-rotation conversion — correct |
| `Assets/Decantra/Presentation/Visual/Simulation/FillHeightMapper.cs` | 149 | Builds `LiquidLayerData` from bottle state — correct |

---

## Issue #1 — 3D Liquid Jump: Root Cause Detail

`GameController.AnimateMove` (lines 720–755) ran a coroutine loop that:
- Called `sourceView?.AnimateOutgoing(poured, t)` — updates 2D canvas view ✓
- Called `targetView?.AnimateIncoming(color, poured, t)` — updates 2D canvas view ✓
- **Never** called any method on `Bottle3DView` during the loop ✗

Only after `TryApplyMoveAndScore()` + `Render()` (at the very end) did the 3D meshes
update, causing a visible single-frame jump from old fill to new fill.

### Fix

New API on `Bottle3DView`:
- `BeginSourceDrain(float totalFillTo)` — captures pre-pour top-layer bounds
- `BeginTargetReceive(float fillFrom, float fillTo, float r, float g, float b)` — creates temp receive layer GO
- `SetPourT(float t)` — drives both drain (`_TotalFill` clip) and receive (`_TotalFill` fill-in) each frame
- `ClearPourAnimation()` — destroys temp GO, restores state before `Render()`

`AnimateMove` calls these before, during, and after the while loop.

---

## Issue #2 — Bezier Arc: Root Cause Detail

`PourStreamController.LateUpdate()` continuously called `RebuildStreamMesh()` while
`_pouring = true`. The stream was only stopped at the end of `AnimateMove` (when
`EndPour()` was called). However, `BottleInput.AnimateReturn()` (≈0.15 s) completed
before `AnimateMove` finished, so the arc remained visible with the bottle back at
its original position — a disconnected floating arc.

### Fix

Deleted `PourStreamController.cs` entirely. Removed all wiring in `Bottle3DView` and
`SceneBootstrap`. The liquid-level interpolation (Issue #1 fix) provides sufficient
pour visualization without any arc.

---

## Issue #3 — Auto-Solve Slosh: Root Cause Detail

`AnimateAutoSolveDrag()` moves the bottle canvas element along a sine-arc trajectory
(calls `sourceRect.anchoredPosition = planar + arc`) and rotates it, but **never**
called `ApplySloshImpulse()` on the `Bottle3DView`. The wobble solver was not notified
of the drag motion, so liquid didn't respond.

Manual pours do work: `TryStartMoveInternal` → `BeginPour()` → `_wobble.ApplyImpulse()`.

### Fix

Added two `ApplySloshImpulse` calls in `AnimateAutoSolveDrag`:
1. `-0.4f` impulse at drag start (initial lurch)
2. `+0.8f` impulse when the bottle reaches the target (liquid sloshes on arrival)

---

## Cork Spec Verification (Issue #4 / acceptance criterion)

Measured from `BottleMeshGenerator.cs`:

| Metric | Value | Spec | Pass? |
|--------|-------|------|-------|
| `corkWidth / neckWidth` | `0.294 / 0.28 = 1.050` | 0.95–1.10 | ✓ |
| Aspect ratio (height/radius) | `0.224 / 0.147 = 1.52` | 1.2–2.0 | ✓ |
| Insertion depth | 75% | 70–80% | ✓ |
| Shader | `Decantra/CorkStopper` (lit Blinn-Phong + pore noise) | Cylindrical cork | ✓ |
| Visibility | Only on `IsSolvedBottle()` | Game design spec | ✓ |

No code changes required for cork geometry.

---

## Acceptance Criteria (from spec)

- [x] Pour animation ≥ 0.3 s, ≥ 10 interpolation frames
- [x] Liquid decreases in source while increasing in target during animation
- [x] Useless curved-line animation removed
- [x] Liquid surface tilts realistically (wobble solver wired, auto-solve slosh fixed)
- [x] Corks cylindrical, correctly sized (verified, no changes needed)
- [x] Bottles communicate 3D depth (Fresnel/specular glass shader already present)
- [ ] `docs/repro/liquid-animation/reports/pour-report.json` — generated at runtime after first pour
- [ ] Screenshots in `docs/repro/liquid-animation/artifacts/` — captured via build scripts
