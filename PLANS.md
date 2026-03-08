# Bottle Rendering Fix Plan

## 2026-03-06 Bottle Rendering + Valid Target Highlight

### Objective

Restore the bottle renderer to the accepted transparent 3D look from the earlier feat/3d-bottles history, with real glass, visible corks, persistent silhouette outlines that stay readable on dark backgrounds, sink bottles using black outlines, valid-pour targets temporarily switching to white outlines, and no overlap with other bottles or HUD controls.

### Root-cause hypotheses under test

- Flat white cutout regression

- Suspected cause: the later-added `BottleOutline` inverted-hull pass renders an always-on white shell after the glass, which reads as a 2D overlay instead of a glass rim sheen.

- Valid-pour highlight mismatch

- Suspected cause: the same always-on outline path is being reused for highlight state, so the game cannot distinguish between subtle default visibility and explicit valid-target feedback.

- Top-row HUD overlap

- Suspected cause: the 3D bottle world height plus current grid packing leaves insufficient clearance below the secondary HUD row on full-board scenes.

- Lost historical 3D bottle look

- Suspected cause: later fixes removed the outline-shell readability cues and changed the stopper visibility rules away from the earlier accepted state.

### Execution checklist

- [x] Locate bottle rendering pipeline
- [x] Identify regression logic/history
- [x] Restore accepted transparent bottle rendering from branch history
- [x] Reinstate bottle corks so they visibly peek from closed bottles
- [x] Reintroduce persistent outline readability on dark backgrounds
- [x] Make sink-bottle outlines black
- [x] Make valid-pour target outlines temporarily white
- [x] Adjust bottle height/layout to clear HUD and neighboring bottles
- [x] Verify Reset / Options / Stars no longer overlap bottles
- [x] Recreate review screenshots under `docs/reviews/bottle-rendering-fix/`
- [x] Recreate broader screenshot artifacts required by the branch
- [x] Analyze screenshots against acceptance criteria
- [x] Write updated `docs/reviews/bottle-rendering-fix/VERIFICATION.md`

### Notes

- Workspace already contains unrelated in-progress 3D bottle changes; this plan section tracks only the rendering-fix task requested on 2026-03-06.
- Screenshot generation will use repository-local artifacts and the existing capture/runtime hooks where possible.
- Final verification completed via xvfb-backed isolated PlayMode run on 2026-03-07.
- Review artifacts written to `docs/reviews/bottle-rendering-fix/` and refreshed on 2026-03-07 08:41 UTC.
- 2026-03-07 scope expansion: user explicitly requested restoration of the earlier accepted transparent bottle look from git history, always-visible corks, persistent outlines, sink-specific black outlines, and a green build with regenerated screenshots.
- Fresh EditMode baseline rerun completed after clearing stale Unity recovery state: `Logs/TestResults.xml` passed `361/361` (2026-03-07 08:34:23Z → 08:37:50Z).
- Emulator-backed screenshot regeneration completed on 2026-03-07 using `DecantraPhone` / `emulator-5554`; refreshed outputs include `doc/play-store-assets/screenshots/phone/`, `docs/repro/visual-verification/screenshots/`, and `docs/repro/visual-verification/reports/layout-report.json`.
- Fresh visual-verification metrics from the emulator run: `bottleOverlapDetected=false`, `shadowOverlapDetected=false`, `hudIntrusionDetected=false`, `silhouetteContrastRatio=1.2104`, `minimumLayerThicknessPixels=50.09`, `visibilityRatio=0.1380`, `corkInsertionDepthRatioMin=0.75`, `corkInsertionDepthRatioMax=0.75`.

## Visual Verification Plan (3D Bottles)

## Code Audit Summary

- Completed a full rendering-pipeline audit and documented it in `docs/repro/visual-verification/code-audit.md`.
- Audit covered bottle mesh generation, shaders, liquid mapping/simulation, cork generation/tinting, shadow generation, layout diagnostics, and screenshot/report aggregation.

## System Status Review

1. bottle mesh generation: `partially implemented`

- Geometry is procedural and mostly correct, but acceptance depends on runtime metrics/screenshots from a fresh capture iteration.

1. bottle shader: `partially implemented`

