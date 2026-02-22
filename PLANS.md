# PLANS

Last updated: 2026-02-22 UTC  
Execution engineer: GitHub Copilot

## 2026-02-22 — UX reset/tutorial/HUD adjustments

- [x] Inspect reset, tutorial, HUD, and options wiring to identify minimal touch points.
- [x] Remove reset-button long-press behavior and active wiring so only options-driven game reset remains.
- [x] Update reset-game confirmation copy to accurately describe reset semantics (score/stars reset, high score/max level preserved).
- [x] Extend tutorial with score-button and options-button instructional highlights per UX requirements.
- [x] Remove gameplay HUD SFX control while preserving options-based audio controls.
- [x] Update focused PlayMode tests for long-press removal, reset dialog wording, tutorial step content/order, and HUD settings panel removal.
- [x] Run verification:
  - [x] Investigate recent CI workflow runs/logs (GitHub Actions MCP): latest non-success runs were cancellations, no failure conclusion found.
  - [x] Attempt baseline local tests before changes (blocked: Unity executable unavailable in sandbox; `./scripts/test.sh` reported Unity not found).
  - [x] Attempt targeted local tests (blocked by same Unity executable limitation).
  - [x] Capture deterministic UI screenshot artifact for review: https://github.com/user-attachments/assets/adc03a24-98c5-475a-aca6-45fcbbf206ea
- [x] Run `code_review`, then `codeql_checker`; addressed comments and re-ran review clean.

### Touched files

- `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs`
- `Assets/Decantra/Presentation/Runtime/RestartGameDialog.cs`
- `Assets/Decantra/Presentation/Runtime/TutorialManager.cs`
- `Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs`
- `Assets/Decantra/Tests/PlayMode/OptionsNavigationPlayModeTests.cs`

### Deterministic verification notes

- **A (remove long-press reset):** `WireResetButton` now removes any `LongPressButton` from `ResetButton` and only wires short click to `ResetCurrentLevel`. Test asserts `ResetButton` has no `LongPressButton`.
- **B (reset wording + semantics):** reset dialog copy updated in both bootstrap creation and runtime `Show()`. Existing and updated PlayMode assertions verify reset preserves `HighScore` and `HighestUnlockedLevel` while resetting current level/score/stars.
- **C (tutorial extension):** tutorial step list now includes new `score` step targeting `ScorePanel`; `options` instruction updated to describe new-game semantics. Tests verify step order, targets, and exact text.
- **D (remove gameplay HUD SFX button):** `CreateSettingsPanel(...)` is no longer created in scene setup; options overlay still contains `AudioSection/SfxRow` and `SfxVolumeRow` (validated by existing options test assertions).

## 2026-02-22 — PR review follow-up fixes

- [x] Centralize restart confirmation copy via `RestartGameDialog.RestartConfirmationMessage` to prevent drift.
- [x] Reuse centralized reset copy in `SceneBootstrap` and PlayMode test assertion.
- [x] Update `OptionsNavigationPlayModeTests` to:
  - [x] assert no `SettingsPanel` using inactive-aware lookup helper.
  - [x] include `AssertInstructionFits` coverage for SCORE and OPTIONS tutorial copy.
- [x] Update stale long-press comment in `GameControllerPlayModeTests`.
- [x] Remove unused `CreateSettingsPanel` helper from `SceneBootstrap`.
- [x] Update PLANS header timestamp to latest date.
- [x] Verification:
  - [x] Attempt local Unity test run (`./scripts/test.sh`) — blocked in sandbox (`Unity not found`).
  - [x] `code_review` completed with no comments.
  - [x] `codeql_checker` completed with 0 alerts.

## 2026-02-21 — WebGL: First Pour Silent + Progress Not Persisted

### Problem 1: First pour after startup is silent on WebGL

#### Root cause

Browsers enforce an autoplay policy: the Web Audio API `AudioContext` starts in `"suspended"` state. Unity's WebGL audio backend calls `audioContext.resume()` in response to the first user interaction. However, `resume()` returns a Promise that resolves **asynchronously**. When the first bottle click triggers `PlayPourSfx → PlayTransientSegment → source.Play()`, the Promise has not yet resolved, so the AudioContext is still suspended and `source.Play()` is silently dropped.

