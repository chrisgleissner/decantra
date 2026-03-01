# PLANS

Last updated: 2026-03-01 UTC  
Execution engineer: GitHub Copilot (GPT-5.3-Codex)

## 1) Scope

Restore gameplay layout geometry regression introduced between tags `1.4.1` and `1.4.2-rc3` while preserving Web fullscreen behavior.

In scope:
- Unity-native geometry measurement using `RectTransform.GetWorldCorners()` only.
- Deterministic baseline-vs-candidate metric capture and JSON artifacts.
- Root-cause line identification via `git diff 1.4.1 1.4.2-rc3`.
- Minimal code fix isolating background scaling from gameplay geometry.
- PlayMode regression guard asserting numeric invariants.
- Screenshot regeneration and local NCI run.

Out of scope:
- Gameplay logic changes.
- Unrelated UI refactors.
- Prefab-wide redesign.

## 2) Reference invariants from 1.4.1

Reference baseline tag: `1.4.1`.

Measured invariants (canvas-local and normalized):
- Logo vertical placement (`TopY`, `BottomY`, `CenterX`).
- Bottle cap `TopY` for rows 1/2/3.
- Bottom bottle bottom edge `BottomY`.
- Bottle center `CenterX` for left/middle/right columns.
- Row spacing: `row1TopY-row2TopY`, `row2TopY-row3TopY`.
- Column spacing: `centerX(mid)-centerX(left)`, `centerX(right)-centerX(mid)`.
- Normalized ratios: `ratioY = y/canvasHeight` and `ratioX = x/canvasWidth`.

## 3) Measurement strategy

Create temporary `LayoutProbe : MonoBehaviour` used by PlayMode tests.

Probe behavior:
- Locate key rects (`BrandLockup`/logo and bottle row/column references).
- Capture corners via `GetWorldCorners()`.
- Convert world to canvas-local coordinates through target canvas transform.
- Compute TopY/BottomY/CenterX, spacing deltas, and normalized ratios.
- Serialize full metrics to `Artifacts/layout/layout-metrics.json`.

Comparison outputs:
- `Artifacts/layout/layout-metrics-1.4.1.json`
- `Artifacts/layout/layout-metrics-1.4.2-rc3.json`
- `Artifacts/layout/layout-metrics-current.json`
- `Artifacts/layout/layout-metrics-compare.md` with
  `Element | 1.4.1 | 1.4.2-rc3/current | Delta | Delta %`

## 4) Diff analysis plan

Run and inspect:
- `git diff 1.4.1 1.4.2-rc3 -- Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs`
- `git diff 1.4.1 1.4.2-rc3 -- Assets/Decantra/Presentation/View/HudSafeLayout.cs`
- Search for `CanvasScaler`, `referenceResolution`, `matchWidthOrHeight`, safe-area logic,
  camera viewport settings, and any screen-size/aspect compensation.

Deliverable:
- Exact line-level root cause references (no speculation).

## 5) Fix strategy

Strict layer separation:

A) Background layer
- Can fill full viewport and stretch as needed.
- Must not alter gameplay transform hierarchy geometry.

B) Gameplay layer
- Fixed reference geometry.
- No dynamic vertical scaling tied to runtime screen height.
- No aspect-ratio compression of bottle rows.
- Constant row/column spacing across Android/iOS/Web portrait.
- Web landscape: centered gameplay region with unchanged vertical spacing.

Implementation constraints:
- Minimal diff only.
- No unrelated refactors.
- No prefab resizing operations beyond required runtime geometry fix.

## 6) Verification plan

1. Run probe on `1.4.1` (baseline) and `1.4.2-rc3` (regressed).
2. Apply minimal fix on current branch.
3. Re-run probe on current branch.
4. Compare against baseline with thresholds:
   - absolute delta <= 1px
   - normalized ratio delta <= 0.001
5. Add/execute automated PlayMode invariant test assertions.
6. Regenerate screenshots using existing pipeline.
7. Run local NCI (tests + build path already used in repo).

## 7) Regression guard strategy

- Add PlayMode test that fails if:
  - row spacing deviates from baseline above tolerance,
  - logo Y ratio drifts above tolerance,
  - bottom row bottom edge ratio drifts above tolerance,
  - column spacing deviates above tolerance,
  - any bottle top/bottom overlap is detected in measured rows.
- Keep baseline values in checked-in test fixture JSON for deterministic checks.

## 8) Completion criteria

All must be true before stop:
- `layout-metrics-current.json` matches `1.4.1` within tolerance.
- No gameplay transform path uses dynamic vertical scaling.
- Background scaling is isolated to background layer behavior.
- Android portrait verified.
- iOS portrait verified (or explicit local environment limitation documented).
- Web portrait verified.
- Web landscape verified.
- Screenshots regenerated.
- Local NCI is green.
- This `PLANS.md` updated with final outcomes and measured deltas.

## 9) Final execution status (2026-03-01)

Completed:
- Baseline and comparison artifacts generated:
  - `Artifacts/layout/layout-metrics-1.4.1.json`
  - `Artifacts/layout/layout-metrics-1.4.2-rc3.json`
  - `Artifacts/layout/layout-metrics-current.json`
  - `Artifacts/layout/layout-metrics-compare.md`
- Numeric restoration verified in compare report:
  - `1.4.1 -> current` deltas are `0.0px` for all tracked geometry metrics.
  - Ratio deltas are `0.000000` for all tracked invariants (within `<= 0.001`).
- Root cause confirmed at line level in `SceneBootstrap`:
  - `matchWidthOrHeight` changed to `1f` in `1.4.2-rc3` and is `0f` in current.
  - `AspectRatioFitter` mode changed to `HeightControlsWidth` in `1.4.2-rc3` and is `FitInParent` in current.
- Local Unity tests/coverage executed in prior pipeline run:
  - EditMode + PlayMode completed successfully (`Test run completed. Exiting with code 0`).
  - Coverage gate passed (`Line coverage: 0.915`, threshold `0.8`).

Partially blocked (environment):
- Screenshot regeneration pipeline requires an ABI-compatible Android target.
- Current connected device: `SM_N9005` (`2113b87f`) cannot install arm64 APK (`INSTALL_FAILED_NO_MATCHING_ABIS`).
- Previously used compatible device serial (`R5CRC3ZY9XH`) is not reachable in current environment.

Unblock action:
- Connect an arm64-compatible Android device/emulator, then run:
  - `./build --screenshots` (preferred full pipeline)
  - or `./build --screenshots-only` (after a fresh APK already exists)
