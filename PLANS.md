# PLANS

Last updated: 2026-02-20 UTC (execution in progress)  
Execution engineer: GitHub Copilot (GPT-5.3-Codex)

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
- Found iOS Maestro flow: `.maestro/ios-cantra-smoke.yaml`.
- Found no Web workflow and no Playwright harness yet.

### 2026-02-19 — Implementation completed locally

- Added Unity WebGL build entry point: `Assets/Decantra/App/Editor/WebGlBuild.cs` (`Decantra.App.Editor.WebGlBuild.BuildRelease`).
- Added Web CI workflow `.github/workflows/web.yml` for deterministic Unity WebGL build, output verification, Playwright smoke run, and GitHub Pages deployment from the same artifact.
- Added Playwright smoke harness files: `tests/web-smoke/package.json`, `tests/web-smoke/package-lock.json`, `tests/web-smoke/playwright.config.ts`, `tests/web-smoke/web.smoke.spec.ts`.
- Hardened iOS workflow `.github/workflows/ios.yml` with deterministic versioning for simulator/IPA jobs, a dedicated `build-ios-ipa` path, and artifact size checks.
- Hardened iOS Maestro flow `.maestro/ios-cantra-smoke.yaml` with explicit launch, menu/board waits, two bottle taps, and board-visibility assertion.

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
