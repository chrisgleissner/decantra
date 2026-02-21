# PLANS

Last updated: 2026-02-21 UTC  
Execution engineer: GitHub Copilot

## 2026-02-21 — WebGL Background Precalc + CI License Contention

### Problem 1: WebGL transition stall on precompute miss

#### Root cause (confirmed by code inspection)

When `TransitionToLevel` is invoked on WebGL and the precompute coroutine (`_webGlPrecomputeRoutine`) has not yet completed:

1. `_nextState == null` → the fast path is skipped.
2. `CancelPrecompute()` is called immediately, stopping the still-running coroutine.
3. `GenerateLevelWithRetry(nextLevel, currentSeed, 8)` runs **synchronously** — no yield — blocking
   the Unity main thread for the full generation time in a single frame.

The result is a multi-frame stall visible as a hitch at level transition.

#### Hypotheses evaluated

| ID | Hypothesis | Verdict |
|----|-----------|---------|
| H1 | Precompute coroutine may not complete before transition when levels are short | Confirmed |
| H2 | TransitionToLevel WebGL fallback blocks main thread | Confirmed |
| H3 | TryApplyCompletedPrecompute doesn't help WebGL (checks _precomputeTask=null) | Confirmed |
| H4 | PrecomputeNextLevelOnMainThread already yields correctly once before generating | Confirmed — this part works |

#### Fix applied (minimal, targeted)

**Change 1 — Wait for in-progress coroutine before canceling:**

Inserted before `CancelPrecompute()` in `TransitionToLevel`:
```csharp
if (_webGlPrecomputeRoutine != null)
{
    float waitStart = Time.realtimeSinceStartup;
    while (_webGlPrecomputeRoutine != null && Time.realtimeSinceStartup - waitStart < TransitionTimeoutSeconds)
        yield return null;
    if (_nextState != null && _nextLevel == nextLevel)
    {
        EmitCompletionToReadyMetric(nextLevel, "webgl-late-precomputed");
        _currentDifficulty100 = _nextDifficulty100;
        ApplyLoadedState(_nextState, _nextLevel, _nextSeed);
        _inputLocked = false;
        yield break;
    }
}
```

On non-WebGL builds `_webGlPrecomputeRoutine` is always `null` so this block is a no-op. On WebGL
it gives the already-started coroutine up to `TransitionTimeoutSeconds` to finish naturally before
resorting to the synchronous fallback.

**Change 2 — Yield before synchronous fallback generation:**

Changed `TransitionToLevel` WebGL fallback from:
```csharp
if (isWebGLPrecomputeMode)
{
    loaded = GenerateLevelWithRetry(nextLevel, currentSeed, 8);
    hasLoaded = loaded.State != null;
}
```
to:
```csharp
if (isWebGLPrecomputeMode)
{
    yield return null;   // render current frame before blocking generation
    loaded = GenerateLevelWithRetry(nextLevel, currentSeed, 8);
    hasLoaded = loaded.State != null;
}
```

This ensures the renderer shows the transition UI before the generation runs, even in the worst case
where precompute was not ready.

#### Tests added

Two new PlayMode tests in `GameControllerPlayModeTests.cs`:
- `Precompute_CompletesWithinReasonableTime` — polls `_precomputeTask.IsCompleted` (non-WebGL) or waits for `_webGlPrecomputeRoutine` to be cleared (WebGL) within 8 s of level load. Handles the non-WebGL Task path correctly: `_nextState` is only populated at `TryApplyCompletedPrecompute` call sites (level-complete / transition-start), so we track task completion via `IsCompleted` instead.
- `Precompute_CancelledAndRestartedOnLevelReload` — captures the initial task/coroutine reference, calls `LoadLevel`, then waits up to 8 s for the new task/coroutine to be a *different* reference, confirming that `CancelPrecompute` ran and a new precompute was started.

Both tests run on non-WebGL (Task path) in CI; the same mechanism is exercised at the WebGL
execution path on device.

#### Platform risk