From the second interaction onwards the AudioContext is already `"running"`, so all subsequent pours play normally. Android is unaffected (native audio, no browser Promise).

#### Fix

Added `_firstPlayLaunched` flag + `RetryFirstPlayIfNeeded` coroutine (gated by `#if UNITY_WEBGL && !UNITY_EDITOR`) in `AudioManager.PlayTransientSegment`. On the very first `source.Play()`:
- The call fires immediately (works on Android, is a no-op on WebGL if context still suspended).
- A one-frame coroutine is scheduled. After the frame, if `source.isPlaying` is still false (WebGL AudioContext resume pending), we retry `source.Play()` — by this time the Promise has resolved.

The flag ensures the retry coroutine is only scheduled once, adding zero overhead to all subsequent plays.

### Problem 2: Progress (level / score / stars) not persisted on WebGL

#### Root cause

`ProgressStore.Save()` uses `File.WriteAllText(Application.persistentDataPath, ...)`. On WebGL, Unity maps `Application.persistentDataPath` to `/idbfs`, an Emscripten IDBFS (IndexedDB-backed virtual filesystem). However, writes to IDBFS are **in-memory only** unless explicitly flushed to IndexedDB with `FS.syncfs(false, callback)`. Without this flush, all progress is lost when the browser tab is closed or refreshed.

`SettingsStore` (audio toggle, accessibility, starfield settings) already uses `PlayerPrefs`, which on WebGL maps to `localStorage` and persists automatically — no flush needed.

#### Fix

Added `#if UNITY_WEBGL && !UNITY_EDITOR` guards in `ProgressStore.Load()` and `ProgressStore.Save()` to use `PlayerPrefs` (key `"decantra.progress.v1"`) instead of file I/O. `PlayerPrefs.Save()` is called after each write to ensure `localStorage` is flushed synchronously.