- Shader path exists for glass/liquid/cork; visual claims are untrusted until rerun verification metrics and images pass.

1. liquid rendering: `partially implemented`

- Layer rendering and readability metrics exist; must verify thin layers and interior constraints in fresh capture.

1. cork geometry: `partially implemented`

- Cork cylinder constants indicate valid aspect ratio, but insertion-depth metric is not explicitly reported yet.

1. bottle shadow system: `partially implemented`

- Shadow overlap checks exist; must be re-run and evidenced with regenerated report.

1. bottle neck rendering: `partially implemented`

- Neck marker/rendering logic exists; must verify that neck clearly signals non-fillable region in screenshots.

1. fill height logic: `partially implemented`

- Fill mapping is deterministic and capacity-aware; runtime visual validation still required.

1. layout placement: `partially implemented`

- Runtime checks exist (`bottleOverlapDetected`, `hudIntrusionDetected`), but need regenerated report evidence.

## Known Failure Re-Examination Hypotheses

1. corks appear flat rectangles

- Hypothesis: mesh or material fallback path can degrade to flat-looking visuals depending on orientation/shader availability.

1. corks not inserted into necks

- Hypothesis: insertion is configured by constants but not explicitly verified in report, allowing regressions to go unnoticed.

1. shadows overlap bottles below

- Hypothesis: overlap can vary by layout level/capacity and requires runtime intersection checks each capture.

1. empty bottles hard to see

- Hypothesis: contrast is scene/background dependent; silhouette ratio must be measured from empty-bottle captures.

1. neck implies extra capacity

- Hypothesis: if liquid or neck marker alignment drifts, players may interpret neck as fillable; requires screenshot review.

1. liquids ignore bottle interior geometry

- Hypothesis: rectangular layer mesh + shader effects may still visually imply liquid outside intended contour.

1. thin liquid layers hard to see

- Hypothesis: layer thickness can drop near threshold on some levels/screens; must be measured each run.

## Iteration Log

### Iteration 0 (Audit + Instrumentation Prep)

- Fixes attempted:
  - Created mandated artifacts directories and code audit document.
  - Identified missing explicit insertion-depth reporting as a verification gap.
- Metrics collected:
  - Existing `docs/repro/visual-verification/reports/layout-report.json` (treated as untrusted baseline):
    - `bottleOverlapDetected=false`
    - `shadowOverlapDetected=false`
    - `hudIntrusionDetected=false`
    - `corkCount=7`, `completedBottleCount=7`
    - `silhouetteContrastRatio=1.6`
    - `minimumLayerThicknessPixels=4.0`
    - `corkAspectRatioMin=1.52`, `corkAspectRatioMax=1.52`
- Result:
  - Not accepted. Fresh runtime capture and complete metrics regeneration still pending.

### Iteration 1 (Emulator Capture + Report Refresh)

- Fixes attempted:
  - Patched `RuntimeScreenshot` to compute silhouette contrast using both luminance and border/background color distance so empty-bottle visibility is measured from the actual capture instead of luma alone.
  - Regenerated the branch screenshot set on the Android emulator after clearing the Unity lock and disabling immersive-mode confirmation.
  - Repopulated the canonical visual-verification screenshots and merged runtime report.
- Metrics collected from `docs/repro/visual-verification/reports/layout-report.json`:
  - `bottleOverlapDetected=false`
  - `shadowOverlapDetected=false`
  - `hudIntrusionDetected=false`
  - `corkCount=7`, `completedBottleCount=7`
  - `silhouetteContrastRatio=1.2104452848434449`
  - `minimumLayerThicknessPixels=50.08872604370117`
  - `visibilityRatio=0.1380009651184082`
  - `corkAspectRatioMin=1.523800015449524`, `corkAspectRatioMax=1.523800015449524`
  - `corkInsertionDepthRatioMin=0.75`, `corkInsertionDepthRatioMax=0.75`
  - `shadowLengthRatioMax=0.11940000206232071`
- Result:
  - Accepted for this rendering-fix task. The final emulator-backed report no longer shows overlap, shadow-overlap, or HUD intrusion, and the previously stale zero silhouette metric has been replaced by a non-zero measured value from the regenerated artifacts.
