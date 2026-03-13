# 3D Bottle Code Audit

Scope: full re-audit of bottle-visual code paths (mesh, shader, liquid, cork, shadow, neck/fill semantics, and layout probes) using repository-wide keyword search for `bottle`, `Bottle3D`, `BottleView`, `Glass`, `Liquid`, `Cork`, `Stopper`, `Shadow`, `Neck`, `Fill`, `Capacity`.

Notes:

- The keyword search produced many non-visual hits (gameplay/background/scoring files using generic words like `fill` and `capacity`).
- This audit document enumerates every file in the rendering pipeline and its direct verification harnesses.

## Rendering Pipeline Audit

| File path | Purpose | Relevant functions | Relationship to bottle visuals |
| --- | --- | --- | --- |
| `Assets/Decantra/Domain/Model/Bottle.cs` | Canonical bottle state and pour rules | `Capacity`, `Count`, `TopColor`, `IsSolvedBottle`, `CanPourInto`, `PourInto` | Source of truth for fill/capacity/completion state consumed by both 2D and 3D rendering. |
| `Assets/Decantra/Presentation/View3D/Bottle3DView.cs` | Main 3D bottle renderer and runtime layout diagnostics | `Render`, `ApplyCapacityRatio`, `ApplyLayerProperties`, `UpdateTopper`, `CheckLayoutSafety`, `WriteReport`, `WriteLayoutReport` | Creates glass/liquid/cork/shadow objects, drives shaders, computes overlap/cork metrics, writes layout reports. |
| `Assets/Decantra/Presentation/View3D/BottleMeshGenerator.cs` | Procedural bottle, liquid-layer, and cork mesh geometry | `GenerateBottleMesh`, `GenerateLiquidLayerMesh`, `GetStopperBaseY` | Defines bottle body/neck geometry, interior bounds, cork radius/height/insertion constants. |
| `Assets/Decantra/Presentation/View3D/PourStreamController.cs` | Visual pour stream and bubbles | `BeginPour`, `EndPour`, `RebuildStreamMesh` | Pour animation tied to bottle neck positions; affects visual liquid transfer readability. |
| `Assets/Decantra/Presentation/View3D/Shaders/BottleGlass.shader` | Glass shading and silhouette behavior | fragment logic for fresnel/specular/rim markers | Controls empty-bottle visibility and neck marker cues. |
| `Assets/Decantra/Presentation/View3D/Shaders/Liquid3D.shader` | Layered liquid rendering shader | layer selection, tilt/wobble/foam, cylindrical shading | Controls liquid fill visibility and thin-layer appearance. |
| `Assets/Decantra/Presentation/View3D/Shaders/CorkStopper.shader` | Cork visual appearance shader | procedural grain/pore + lighting | Controls cork material and completed-bottle visual identity. |
| `Assets/Decantra/Presentation/View/BottleView.cs` | Legacy 2D bottle renderer, still coupled to controller | `Render`, `ApplyCapacityScale`, `RenderSegment` | Defines the reference capacity/body-only scaling behavior mirrored by 3D system. |
| `Assets/Decantra/Presentation/View/BottleVisualMapping.cs` | 2D proportional mapping math | `ProportionalScaleY`, `LocalHeightForUnits` | Reference invariants for fill/height consistency across capacities. |
| `Assets/Decantra/Presentation/View/BottleInput.cs` | Pointer/drag integration for bottle interactions | `OnBeginDrag`, `OnDrag`, `OnEndDrag` | Applies 3D drag rotation and targeting, impacts perception of bottle geometry. |
| `Assets/Decantra/Presentation/Visual/Simulation/FillHeightMapper.cs` | Converts slots into visual layers | `Build`, `TotalFill`, `TopSurfaceFill` | Bridges logical fill state to shader-consumable layer bounds. |
| `Assets/Decantra/Presentation/Visual/Simulation/LiquidLayerData.cs` | Layer data model | constructor + properties | Per-layer color/fill bounds contract used by `Bottle3DView`. |
| `Assets/Decantra/Presentation/Visual/Simulation/SurfaceTiltCalculator.cs` | Deterministic surface tilt from rotation | `ComputeTiltDegrees` | Drives visual liquid surface angle when bottle rotates. |
| `Assets/Decantra/Presentation/Visual/Simulation/WobbleSolver.cs` | Deterministic slosh simulation | `ApplyImpulse`, `Step`, `TiltAngleDegrees` | Adds dynamic liquid motion used by liquid shader properties. |
| `Assets/Decantra/Presentation/Controller/GameController.cs` | Master orchestrator for state and rendering | `Render` and move/pour lifecycle methods | Calls both `BottleView` and `Bottle3DView`, wiring palette/state for visual updates. |
| `Assets/Decantra/Presentation/Runtime/RuntimeScreenshot.cs` | Runtime screenshot capture and visual metrics aggregation | `CaptureSceneWithEmptyBottles`, `CaptureLiquidReadabilityMetricsFromCurrentLevel`, `CaptureSilhouetteMetricsFromCurrentLevel`, `WriteVisualVerificationReport` | Produces screenshot evidence and merged metrics report (`layout-report.json`). |

## Verification Harness Audit

| File path | Purpose | Relevant functions | Relationship to bottle visuals |
| --- | --- | --- | --- |
| `Assets/Decantra/Tests/PlayMode/Layout/LayoutProbe.cs` | Captures layout metrics from scene | `Capture` | Quantifies bottle spacing/overlap in runtime layout. |
| `Assets/Decantra/Tests/PlayMode/Layout/LayoutMetricsProbePlayModeTests.cs` | Baseline regression checks | `CaptureLayoutMetricsJson`, `LayoutInvariants_MatchBaselineWithinTolerance` | Guards against layout regressions affecting bottle positioning. |
| `Assets/Decantra/Tests/PlayMode/BottleVisualConsistencyTests.cs` | Bottle fill/height consistency checks | multiple invariant tests | Validates capacity-proportional visual behavior in play mode. |
| `Assets/Decantra/Tests/PlayMode/RenderChecklistPlayModeTests.cs` | End-to-end render checklist | orientation/layout checklist tests | Verifies bottle/HUD positioning and visibility constraints. |

## Coupled Supporting File

| File path | Purpose | Relevant functions | Relationship to bottle visuals |
| --- | --- | --- | --- |
| `Assets/Decantra/Presentation/View/ColorPalette.cs` | Color mapping for bottle liquids | `GetColor` | Supplies both 2D/3D bottle and cork tint colors. |

## Initial Audit Conclusion

- The 3D bottle pipeline is centralized in `Bottle3DView` + `BottleMeshGenerator` + shaders, with runtime evidence generated by `RuntimeScreenshot`.
- Layout and cork metrics exist, but insertion-depth validation is not explicitly reported yet.
- Runtime verification is required before accepting any claim as fixed.