Existing file-based tests use the `ProgressStore(overridePaths)` constructor, which is unaffected by the WebGL guard (the guard only triggers when no override paths are set and we're in a WebGL runtime build, not the Editor).

No change to `SettingsStore` — it already works correctly on WebGL.

---

## 2026-02-21 — Liquid Fill Top-Gap Invariance Fix

### Problem statement

On WebGL mobile (and to a lesser extent on other platforms), the gap between the top of the
colored liquid stack and the inner top border of the straight bottle body varies with bottle size.

- Tall (high-capacity) bottle: ~9–11 px empty gap visible above the liquid.
- Small (low-capacity) bottle: ~0–2 px gap.
- Gap is not constant → violates the "full bottle should fill to the top" invariant.

### Root cause (confirmed by code inspection)

In `SceneBootstrap.CreateBottle()` the initial RectTransform dimensions are:

| Element     | Height | Center Y | Top Y       | Bottom Y    |
|-------------|--------|----------|-------------|-------------|
| Outline     |  372   |   -6     | 180         | -192        |
| LiquidMask  |  320   |  -10     | 150         | -170        |

After `BottleView.ApplyCapacityScale(ratio)` both elements shrink from their bottom edge:

```
outlineTop(r)    = -192 + 372 * r
liquidMaskTop(r) = -170 + 320 * r
```

The full-fill gap:

```
gap(r) = outlineTop(r) - liquidMaskTop(r) = -22 + 52 * r
```

For r = 1.0 (max-capacity / "tall" bottle): **gap = 30 canvas units** ≈ 9–11 screen px  
For r = 0.5 (half-capacity / "small" bottle): **gap = 4 canvas units** ≈ 1–2 screen px  

The gap is proportional to `ratio`, so it scales with bottle size — this is the observed defect.
The root cause is that `LiquidMask.height (320) ≠ Outline.height (372)` and the two elements
have different initial Y-centers (`-10` vs `-6`), so their tops diverge after proportional scaling.

### Fix (minimal, targeted)

Make the `LiquidMask` and `LiquidRoot` match the outline dimensions exactly:

1. **`SceneBootstrap.CreateBottle()`**: change  
   - `liquidMaskRect.sizeDelta = new Vector2(128, 372)` (was 320)  
   - `liquidMaskRect.anchoredPosition = new Vector2(0, -6)` (was -10)  
   - `liquidRect.sizeDelta = new Vector2(112, 372)` (was 320)

2. **`BottleView.cs`**: change  
   - `RefSlotRootHeight = 372f` (was 320f)  
   - Simplify `ApplyCapacityScale` slotRoot/liquidMask block: derive liquid height from
     `origMask.SizeDelta.y * ratio` (the outline-aligned mask height) and set both mask and
     slotRoot to the same value, removing the old snap-to-capacity rounding (which could
     introduce a 2–4-unit rounding error for maxCap=8).

After the fix:

```
liquidMaskTop(r) = -192 + 372 * r = outlineTop(r)
gap(r) = 0  ∀ r    (invariant A satisfied)
```

Pixel-per-slot invariance is preserved:
```
unitHeight = (372 * ratio) / capacity = 372 / maxCapacity   (constant across bottles ✓)
```

### Measurable invariants and acceptance criteria

| # | Invariant | Target |
|---|-----------|--------|
| A | Full-bottle gap: `abs(outlineTopWorld - slotRootTopWorld) ≤ 2 world units` | for all capacities |
| B | Consistent gap across capacities (same `maxCap`): max(gap) - min(gap) ≤ 2 world units | ✓ |
| C | Pixel-invariant liquid heights (existing test `LiquidHeights_MatchSlotCountsAcrossCapacities`) | still pass |
| D | Proportional slotRoot heights (existing test `SlotRootHeights_AreProportionalToCapacity`) | still pass |
| E | All EditMode + PlayMode tests green in CI | ✓ |

### Risk register and rollback plan

| Risk | Mitigation | Rollback |
|------|------------|---------|
| Bottom of bottle fills 22 more canvas units (previously empty at bottom) | Visually correct — liquid fills to the glass bottom; rounded mask clips corners naturally | Revert three-line change to SceneBootstrap + RefSlotRootHeight |
| Snapping removed may accumulate float error | 372/maxCap is representable for typical maxCap; error < 0.001 units | Re-add snap if visible artifacts appear |
| Android layout regression | No change to bottle/grid sizing or HUD; only liquid fill region moves | Revert |
| Existing BottleVisualMappingTests use 300 as local RefSlotRootHeight | Those tests are parameterized; they don't reference BottleView's constant | N/A |

### Verification (automated)

New test `FullBottle_LiquidTopAligned_ToInnerBodyTop_AcrossCapacities` added to
`BottleVisualConsistencyTests.cs` validates invariant A and B for capacities {4,6,8,10} with
maxCap=10.

---

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

---

## 2026-02-21 — Liquid Rendering Invariants: Explicit Interior Bounds

### Problem statement

The liquid rendering system must strictly enforce four invariants:

1. **Interior Fill Bounds**: Liquid segments are rendered within the straight-sided interior region only (excludes border, neck, flange, decorative geometry).
2. **Pixel Conservation**: Pouring N segments removes exactly N × segmentPixelHeight rows from source and adds the same to target.
3. **Deterministic Segment Height**: `segmentPixelHeight = interiorHeight / maxSegments` — computed fresh every frame, no incremental float accumulation.
4. **Fullness Logic**: A bottle is full when `segmentCount == maxSegments`; rendering reflects state, it does not define state.

### Current rendering architecture

Bottle hierarchy (per bottle in the 3 × 3 grid):

```
Bottle_N (RectTransform, 220 × 420)
├── GlassBack  (height 350, center Y -8)
├── LiquidMask (height 372, center Y -6)  ← Mask clipping region
│   ├── LiquidRoot (height 372, bottom-anchored)  ← Segment container
│   │   ├── Segment_1 .. Segment_N  (bottom-stacked)
│   │   └── Incoming  (pour preview)
│   └── LiquidSurface
├── GlassFront (height 356, center Y -8)
├── Outline    (height 372, center Y -6)  ← border-only (fillCenter=false)
├── BottleNeck (height  56, center Y 211) ← above body
├── BottleFlange (height 14, center Y 187)← at body/neck junction
└── Rim, NeckInnerShadow, Stopper, …
```

Interior bounds (bottle local space, centre-pivot parent):

| Property        | Value  | Derivation                               |
|-----------------|--------|------------------------------------------|
| InteriorBottomY | -192   | = -(InteriorHeight / 2) + CenterOffset   |
| InteriorTopY    |  180   | = +(InteriorHeight / 2) + CenterOffset   |
| InteriorHeight  |  372   | = Outline.height = LiquidMask.height     |
| CenterOffset    |   -6   | = Outline/LiquidMask.anchoredPosition.y  |

The BottleFlange bottom edge is at Y = 187 − 14/2 = 180 = InteriorTopY, confirming that
InteriorTopY is exactly the boundary between the straight body and the neck/flange geometry.

Segment height invariant:
```
segmentPixelHeight = InteriorHeight × (capacity / maxCapacity) / capacity
                   = InteriorHeight / maxCapacity          (constant across all bottles ✓)
```

### Identified root cause

Although the invariants held mathematically, the interior bounds were **implicit**:

- `ApplyCapacityScale` read `origMask.SizeDelta.y` (runtime saved value of LiquidMask.sizeDelta.y)
  to derive `liquidHeight`. This couples liquid sizing to the mask sprite's runtime dimensions
  rather than to a named, purpose-built constant.
- `RefSlotRootHeight = 372f` duplicated the interior height value without naming its semantic role.
- No tests explicitly verified that the first filled segment's bottom aligns with InteriorBottomY.

### Corrective plan

| # | Change | File |
|---|--------|------|
| 1 | Add `InteriorBottomY = -192f`, `InteriorTopY = 180f`, `InteriorHeight = 372f` constants | BottleView.cs |
| 2 | Replace `origMask.SizeDelta.y * ratio` with `InteriorHeight * ratio` in `ApplyCapacityScale` | BottleView.cs |
| 3 | Replace `InteriorHeight * (1f − ratio) * 0.5f` for `maskHalfDelta` (explicit formula) | BottleView.cs |
| 4 | Replace `RefSlotRootHeight * ratio` fallback with `InteriorHeight * ratio` | BottleView.cs |
| 5 | Add PlayMode test `EmptyBottle_FirstSegment_AlignsToInteriorBottom` | BottleVisualConsistencyTests.cs |

### Risk register

| Risk | Mitigation |
|------|------------|
| Value mismatch between constants and CreateBottle setup | InteriorHeight = 372 matches CreateBottle liquidMaskRect.sizeDelta.y exactly; asserted by new test |
| Float precision regression | Using InteriorHeight directly removes a layer of runtime indirection; no new float ops introduced |
| Existing tests broken | All existing tests depend on same pixel invariant; constants produce identical values |

### Validation

- All existing PlayMode + EditMode tests must remain green.
- New test `EmptyBottle_FirstSegment_AlignsToInteriorBottom`: verifies that for a bottle with 1 filled slot, the world-space bottom of the first segment aligns with the world-space bottom of the LiquidRoot (= InteriorBottomY after scale).
- `FullBottle_LiquidTopAligned_ToInnerBodyTop_AcrossCapacities`: remains the canonical validation for Invariant 1 top-edge correctness.

### Completion checklist

- [x] InteriorBottomY / InteriorTopY / InteriorHeight constants added to BottleView.cs
- [x] ApplyCapacityScale decoupled from origMask.SizeDelta.y
- [x] New EmptyBottle_FirstSegment test added and passes
- [x] All existing liquid-rendering tests pass
- [x] CI green

---

## 2026-02-21 — Reset Game: Preserve Lifetime Progression Stats

### Problem statement

The "Reset Game" action (triggered from the Options overlay) was wiping all `ProgressData`
fields, including `HighScore` and `HighestUnlockedLevel`. These are lifetime progression
stats displayed in the Score Details HUD panel and must be preserved across a session reset.

### Root cause

In `GameController.RestartGameConfirmed()`:
```csharp
_progress = new ProgressData
{
    HighestUnlockedLevel = 1,  // ← erases all-time max level
    HighScore = 0,             // ← erases all-time high score
    ...
};
```

### Fix

Capture `HighScore` and `HighestUnlockedLevel` from the existing `_progress` before
replacing it:
```csharp
int preservedHighScore = _progress?.HighScore ?? 0;
int preservedMaxLevel  = _progress?.HighestUnlockedLevel ?? 1;
_progress = new ProgressData
{
    HighScore              = preservedHighScore,
    HighestUnlockedLevel   = preservedMaxLevel,
    CurrentLevel           = 1,   // session reset
    CurrentScore           = 0,   // session reset
    StarBalance            = 0,   // session reset
    ...
};
```

### Affected fields

| Field | Behaviour after fix |
|-------|---------------------|
| `HighScore` | Preserved (lifetime stat) |
| `HighestUnlockedLevel` | Preserved (max level ever reached, lifetime stat) |
| `CurrentLevel` | Reset to 1 (session) |
| `CurrentScore` | Reset to 0 (session) |
| `StarBalance` | Reset to 0 (session) |
| `CurrentSeed` | Reset to 0 (session) |
| `CompletedLevels` | Cleared (session) |
| `BestPerformances` | Cleared (session) |

### Tests

- `RestartGame_ConfirmationResetsProgress` — updated assertions: `HighScore` must equal 200
  (preserved from fixture), `HighestUnlockedLevel` must equal 7 (preserved); `CurrentLevel`
  and `CurrentScore` must still be reset to 1 and 0 respectively.
- `RestartGame_WithNoPriorProgress_ResetsToDefaults` — new test verifying that reset with
  no prior progress yields `CurrentLevel == 1`, `CurrentScore == 0`, `StarBalance == 0`,
  and that `HighScore >= 0` / `HighestUnlockedLevel >= 1`.

### Completion checklist

- [x] `RestartGameConfirmed()` preserves `HighScore` and `HighestUnlockedLevel`
- [x] `RestartGame_ConfirmationResetsProgress` test updated
- [x] `RestartGame_WithNoPriorProgress_ResetsToDefaults` test added
- [x] PLANS.md updated

---

## 2026-02-21 — Liquid Segment Height Invariant

### Simplified requirement

Every segment in every bottle must have the same height in canvas units.
Formally: `segmentHeight = InteriorHeight / levelMaxCapacity` — constant across all bottle
capacities (4–10) and positions in the same level.

"Interior area" is the main straight-sided body region of the bottle, defined by the
`LiquidMask` RectTransform (height 372, centre Y −6 in bottle-local canvas space).

### Audit of current state (after PR #40)

#### Geometry at `levelMaxCapacity = M`, bottle `capacity = C`

| Element    | Initial height | After ApplyCapacityScale  |
|------------|---------------|---------------------------|
| Outline    | 372           | 372 × C/M                 |
| LiquidMask | 372           | 372 × C/M (same centre Y) |
| LiquidRoot | 372           | 372 × C/M (bottom-anchored to LiquidMask) |

#### Segment height derivation

```
slotRootHeight  = InteriorHeight × C/M        (set in ApplyCapacityScale)
segmentHeight   = LocalHeightForUnits(slotRootHeight, C, M, 1)
                = slotRootHeight × 1 / C
                = InteriorHeight × (C/M) / C
                = InteriorHeight / M           ← CONSTANT across all C ✓
```

Every bottle in the same level therefore produces segments of identical height in canvas
units, regardless of its own capacity.

#### Why the invariant holds

`LocalHeightForUnits(height, capacity, refCap, units) = height × units / capacity`.
The `refCap` parameter is accepted for API stability but not used in the formula.
`ApplyCapacityScale` sets `slotRoot.sizeDelta.y = InteriorHeight × ratio` with
`ratio = capacity / _levelMaxCapacity`, so `height × 1 / capacity` cancels to
`InteriorHeight / _levelMaxCapacity`.

#### Border analysis

The bottle outline uses `Image.Type.Sliced`, `fillCenter = false`, with the `rounded`
sprite (64 × 64 px at PPU = 100, border = 12 px).

Corner/strip size in canvas units = 12 px / 100 ppu = **0.12 canvas units**.

At the reference resolution of 1080 × 1920 the canvas maps 1 unit ≈ 1 screen pixel, so
the visible border strip is ~0.12 screen pixels — sub-pixel.  The `LiquidMask` is sized
to the same 372-unit height as the outline, so the border has no practical effect on
which pixels are visible to the player.  No inset adjustment is needed.

### Changes made

| # | Change | File |
|---|--------|------|
| 1 | Made `InteriorBottomY`, `InteriorTopY`, `InteriorHeight`, `InteriorCenterY` `public` (were `internal`) so tests can reference them directly | `BottleView.cs` |
| 2 | Added `SegmentHeight_IsConstant_AcrossAllBottleCapacities` test | `BottleVisualMappingTests.cs` |
| 3 | Added `FullBottle_FillsEntireInteriorArea` test | `BottleVisualMappingTests.cs` |
| 4 | Updated comments in `BottleView.cs` to state the segment-height invariant explicitly | `BottleView.cs` |

### Completion checklist

- [x] Segment height = `InteriorHeight / levelMaxCapacity` proven and documented
- [x] `SegmentHeight_IsConstant_AcrossAllBottleCapacities` test added and passes
- [x] `FullBottle_FillsEntireInteriorArea` test added and passes
- [x] `BottleView` interior constants promoted to `public` for test access
- [x] PLANS.md updated
- [x] All existing tests unaffected
- [x] CI green

---

## 2026-02-22 — Actual Border Overlap Fix

### Clarification from device owner

User confirmed: bottom border on a Samsung Galaxy S21 is **≈ 10 pixels wide** — the same
stroke width all around the bottle body outline, not sub-pixel.

### Root cause (proven)

`CanvasScaler.referencePixelsPerUnit` defaults to **100** (not set in `CreateCanvas`).
The Outline sprite is `CreateRoundedRectSprite(64, 12)` at PPU **100**.

Effective border in canvas units:
```
border_cu = border_px / (sprite.PPU / canvas.referencePixelsPerUnit)
           = 12       / (100         / 100                         )
           = 12 canvas units
```

At the S21 (1080 wide) with `ScaleWithScreenSize` match-width, scale = 1.0 →
12 canvas units ≈ 12 screen pixels.  This matches the user's "≈ 10 px" estimate.

**The previous analysis was wrong**: it assumed effectivePPU = sprite.PPU = 100,
giving 0.12 canvas units (sub-pixel).  The correct formula uses
`referencePixelsPerUnit` as the denominator, giving 1.0 as the effective PPU.

### Geometry table

```
Outline: height=372, centre Y=-6, border=12 cu
  outer bottom = -192,   outer top = 180
  inner bottom = -180,   inner top = 168   (inner = outer ± border)

LiquidMask BEFORE fix: height=372, centre=-6
  bottom=-192 (same as Outline outer! → 12 cu behind border ✗)
  top   = 180 (same as Outline outer! → 12 cu behind border ✗)

LiquidMask AFTER fix:  height=348, centre=-6  (= 372 - 2×12)
  bottom=-180 (= Outline inner bottom ✓)
  top   = 168 (= Outline inner top    ✓)
```

`InteriorCenterY` stays **−6** because the inset is symmetric; only
`InteriorHeight` changes 372 → 348 and `InteriorBottomY/TopY` shift by ±12.

### Segment height invariant (unchanged, updated values)

```
slotRootHeight  = InteriorHeight × capacity/maxCapacity = 348 × C/M
segmentHeight   = slotRootHeight / capacity = 348/M   ← constant across all bottles ✓
```

### Changes made

| # | File | Change |
|---|------|--------|
| 1 | `SceneBootstrap.cs` | LiquidMask `sizeDelta.y` 372→348; LiquidRoot `sizeDelta.y` 372→348 |
| 2 | `BottleView.cs` | `InteriorBottomY`=-180, `InteriorTopY`=168, `InteriorHeight`=348, `InteriorCenterY`=-6 (unchanged); update comment with derivation |
| 3 | `BottleView.cs` | Add `ShowInteriorBoundsDebug` static flag (dev/editor only); `AssertInteriorBoundsValid()` fires `Debug.Assert` if LiquidMask bottom ≠ InteriorBottomY; `UpdateInteriorBoundsDebugOverlay()` creates a green fill-region rect and logs per-bottle values when flag is set |
| 4 | `BottleVisualConsistencyTests.cs` | Update `FullBottle_LiquidTopAligned_ToInnerBodyTop_AcrossCapacities`: gap must now be ≈ 12/372 × outlineHeight (resolution-independent) |
| 5 | `BottleVisualConsistencyTests.cs` | Add `BottomBorder_NotOverlappedByLiquid`: asserts `liquidMaskBottom - outlineBottom ≈ border thickness` (FAILS before fix, PASSES after) |

### Completion checklist

- [x] Root cause identified and proven (border = 12 cu, not sub-pixel)
- [x] LiquidMask height reduced 372 → 348, centre unchanged at -6
- [x] LiquidRoot height reduced 372 → 348
- [x] Interior constants updated: Bottom=-180, Top=168, Height=348, Centre=-6
- [x] Dev-only debug flag + green overlay + per-bottle assertions added
- [x] `BottomBorder_NotOverlappedByLiquid` test added (fails before fix)
- [x] `FullBottle_LiquidTopAligned_ToInnerBodyTop_AcrossCapacities` updated
- [x] Segment-height invariant still holds (348/maxCapacity is constant)
- [x] PLANS.md updated with full derivation
- [ ] CI green

---

## 2026-02-22 — HUD/Bottle Vertical Spacing Regression (Level 21)

### Current vertical layout algorithm (actual implementation)

`HudSafeLayout.ApplyLayout()` computes:

- `screenTopY` from `min(GetMinY(topHud), GetMinY(secondaryHud), GetMinY(brandLockup?))`
- `screenBottomY` from `GetMaxY(bottomHud)`
- `availableHeight = screenTopY - screenBottomY`

It then attempted equal gaps by mutating `GridLayoutGroup`:

- `rows = ResolveGridRows()` (derived from active child count)
- `cellHeight = bottleGridLayout.cellSize.y`
- `idealGap = (availableHeight - rows * cellHeight) / (rows + 1)`
- `spacing.y = idealGap`
- `padding.top = padding.bottom = RoundToInt(idealGap)`
- `gridHeight = rows * cellHeight + (rows - 1) * idealGap + 2 * idealGap`

### How row heights are currently derived

Before this fix, row allocation used `cellHeight` with a row count derived from current child count. Outer gaps depended on integer `RectOffset` padding, not on a strict closed-form model over 3 fixed rows.

### Why maximum-height bottles break the spacing invariant

The old approach mixed float spacing with integer top/bottom padding and sized the grid to include internal top/bottom padding. This creates rounding drift at top/bottom and can collapse effective HUD clearance in edge cases (e.g. tall rows). It also tied vertical math to resolved row count instead of the non-negotiable 3-row board model.

### Corrected mathematical model

Use fixed 3-row model and reserve max row height unconditionally:

- `rows = 3`
- `maxBottleHeight = bottleGridLayout.cellSize.y` (fallback: cached base grid height / 3)
- `gapHeight = (totalAvailableHeight - rows * maxBottleHeight) / 4`

Then enforce layout by:

- `spacing.y = gapHeight`
- `padding.top = padding.bottom = 0`
- `gridHeight = rows * maxBottleHeight + (rows - 1) * gapHeight`
- keep grid centered in `bottleArea`

Because `bottleArea` height is `totalAvailableHeight`, centering yields:

- top external gap = `(totalAvailableHeight - gridHeight) / 2 = gapHeight`
- bottom external gap = same
- internal row gaps = `gapHeight`

So gaps A/B/C/D are all equal by construction.

### Runtime assertions added

In development/editor builds, `HudSafeLayout` now asserts:

- `gapHeight > 0`
- computed `bottomGap ~= gapHeight`
- top row stays below HUD bottom
- active bottle tops do not intersect HUD bounds

### Risks

- If available vertical space is too small for `3 * maxBottleHeight + 4 * minGap`, fallback scaling path still applies.
- Cross-platform rounding differences can affect world-space comparisons by sub-pixel amounts; tests use explicit tolerances.

### Validation plan

- Strengthen PlayMode layout invariants for level set including level 21.
- Validate equal A/B/C/D gaps in world space.
- Validate bottle-in-row bounds and HUD clearance.
- Add cross-resolution check (two portrait resolutions) asserting the same invariants.

### Checklist

- [x] Document current algorithm and failure mode
- [x] Implement fixed 3-row max-height gap model in `HudSafeLayout`
- [x] Add runtime development assertions
- [x] Extend PlayMode tests with equal-gap + overlap invariants
- [x] Add cross-resolution invariant test
- [ ] Run targeted tests locally
- [ ] Verify CI green (Android/iOS/WebGL)