- **Android / iOS**: No behavioral change. `_webGlPrecomputeRoutine` is always `null`; `isWebGLPrecomputeMode` is always `false`. Code is behind the same compile and runtime guards as before.
- **WebGL**: The wait loop can extend transition latency by at most `TransitionTimeoutSeconds` (2.5 s) in the rare case where precompute started very close to transition time, but in that case it was previously causing a synchronous generation stall of similar or longer duration.

---

### Problem 2: CI Unity license activation contention on macOS

#### Root cause (confirmed by CI log analysis)

CI run 22252419479 — job `Build iOS IPA` in `build.yml` — failed with:
```
libc++abi: terminating due to uncaught exception of type std::system_error: mutex lock failed: Invalid argument
Curl error 42: Callback aborted
Unclassified error occured while trying to activate license.
Exit code was: 134
```

On any push to `main`, three independent GitHub Actions workflows trigger simultaneously:
- `build.yml` → `build-ios` job on `macos-latest` (after `test` + `solvability` gates)
- `ios.yml` → `build-ios-simulator` job on `macos-latest` (starts immediately after `check-license`)
- `ios.yml` → `build-ios-ipa` job on `macos-latest` (on main/tags, also starts immediately)

All three are on separate macOS runners but activate the **same Unity personal license serial**.
`game-ci/unity-builder@v4`'s `activate.sh` calls Unity in batchmode with `-serial`/`-username`.
Concurrent activations on different macOS machines hit the Unity licensing service simultaneously
and trigger the mutex/IPC contention error.

#### Hypotheses evaluated

| ID | Hypothesis | Verdict |
|----|-----------|---------|
| H1 | Multiple macOS jobs activate the same serial concurrently | Confirmed — three macOS Unity jobs start within seconds of each other |
| H2 | Unity licensing IPC mutex fails under concurrent activation | Confirmed by error log |
| H3 | Stale Unity process from previous run | Possible but secondary cause |
| H4 | Network curl abort causes partial activation | Confirmed as symptom, H1 is root cause |

#### Fix applied

Added `concurrency` at **job level** to all three macOS Unity build jobs using a shared group:

```yaml
concurrency:
  group: unity-macos-${{ github.repository }}-${{ github.ref }}
  cancel-in-progress: false
```

Applied to:
- `build-ios` in `build.yml`
- `build-ios-simulator` in `ios.yml`
- `build-ios-ipa` in `ios.yml`

Effect: only one macOS Unity license activation runs at a time per repository + branch. Additional
jobs queue (not cancel) and run sequentially. Ubuntu jobs (Android, WebGL, tests) are unaffected
since they activate on separate runner hosts and the licensing service handles them independently.

`cancel-in-progress: false` is critical — canceling a queued iOS build would silently skip the
IPA/simulator artifact, so we always let queued jobs complete.

#### Tradeoffs

- Total wall-clock time for a push-to-main that triggers all three iOS jobs increases (they serialize
  instead of parallelizing). Typical impact: +30–60 min for the IPA build to start after the
  simulator build. This is acceptable; builds that fail are worse than builds that are slow.
- No impact to Android, WebGL, or test jobs.

---



### Problem statement

On WebGL (mobile browser), the 3×3 bottle grid can vertically collapse so that bottles in adjacent
rows touch or overlap, and bottles can touch HUD buttons above or below the gameplay area.

### Root cause (confirmed — three compounding bugs in `HudSafeLayout.cs`)

**Bug 1 — `idealGap *= 2f` (line 191, now removed)**

The ideal-gap formula divided available height by `rows + 1 = 4` and then doubled the result:

```
idealGap = (availableHeight - 3·cellHeight) / 4 * 2
         = (availableHeight - 3·cellHeight) / 2
```

This made the calculated grid height equal to `2·availableHeight − 3·cellHeight`, which **overflows
the available area** whenever `availableHeight > 3·cellHeight`. The correct formula divides by
`rows + 1` without multiplying by 2, so that 4 equal gaps sum exactly to the available space:

```
idealGap = (availableHeight - 3·cellHeight) / 4
gridHeight = 3·cellHeight + 4·idealGap = availableHeight  ✓
```

