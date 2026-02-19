# PLANS

Last updated: 2026-02-19 (UTC)
Owner: GitHub Copilot (GPT-5.3-Codex)
Objective: Production-ready iOS simulator build + Maestro CI pipeline on GitHub Actions (Unity project: Decantra/Cantra)

## Execution Status Summary

- Phase 1 (Research & gap analysis): **Completed**
- Phase 2 (Unity iOS build configuration): **Completed (implementation), CI run pending**
- Phase 3 (GitHub Actions macOS pipeline): **Completed (implementation), CI run pending**
- Phase 4 (Maestro integration): **Completed (implementation), CI run pending**
- Phase 5 (Stabilization): **Not started**
- Phase 6 (Hardening/docs): **In progress**
- CI state target: **Green**

---

## Phase 1 — Research and Gap Analysis

### Tasks (Phase 1)

- [x] Inspect Unity project structure and build entry points.
- [x] Identify Unity version, Android build pipeline, and existing CI workflows.
- [x] Inspect Player Settings for iOS readiness and simulator constraints.
- [x] Inspect plugin/package landscape for iOS blockers.
- [x] Study C64 Commander iOS + Maestro workflow patterns for simulator setup and test execution.
- [x] Finalize architecture decisions and concrete change list.

### Findings (Evidence)

1. **Unity version**
   - `ProjectSettings/ProjectVersion.txt`: `6000.3.5f2`.

2. **Current CI**
   - Existing workflow: `.github/workflows/build.yml`.
   - Contains:
     - Unity license gate (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).
     - PlayMode/EditMode test jobs using `game-ci/unity-test-runner@v4`.
     - Android build job using `game-ci/unity-builder@v4` and build method `Decantra.App.Editor.AndroidBuild.*`.
   - No iOS workflow currently.

3. **Current build automation**
   - Android editor build script exists: `Assets/Decantra/App/Editor/AndroidBuild.cs`.
   - No iOS editor build script currently in project.

4. **iOS project configuration present**
   - `ProjectSettings/ProjectSettings.asset` has iOS application identifier and minimum OS target configured:
     - `applicationIdentifier.iPhone: uk.gleissner.decantra`
     - `iOSTargetOSVersionString: 15.0`
   - Signing fields present but empty/manual by default; simulator builds can use `CODE_SIGNING_ALLOWED=NO` in Xcode build step.

5. **Packages/plugins**
   - `Packages/manifest.json` is Unity-first and does not show obvious iOS blockers.
   - Mobile Dependency Resolver present; no immediate hard block detected.

6. **Reference pattern (C64 Commander)**
   - Uses macOS runners, builds simulator app via `xcodebuild -sdk iphonesimulator` with `CODE_SIGNING_ALLOWED=NO`.
   - Boots simulator via `xcrun simctl`, installs app, runs Maestro CLI.
   - Archives artifacts and logs.

### Gap List

- Missing iOS Unity build entry point (`BuildTarget.iOS`, Xcode export path).
- Missing iOS GitHub Actions workflow.
- Missing Maestro test folder and iOS flow(s) for this repo.
- Missing simulator orchestration + app install + test artifact upload in CI.
- Missing docs for iOS pipeline and required secrets.

### Validation (Phase 1)

- A clear list of required changes is documented.
- CI architecture decision is documented and executable.
- Unity/macOS compatibility assumptions are explicitly stated.

### Risks & Mitigations

- Risk: Unity iOS module availability mismatch on runner.
  - Mitigation: use `game-ci/unity-builder@v4` on `macos-latest` with pinned Unity version from project.
- Risk: iOS simulator runtime mismatch.
  - Mitigation: discover available runtime/device dynamically with `simctl list -j` and boot accordingly.
- Risk: Unity UI discoverability by Maestro selectors.
  - Mitigation: coordinate-driven interaction + deterministic waits + screenshot/log evidence assertions.

---

## Phase 2 — Unity iOS Build Configuration

### Tasks (Phase 2)

- [x] Add iOS editor build script in `Assets/Decantra/App/Editor`.
- [x] Configure iOS build method for simulator Xcode project export.
- [x] Ensure deterministic CLI args for output path and versioning.
- [x] Ensure build exits non-zero on failure.

### Evidence (Phase 2)

- Added `Assets/Decantra/App/Editor/IosBuild.cs` with build method `Decantra.App.Editor.IosBuild.BuildSimulatorXcodeProject`.
- Build method exports `BuildTarget.iOS` Xcode project to configurable `-buildPath` (default `Builds/iOS/Xcode`).
- Versioning reads `VERSION_NAME` and `VERSION_CODE`/`GITHUB_RUN_NUMBER`.
- Build failure throws exception for deterministic non-zero CI failure.

### Validation (Phase 2)

- CI can call Unity build method headlessly.
- Xcode project artifact is generated at expected path.
- Unity logs show platform switch/build success without iOS platform errors.

---

## Phase 3 — GitHub Actions macOS Pipeline

### Tasks (Phase 3)

- [x] Add `.github/workflows/ios.yml`.
- [x] Gate execution on Unity license secrets.
- [x] Build Unity iOS Xcode project on macOS runner.
- [x] Build simulator `.app` via `xcodebuild` with no signing required.
- [x] Upload Unity and Xcode artifacts.

### Evidence (Phase 3)

