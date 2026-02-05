# PLANS - Scoring, Interstitial Layout, Orientation Fix, Starfield Tuning

## Scope
Implement final scoring + stars, interstitial centering fix, Android portrait stability fix, and calmer starfield tuning. Deterministic logic only, no new mechanics or visual styles.

## Constraints & References
- Determinism required for all scoring and visual logic.
- Stars are badge-only; they must not drive score.
- CleanSolve = no undo, no restart, no hints (per attempt).
- Portrait orientation enforced; auto-rotation remains disabled.
- Use internal difficulty D in [0..100] for scoring.
- All numeric constants must be justified in code comments.

## Execution Plan (Authoritative)

### Step 1 - Implement final scoring + stars + tests
- [x] Update `ScoreCalculator` + `ScoreSession` to the new formulas (Base/Perf/Clean/Total).
- [x] Update `GameController` to supply M_opt, M_max, D, CleanSolve and compute stars via new thresholds.
- [x] Update tests in EditMode for scoring, total accumulation, and star thresholds.
- [x] Run full test suite (`tools/test.sh`).

### Step 2 - Fix interstitial vertical centering + test
- [x] Recenter stars + score as a group within the bright rectangle; compute upward shift from layout metrics.
- [x] Add/adjust a PlayMode test that asserts group centering + upward shift.
- [x] Run full test suite (`tools/test.sh`).

### Step 3 - Enforce portrait orientation early + verify
- [x] Force `Screen.orientation = Portrait` at scene init (early).
- [x] Record PlayerSettings orientation state in this plan for verification.
- [x] Run full test suite (`tools/test.sh`).

### Step 4 - Calmer starfield tuning + verify
- [x] Reduce star density (>=50%), reduce speed to ~30%, add brightness levels (dark/medium/white) without changing size logic.
- [x] Confirm starfield shader changes are deterministic and performance-safe.
- [x] Run full test suite (`tools/test.sh`).

### Step 5 - Release build + screenshots
- [x] Build release APK.
- [x] Capture fresh Play Store screenshots from release build.
- [x] Archive screenshot paths and build outputs in this plan.

## Verification Notes (Fill As You Go)
- PlayerSettings (ProjectSettings.asset):
  - defaultScreenOrientation: 1
  - allowedAutorotateToPortrait: 1
  - allowedAutorotateToPortraitUpsideDown: 0
  - allowedAutorotateToLandscapeLeft/Right: 0 / 0
- Release build output:
- Release build output: Builds/Android/Decantra.apk
- Screenshot output: doc/play-store-assets/screenshots/phone