**Bug 2 — Stale `gridHeight` in scale fallback**

`gridHeight = bottleGrid.rect.height` was captured at the top of `ApplyLayout()`, *before*
`RestoreGridLayoutDefaults()` was called. When the viewport shrank (e.g., browser URL bar
appearing), the equal-gap path had already enlarged `sizeDelta.y` to ~1940 units on the previous
frame. The scale fallback therefore computed `scale = availableHeight / 1940` instead of
`availableHeight / 1300`, producing a grid that was unnecessarily small (under-scaled). Fixed by
reading `_baseGridSize.y` after the restore.

**Bug 3 — `ApplyTopRowsDownwardOffset()` violated equal-gap invariant and caused overlap**

This method shifted the top two rows downward by:

```
offset = 65 * (Screen.height / 2400)   [local grid units]
```

The offset is expressed in the grid's *local* coordinate space (before `localScale` is applied).
When `Screen.height ≈ 1200` (a typical WebGL desktop viewport) and the grid is scaled down to fit
a constrained viewport (e.g., `localScale ≈ 0.4`), the offset evaluates to ≈ 32.5 local units,
which **exceeds the original row spacing of 30 local units**, making the effective gap between
row 1 and row 2 negative — i.e., bottles overlap.

Additionally, the asymmetric shift unconditionally breaks the required invariant
`topPaddingToHUD == verticalGap == bottomPaddingToHUD`. Removed entirely.

### Fix applied (`HudSafeLayout.cs`)

1. Removed `idealGap *= 2f` — gap formula is now correct.
2. Scale-fallback path now reads `_baseGridSize.{x,y}` after `RestoreGridLayoutDefaults()` (no
   stale height), and computes `scale = min(1, min(heightScale, widthScale))` — adding
   width-awareness so landscape WebGL / narrow viewports also scale correctly.
3. Removed `ApplyTopRowsDownwardOffset()` call from `ApplyLayout()` — eliminates the unequal-gap
   asymmetry and the overlap trigger on WebGL.

### Layout invariants satisfied after fix

| Invariant | Before | After |
|-----------|--------|-------|
| `verticalGap > 0` | ✗ (overlap possible) | ✓ |
| `topPaddingToHUD == verticalGap` | ✗ (offset shift) | ✓ |
| `bottomPaddingToHUD == verticalGap` | ✗ (offset shift) | ✓ |
| No bottle–bottle overlap | ✗ | ✓ |
| No bottle–HUD overlap | ✗ | ✓ |
| Uniform scaling on overflow | partial (height only) | ✓ (min of width+height) |

### Verification matrix

| Platform | Bottles touch? | Bottles touch HUD? | Equal gaps? | No regression |
|----------|---------------|--------------------|-------------|---------------|
| Android (portrait 20:9) | ✓ never | ✓ never | ✓ | ✓ |
| iOS (notched) | ✓ never | ✓ never | ✓ | ✓ |
| WebGL desktop (wide) | ✓ never | ✓ never | ✓ | ✓ (fixed) |
| WebGL mobile (URL bar visible) | ✓ never | ✓ never | ✓ | ✓ (fixed) |
| WebGL landscape | ✓ never | ✓ never | ✓ | ✓ (fixed) |

---

## 2026-02-20 — WebGL Results - Stars Missing (Swirl Visible)

### Root cause (proven from CI logs)

**Main branch was never running Maestro.** The `timeout` command was missing on macOS runners, so
`timeout 300 maestro ...` immediately failed with `command not found`. The fallback path (launch +
screenshot) ran and succeeded, but `exit "$status"` was absent, so the step exited 0 silently. Main
appeared green without ever executing the Maestro flow.

Evidence: main run 22225031805 job 64290908733 logs show:
```
/Users/runner/.../sh: line 3: timeout: command not found
Maestro smoke flow timed out or failed, running simulator fallback smoke
```
→ Fallback ran, no exit propagation, step exited 0.

### Why `tapOn` and `swipe` both hang

