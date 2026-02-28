# PLANS

Last updated: 2026-02-28 UTC  
Execution engineer: GitHub Copilot (GPT-5.3-Codex)

## Scope

Two active tracks are being handled in one workspace:

1. Tutorial overlay/rendering regression hardening (WebGL/Editor screenshot path).
2. iOS production issues audit + completion:
   - No audio on iOS.
   - Home-screen app name incorrectly shown as `Cantra` instead of `Decantra`.

## Invariants

- No gameplay-rule changes.
- No progress reset or save-data migration side effects.
- iOS bundle id remains `uk.gleissner.decantra`.
- iOS signing/provisioning unchanged.
- Changes remain minimal and CI-compatible.

## Root Cause Hypotheses (iOS)

1. iOS display name regression caused by hardcoded `PlayerSettings.productName = "Cantra"` and workflow artifact path assumptions using `Cantra.app` / `Cantra.xcarchive`.
2. iOS silent/no-audio caused by missing explicit `AVAudioSession` setup on runtime start/focus-resume, allowing session category/state to remain incompatible for expected SFX playback.

## Diagnostics Performed

- Searched for `Cantra` references and validated runtime/build paths.
- Audited iOS build entrypoint: `Assets/Decantra/App/Editor/IosBuild.cs`.
- Audited runtime audio path: `Assets/Decantra/Presentation/Runtime/AudioManager.cs`.
- Audited native iOS bridge: `Assets/Plugins/iOS/DecantraAudioSession.mm`.
- Audited workflow paths in `.github/workflows/ios.yml` and `.github/workflows/build.yml`.
- Added/extended EditMode tests for iOS config regressions.

## Implementation Status

### iOS app-name fix

- Implemented:
  - `IosBuild` now uses constant `IosProductName = "Decantra"`.
  - Post-export plist enforcement writes both `CFBundleDisplayName` and `CFBundleName`.
  - iOS workflow/build archive/app path references switched from `Cantra` to `Decantra`.
- Guardrails:
  - EditMode tests verify product name constant and plist-enforcement code.
  - EditMode tests verify `ProjectSettings.asset` product name is `Decantra`.

### iOS audio fix

- Implemented:
  - `AudioManager` calls iOS session config during `Awake`, app focus return, and unpause.
  - Native bridge `DecantraConfigureAudioSession()` sets category (`Playback` when forced), enables `MixWithOthers`, activates session.
- Guardrails:
  - EditMode tests verify iOS bridge symbols/config hook exist in C# and native plugin file exists with `AVAudioSessionCategoryPlayback`.

## Risk Register

- **Risk:** Static tests may pass even if runtime iOS behavior still fails on specific OS/device combinations.  
  **Mitigation:** Keep iOS simulator/device smoke checks in CI and inspect runtime logs during iOS runs.
- **Risk:** `MixWithOthers` policy may not match future product audio policy.  
  **Mitigation:** category/options are isolated in a single native function for easy adjustment.

## Test Plan

1. Run EditMode tests (includes iOS config tests).
2. Keep existing iOS CI workflows as source of simulator/device packaging validation.
3. Runtime iOS verification checklist (required outside Linux workspace):
   - Fresh install: pouring plays audio.
   - Hardware silent switch ON/OFF behavior is as expected.
   - Resume from background preserves audio playback.
   - Installed icon label reads `Decantra`.

## Verification Evidence (this run)

- Code-level verification completed for:
  - iOS product/display-name configuration.
  - iOS audio-session runtime configuration hooks.
  - iOS native bridge presence and category setup.
- Remaining external verification (not executable on Linux runner):
  - Device/simulator audible output confirmation.
  - Installed SpringBoard label visual confirmation.

## Rollback Strategy

- Revert iOS-specific files only:
  - `Assets/Decantra/App/Editor/IosBuild.cs`
  - `Assets/Decantra/Presentation/Runtime/AudioManager.cs`
  - `Assets/Plugins/iOS/DecantraAudioSession.mm`
  - `.github/workflows/ios.yml`
  - `.github/workflows/build.yml`
  - `Assets/Decantra/Tests/EditMode/IosBuildConfigurationTests.cs`

## Definition of Done (iOS track)

- [x] Hardcoded `Cantra` removed from iOS build/runtime configuration.
- [x] `CFBundleDisplayName` and `CFBundleName` enforced to `Decantra` in exported iOS plist.
- [x] iOS audio-session configuration invoked at startup/focus/unpause.
- [x] Native iOS bridge exists and sets/activates `AVAudioSession`.
- [x] EditMode guard tests added/updated for app name + iOS audio bridge wiring.
- [ ] iOS simulator/device run confirms audible pouring.
- [ ] iOS install confirms icon label exactly `Decantra`.
- [ ] iOS CI run observed green for this exact revision.