- New workflow: `.github/workflows/ios.yml`.
- Uses `macos-latest` and Unity version `6000.3.5f2`.
- Unity export step uses `game-ci/unity-builder@v4` with build method `Decantra.App.Editor.IosBuild.BuildSimulatorXcodeProject`.
- Xcode simulator build step uses `CODE_SIGNING_ALLOWED=NO` and uploads simulator `.app` artifact.

### Validation (Phase 3)

- Workflow produces simulator `.app` artifact.
- Xcode build completes with `CODE_SIGNING_ALLOWED=NO`.
- Job exits cleanly.

---

## Phase 4 — Maestro Integration

### Tasks (Phase 4)

- [x] Add Maestro config and iOS test flow(s) under `.maestro/`.
- [x] Install Maestro CLI in workflow.
- [x] Boot iOS simulator and install built app with `simctl`.
- [x] Run Maestro tests against simulator.
- [x] Archive Maestro logs/screenshots/artifacts.

### Evidence (Phase 4)

- Added `.maestro/ios-cantra-smoke.yaml`.
- Workflow installs Maestro, boots simulator, installs app, launches with `decantra_quiet decantra_ci_probe`.
- Added development-only CI probe logging in `GameController` for `POUR_STARTED` and `POUR_COMPLETED`.
- Workflow validates probe log after Maestro run and uploads Maestro artifacts.

### Validation (Phase 4)

- Maestro exits code `0`.
- Evidence artifacts uploaded.
- Flow covers: app launch, level context, at least one bottle interaction, and expected state assertion.

---

## Phase 5 — Stabilization

### Tasks (Phase 5)

- [ ] Address timing/race issues in flow or launch sequencing.
- [ ] Tune waits/retries for Unity startup and animation windows.
- [ ] Achieve two consecutive green iOS CI runs.

### Validation (Phase 5)

- No intermittent failures in repeated runs.
- iOS workflow remains green on rerun.

---

## Phase 6 — Hardening and Documentation

### Tasks (Phase 6)

- [x] Add safe caches (Library/maestro where appropriate).
- [x] Add artifact retention and useful debug exports.
- [x] Update `README.md` with iOS + Maestro CI usage.
- [ ] Verify Android workflow remains unaffected.

### Validation (Phase 6)

- Android workflow remains unchanged/green.
- iOS workflow documented and reproducible from repo docs.
- No open high-risk blockers.

---

## CI Architecture Decision (Current)

1. Unity iOS export on macOS GitHub Actions using Unity build method.
2. Xcode simulator build (`iphonesimulator`) from exported project with signing disabled.
3. Simulator boot and app install via `xcrun simctl`.
4. Maestro CLI executes iOS flow(s) against simulator.
5. Upload artifacts: Xcode logs, simulator app, Maestro outputs.

This architecture avoids local macOS dependencies and is fully CI-driven.

## Current Blockers

- Remote CI validation is pending because workflow execution requires these uncommitted changes to be pushed to GitHub first.
- Once pushed, run `.github/workflows/ios.yml` at least twice consecutively to complete Phase 5 stability criteria.

## Scope Extension (2026-02-19)

- Added release artifact naming requirement in main build workflow:
  - `decantra-android-$version.apk`
  - `decantra-android-play-$version.aab`
  - `decantra-ios-$version.ipa`
- Implemented `$version` policy:
  - Tagged build: tag name.
  - Non-tag build: latest tag + `-` + first 8 chars of git SHA.
- Constraint retained: Google Play AAB upload must continue to work.

---

## Execution Journal (UTC)

- 2026-02-19T00:00:00Z — Started iOS production pipeline task.
- 2026-02-19T00:05:00Z — Audited Unity version (`6000.3.5f2`) and current CI (`build.yml`).
- 2026-02-19T00:10:00Z — Verified existing Android build entry points and absence of iOS build script.
- 2026-02-19T00:15:00Z — Verified iOS Player settings baseline and simulator-signing strategy viability.
- 2026-02-19T00:20:00Z — Inspected C64 Commander iOS workflow patterns (simulator + Maestro structure).
- 2026-02-19T00:25:00Z — Replaced `PLANS.md` with this authoritative iOS execution plan.
- 2026-02-19T00:35:00Z — Implemented Unity iOS build automation at `Assets/Decantra/App/Editor/IosBuild.cs`.
- 2026-02-19T00:40:00Z — Added workflow `.github/workflows/ios.yml` (Unity export, Xcode simulator build, Maestro test, artifact upload).
- 2026-02-19T00:45:00Z — Added Maestro flow `.maestro/ios-cantra-smoke.yaml` for iOS simulator smoke interaction.
- 2026-02-19T00:48:00Z — Added development-only CI probe instrumentation in `GameController` and probe validation step in workflow.
- 2026-02-19T00:52:00Z — Updated README with iOS CI pipeline and required Unity secrets.
- 2026-02-19T00:55:00Z — Local static checks passed for changed C# and workflow files; remote GitHub Actions execution pending.
- 2026-02-19T01:00:00Z — Verified GitHub CLI authentication is available; remaining step is commit/push + workflow runs to reach green CI.
- 2026-02-19T01:10:00Z — Updated `.github/workflows/build.yml` to use versioned Android filenames and keep Play upload path working.
- 2026-02-19T01:14:00Z — Added `build-ios` job in `build.yml` to export iOS device Xcode project and package `decantra-ios-$version.ipa`.
- 2026-02-19T01:18:00Z — Extended `Assets/Decantra/App/Editor/IosBuild.cs` with `BuildDeviceXcodeProject` for IPA pipeline.