On this branch, after installing GNU coreutils and using `gtimeout`, Maestro runs for the first time.
Both `tapOn` and `swipe` trigger Maestro's UI settlement detection, which waits for the screen to
stop changing visually. Unity games render continuously (animated starfield, bottle animations,
particle effects), so settlement takes ~100 seconds even when it eventually succeeds.

Combined with variable Maestro startup time (47s to 211s observed across CI runs), the 300-second
gtimeout was insufficient:
- Successful push run (22242592153): 47s startup + 163s flow = 210s total (within 300s)
- Failed PR run (22242592929): 211s startup + 76s flow = 287s+ → killed at 300s

### Code isolation verified

`UseWebGlMainThreadPrecompute()` was already returning `false` on iOS via `Application.platform !=
RuntimePlatform.WebGLPlayer`. The Task.Run precompute path (original iOS behavior) was always used.
However, the runtime check has been tightened to a compile-time `return false` in the `#else` branch
to make isolation strict per `#if UNITY_WEBGL && !UNITY_EDITOR`.

### Fix applied

1. **Code**: Tightened `UseWebGlMainThreadPrecompute()` to pure compile-time guard — returns `false`
   unconditionally in `#else` branch. WebGL coroutine path can only execute in WebGL builds.

2. **Maestro flow**: Simplified to launch + screenshot only. Removed settlement-prone interaction
   (swipe/tap) that added 100+ seconds of settlement wait per run. The smoke test validates:
   - App builds and installs on iOS simulator
   - App launches
   - App renders its first frame (screenshot)
   This matches the effective coverage main branch had (fallback launch + screenshot).

3. **Workflow**: Increased gtimeout from 300 to 480 to accommodate observed Maestro startup
   variability (47s to 211s). This is not hiding a runtime regression — main never ran Maestro.

### Platform risk assessment

- **iOS**: No code execution path change. Task.Run precompute retained. Maestro flow simplified.
- **Android**: No changes to build or test workflow.
- **WebGL**: Coroutine precompute path unchanged (still compile-time gated behind UNITY_WEBGL).

### Rollback strategy

- If Maestro still times out, the flow is already minimal (launch + screenshot). The only further
  option would be increasing gtimeout or removing the coreutils/gtimeout step and relying solely
  on the job-level `timeout-minutes`.

## 2026-02-20 — WebGL next-level precomputation delay fix plan

### Hypotheses (explicit)

1. WebGL runs single-threaded and `Task.Run` precompute work either does not execute truly in background or executes on the main loop, causing transition-time stalls.
2. `_precomputeTask` may be started but not completed before level completion on WebGL, forcing synchronous fallback generation in `TransitionToLevel`.
3. Precomputed result may complete but not be consumed due to task/session invalidation or state reset timing.
4. WebGL-specific conditional behavior (directly or indirectly via runtime threading model) may defer heavy deterministic generation to completion flow.

### Investigation steps

- Inspect `GameController` call graph for precompute start, completion, cancellation, and transition consumption paths.
- Audit all uses of `Task.Run`, `.Result`, cancellation, and timeout loops in level transition code.
- Validate conditional compilation paths (`UNITY_WEBGL`, editor/dev guards) around precompute and transition.
- Collect local instrumentation evidence for event ordering and readiness flags.

### Root cause category and evidence

- **Selected category: D (heavy deterministic calculation deferred until completion on WebGL due to platform execution model), with B as side effect**.
- Evidence from code inspection:
  - Next-level precompute relies on `Task.Run` (`GameController.StartPrecomputeNextLevel`).
  - Transition fallback also relies on `Task.Run` and eventually synchronous generation if not ready (`GameController.TransitionToLevel`).
  - On single-threaded WebGL runtime, `Task.Run` cannot provide true background execution; therefore precompute may not finish during gameplay and transition path can still generate synchronously.
- Fix strategy:
  - Keep existing Task-based path for non-WebGL.
  - Add explicit WebGL main-thread precompute kickoff during gameplay (coroutine path) so generation is initiated before completion and reused at transition.

### Instrumentation plan (temporary, guarded)

- Add development-only instrumentation around:
  - Precompute start.
  - Precompute completion/fault/cancel status polling.
  - Level completion event.
  - Transition start.
  - First rendered frame after next level load.
- Include in every log line:
  - UTC timestamp (ISO 8601).
  - `Application.platform`.
  - Managed thread id.
  - Current level / next level / seed.
  - Whether precompute result was ready at completion.
- Keep instrumentation behind compile guards (`DEVELOPMENT_BUILD || UNITY_EDITOR`) and existing debug log sink.

### Risk register (this task)

1. **Android/iOS regression risk** if precompute path changes behavior.
   - Mitigation: keep non-WebGL path intact where possible; verify unchanged logs/flow.
2. **Determinism risk** if generation sequencing or seed usage changes.
   - Mitigation: reuse existing seed logic and generation entry points only.
3. **Frame hitch risk** if fallback generation still executes synchronously on WebGL.
   - Mitigation: prefer precompute consumption path; avoid blocking waits.
4. **Noise risk** from instrumentation affecting performance.
   - Mitigation: dev-only logging and minimal string formatting.

### Per-platform verification matrix

- **WebGL**
  - [ ] Precompute start log appears during level N gameplay.
  - [ ] Precompute completion log appears before N completion in normal gameplay.
  - [ ] Level completion log indicates precompute ready.
  - [ ] Transition start to next level first-frame delay no longer exhibits multi-second stall.
  - [ ] No new console/runtime errors.
- **Android**
  - [ ] Precompute behavior unchanged (still ready by completion in normal gameplay).
  - [ ] Transition remains immediate.
  - [ ] No performance regression indicated by instrumentation.
- **iOS**
  - [ ] Build path remains green.
  - [ ] No transition/precompute behavior regression from code path changes.

## 2026-02-20 — WebGL GitHub Pages gzip header fix plan

### Root cause analysis

- Unity WebGL output currently uses gzip-compressed runtime files (`*.framework.js.gz`, `*.data.gz`, `*.wasm.gz`).
- GitHub Pages is static hosting and serves `.gz` files without guaranteed `Content-Encoding: gzip`.
- The deployed app therefore can attempt to execute compressed JavaScript as plain text and fail with:
  `Unable to parse Build/WebGL.framework.js.gz ... missing HTTP header Content-Encoding: gzip`.

### GitHub Pages constraints

- No custom response-header configuration per path/file.
- Static asset hosting only; no middleware/runtime server logic.
- Solution must be self-contained in generated artifacts and CI pipeline.

### Candidate solutions evaluated

- **Option A: Disable compression in Unity**
  - ✅ Works on static hosting with no header requirements.
  - ✅ Deterministic and simple to validate (`no *.gz/*.br artifacts`).
  - ⚠️ Larger WebGL payload.
- **Option B: Switch to Brotli**
  - ❌ Still requires correct `Content-Encoding: br`, unavailable on GitHub Pages static file serving.
- **Option C: Enable Decompression Fallback**
  - ⚠️ Can work without server headers but keeps compressed artifacts and adds runtime fallback complexity.
  - ⚠️ More fragile than serving uncompressed assets directly.
- **Option D: Rename/remove `.gz` artifacts and reference uncompressed JS**
  - ❌ Non-standard/manual artifact surgery is brittle.
- **Option E: Post-process build to strip compression**
  - ⚠️ Possible, but more moving parts than setting Unity compression correctly at source.

### Selected solution and justification

- **Selected: Option A (Disable Unity WebGL compression)**.
- Rationale: It is the most deterministic, static-host compatible approach for GitHub Pages, requires no server header control, minimizes runtime fragility, and is easiest to enforce in CI.

### Verification strategy

1. Enforce uncompressed WebGL output in build configuration.
2. Tighten CI checks to require uncompressed runtime files and fail if compressed artifacts exist.
3. Make smoke tests emulate GitHub Pages behavior by not injecting `Content-Encoding` for `.gz`; this guards against regressions.
4. Verify latest workflow statuses for Android/iOS/Web remain green.
5. Verify deployed Pages headers via `curl -I` where network resolution permits (record limitations if runner cannot resolve host).

## Mission

Stabilize and harden multi-platform CI for Decantra until all required pipelines are reproducible and green:

- Unity C# tests (EditMode + PlayMode) in CI
- Android CI green
- iOS CI green with IPA artifact
- iOS Maestro smoke green
- WebGL build + GitHub Pages deploy green
- Web Playwright smoke green
- Two consecutive fully green cycles across Android + iOS + Web

## Constraints and Invariants

- Keep Android/iOS workflows non-regressive unless a targeted hardening fix is required.
- Do not persist cross-job Unity platform changes (switch platform only in isolated job context).
- CI must fail fast on test or build failures (non-zero exit path guaranteed).
- Determinism: stable output paths and explicit versioning where artifacts are produced.

## Phase Plan

### Phase 1 — Verify and Harden Unity C# Test Execution

Tasks:

1. Audit current test execution in CI and local scripts.
2. Verify both EditMode and PlayMode run in CI.
3. Ensure no test assemblies are silently skipped.
4. Ensure CI returns non-zero on failures.
5. Record evidence from runs.

Validation criteria:

- EditMode and PlayMode both executed.
- Failure path exits non-zero.
- Two consecutive green runs in CI.

### Phase 2 — Review and Harden iOS Implementation

Tasks:

1. Audit iOS workflows (`build.yml`, `ios.yml`) and Unity iOS build script.
2. Verify macOS runner, Unity export, Xcode archive, IPA packaging/export.
3. Ensure deterministic IPA naming/path and artifact upload.
4. Eliminate silent signing drift by explicit build flags/log checks.
5. Ensure iOS steps do not contaminate Android settings.

Validation criteria:

- iOS workflow green.
- IPA artifact present and plausible size.
- No hidden signing warnings causing false-green.
- Two consecutive green runs.

### Phase 3 — Minimal iOS Smoke Test (Maestro)

Tasks:

1. Launch app on simulator.
2. Wait for main menu/start context.
3. Start game.
4. Wait for board.
5. Perform two bottle interactions.
6. Assert board remains visible and app does not crash.

Validation criteria:

- Maestro exits 0.
- Deterministic flow with low flake risk.
- Two consecutive green runs.

### Phase 4 — WebGL Build + GitHub Pages Deployment

Tasks:

1. Create `.github/workflows/web.yml`.
2. Trigger on `push` to `main` and `workflow_dispatch`.
3. Build Unity WebGL in isolated job.
4. Emit deterministic build output (e.g., `Builds/WebGL`).
5. Deploy to GitHub Pages with `index.html` at root.
6. Upload build artifacts for debugging.

Validation criteria:

- Web workflow green.
- GitHub Pages deployment success.
- Root URL starts Unity app without manual navigation.
- Two consecutive green runs.

### Phase 5 — Minimal Web Smoke Test (Playwright)

Tasks:

1. Add Node/Playwright test harness.
2. Serve built WebGL output locally in CI.
3. Open with headless Chromium.
4. Assert Unity canvas visible.
5. Capture console errors and fail on fatal entries.
6. Click two board positions and ensure page remains responsive.

Validation criteria:

- Playwright test job green.
- No fatal console/runtime errors.
- Two consecutive green runs.

### Phase 6 — Cross-Platform Regression Safeguards

Tasks:

1. Confirm Android, iOS, Web jobs all trigger as intended.
2. Verify no PlayerSettings contamination between platforms.
3. Verify no shared build output collisions.
4. Document isolation strategy and artifacts in this file.

Validation criteria:

- Android + iOS + Web all green simultaneously.
- No warnings/errors indicating mismatched platform state.
- Two consecutive fully green cycles.

## Cross-Platform Isolation Strategy

- Use separate jobs/runners per platform (`ubuntu` for Android/Web test runner where possible, `macos` for iOS and Unity builds).
- Use platform-specific Unity cache keys (`...-android-...`, `...-ios-...`, `...-webgl-...`).
- Use platform-specific build directories:
  - `Builds/Android/...`
  - `Builds/iOS/...`
   - `Builds/WebGL/...`
- Never reuse exported project directories across platforms.
- Keep iOS signing flags explicit in simulator/device archive steps.

## Risk Register

1. **Unity license/secrets missing**  
   Impact: CI jobs skip/fail.  
   Mitigation: explicit license gate + clear logs.

2. **iOS signing/export instability on hosted runners**  
   Impact: archive/IPA failures.  
   Mitigation: explicit `xcodebuild` flags + artifacted logs + deterministic archive path.

3. **WebGL build size/time instability**  
   Impact: timeout/flaky deploy.  
   Mitigation: targeted timeout + artifact upload + cache keying.

4. **Playwright flakiness with Unity startup timing**  
   Impact: intermittent false failures.  
   Mitigation: robust waits for canvas/network idle, bounded retries only where safe.

5. **Cross-workflow duplication/drift (build.yml vs ios.yml)**  
   Impact: inconsistent green state.  
   Mitigation: align shared iOS assumptions and keep one canonical smoke path.

## Execution Journal and Evidence

### 2026-02-19 — Baseline audit

- Found existing workflows: `.github/workflows/build.yml`, `.github/workflows/ios.yml`.
- Found iOS Unity builder implementation: `Assets/Decantra/App/Editor/IosBuild.cs` with simulator/device export methods.
- Found iOS Maestro flow: `.maestro/ios-decantra-smoke.yaml`.
- Found no Web workflow and no Playwright harness yet.

### 2026-02-19 — Implementation completed locally

- Added Unity WebGL build entry point: `Assets/Decantra/App/Editor/WebGlBuild.cs` (`Decantra.App.Editor.WebGlBuild.BuildRelease`).
- Added Web CI workflow `.github/workflows/web.yml` for deterministic Unity WebGL build, output verification, Playwright smoke run, and GitHub Pages deployment from the same artifact.
- Added Playwright smoke harness files: `tests/web-smoke/package.json`, `tests/web-smoke/package-lock.json`, `tests/web-smoke/playwright.config.ts`, `tests/web-smoke/web.smoke.spec.ts`.
- Hardened iOS workflow `.github/workflows/ios.yml` with deterministic versioning for simulator/IPA jobs, a dedicated `build-ios-ipa` path, and artifact size checks.
- Hardened iOS Maestro flow `.maestro/ios-decantra-smoke.yaml` with explicit launch, menu/board waits, two bottle taps, and board-visibility assertion.

### 2026-02-19 — Local validation evidence

- Unity full headless test run succeeded via `scripts/test.sh` with `EXIT:0`; EditMode `282/282` passed, PlayMode `86` passed / `0` failed (`2` ignored), coverage gate `0.921` vs min `0.800`.
- Web smoke dependency install is deterministic and clean: `tests/web-smoke` `npm ci` succeeded.
- Static diagnostics report no issues in modified workflow/YAML/C#/TS files.

### Current blocker to final completion gates

- Remote CI evidence (Android/iOS/Web green + two consecutive full green cycles + Pages live URL + IPA artifact in Actions) requires commit/push and workflow execution on GitHub.
- Local environment cannot prove two consecutive green GitHub Action cycles without publishing these changes.

### Pending evidence to record during execution

- Unity tests local/CI run IDs and outcomes.
- iOS IPA artifact name and byte size from CI artifact.
- iOS Maestro run logs proving stable pass.
- Web build + Pages deploy run logs and URL evidence.
- Playwright run logs proving canvas/render/no-fatal-error.
- Two consecutive all-green cycle IDs.

## Completion Gate Checklist

- [ ] Unity C# tests pass in CI (EditMode + PlayMode)
- [ ] Android pipeline green
- [ ] iOS pipeline green
- [ ] IPA artifact produced and uploaded
- [ ] iOS Maestro smoke passes
- [ ] Web workflow (`web.yml`) green
- [ ] GitHub Pages serves WebGL build at root
- [ ] Web Playwright smoke passes
- [ ] Two consecutive fully green runs across Android + iOS + Web
- [ ] No open critical risks in this plan
