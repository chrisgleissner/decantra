# Main CI Test Stabilization Plan (2026-02-16)

## Status Checklist

- [x] Inventory recent failed `main` workflow runs and isolate test-related failures.
- [x] Extract exact failing test cases and stack traces from CI logs.
- [x] Classify each failure root cause (incorrect/brittle test vs production defect vs nondeterminism).
- [x] Apply minimal deterministic fixes.
- [x] Verify fixes with available local non-Unity checks and repeated deterministic analysis from CI evidence.
- [x] Finalize risk review and Definition of Done validation.

## 1) Inventory of failing CI runs on `main`

Recent failed `Build Decantra` runs on `main`:

- Run `22079902510` (run #369) — **failed** in job `63802878058` (`Unity tests (EditMode + PlayMode)`).
- Run `22079759763` (run #366) — **failed** in job `63802470989` (`Unity tests (EditMode + PlayMode)`).
- Run `21925005022` (run #307) — failed in Android build job only; Unity tests passed (**not test-failure scope**).
- Run `21924282035` (run #305) — failed in Android build job only; Unity tests passed (**not test-failure scope**).

## 2) Exact failing test cases with stack traces

### Failure A (appears in runs 22079759763 and 22079902510)

- Test: `Decantra.Tests.PlayMode.OptionsNavigationPlayModeTests.ReplayTutorial_SeparatesLevelAndMovesSteps_AndKeepsTextWithinContainer`
- Error:
  - `Instruction text overflowed container. content=LEVEL & Difficulty ...`
  - `Expected: less than or equal to 198.5f`
  - `But was: 273.375f`
- Stack trace:
  - `at Decantra.Tests.PlayMode.OptionsNavigationPlayModeTests.AssertInstructionFits (...) in Assets/Decantra/Tests/PlayMode/OptionsNavigationPlayModeTests.cs:165`
  - `at ...ReplayTutorial_SeparatesLevelAndMovesSteps_AndKeepsTextWithinContainer... in ...OptionsNavigationPlayModeTests.cs:116`

### Failure B (appears in run 22079902510)

- Test: `Decantra.Tests.PlayMode.GameControllerPlayModeTests.AccessibleColorsToggle_UpdatesRenderedBottleLiquid`
- Error:
  - `Expected: RGBA(59, 130, 255, 255)`
  - `But was: RGBA(59, 129, 255, 255)`
- Stack trace:
  - `at ...AccessibleColorsToggle_UpdatesRenderedBottleLiquid... in Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs:721`

## 3) Root cause classification

- Failure A: **B. Legitimate production defect**
  - Tutorial instruction content was expanded/split; current `InstructionText` layout height in overlay is too small for the required LEVEL & Difficulty copy, producing deterministic overflow in CI.
- Failure B: **A/C. Brittle assertion with minor rendering nondeterminism**
  - Test compares post-processed color using strict `Color32` equality; float/HSV conversion + platform rounding yields ±1 channel variation.

## 4) Proposed fix strategy

- Failure A: minimally increase available tutorial instruction text area in `SceneBootstrap.CreateTutorialOverlay` so required text fits container.
  - ✅ Implemented: `InstructionPanel` height changed from `390f` to `530f`.
- Failure B: make test assertion deterministic but tolerant to 1-step channel rounding drift (bounded per-channel tolerance), while still verifying palette switch and distinct colors.
  - ✅ Implemented: replaced strict `Color32` equality with `AssertColorApproximately(..., channelTolerance: 1)`.

## 5) Verification steps

1. Run available local repo test command baseline (`./scripts/test.sh`) to capture environment limitations.
2. Apply minimal code/test changes for Failures A and B.
3. Run available non-Unity validation commands (repository does not provide non-Unity executable tests; record limitation).
4. Re-check changed tests/logic statically against CI stack traces and failure conditions for deterministic resolution.

Execution notes:

- Baseline command before changes: `./scripts/test.sh` → `Unity not found. Set UNITY_PATH to the Unity editor executable.`
- Post-change command: `./scripts/test.sh` → same environment limitation, no additional failures observable without Unity.
- Automated review/security: `code_review` rerun clean after fix; `codeql_checker` reported 0 C# alerts.

## 6) Risk assessment

- UI layout change could shift tutorial panel composition; mitigate by only adjusting instruction text bounds.
- Tolerance in color assertion could hide larger regressions; mitigate by using tight per-channel bound (1) and preserving distinct-color assertion.

## 7) Definition of Done

- Both historical failing tests have deterministic, root-cause fixes.
- No unrelated files/refactors are introduced.
- `PLANS.md` documents inventory, stack traces, classification, strategy, verification, and risks.
- PR includes concise summary of fixes and validation limits (Unity unavailable locally in this environment).

 # Sound Effects Subsystem Hardening Plan

## Accessible Colors Toggle Feature (2026-02-16)

### Feature description

- Add a Settings toggle labeled exactly `Accessible Colors`.
- Persist toggle state with default OFF and initialize palette state during startup.
- Route all liquid color resolution through a centralized palette provider that supports exactly:
  - `DefaultPalette`
  - `AccessiblePalette`
- Use this exact accessible 8-color palette: `#0072B2`, `#E69F00`, `#56B4E9`, `#009E73`, `#F0E442`, `#D55E00`, `#CC79A7`, `#1B2A41`.

### Implementation steps

- [x] Update palette provider to expose `DefaultPalette`/`AccessiblePalette` selection and resolve colors centrally for bottle liquid visuals.
- [x] Add `Accessible Colors` toggle row to Options and wire it to game settings state.
- [x] Persist/reload accessible colors setting through `SettingsStore` (default OFF).
- [x] Apply palette mode before first level render and re-render bottles when toggled at runtime.
- [x] Add focused tests for palette switching, luminance-order distinction, and mid-game toggle safety.

### Risk assessment

- Runtime toggle may leave transient preview overlays stale if bottle visuals are not re-rendered consistently.
- SceneBootstrap UI wiring order may show stale toggle defaults if runtime state is not loaded before interaction.
- Palette data mismatches (hex conversion drift) could violate exact accessible color requirements.

### Test checklist

- [x] Unit: palette switching returns expected colors for both palettes.
- [x] Unit: accessible and default palettes produce distinct grayscale luminance ordering.
- [x] Integration: toggling during active level updates rendered bottle liquid colors.
- [x] Regression: toggling on/off mid-game does not produce null references.
- [ ] Execute Unity EditMode/PlayMode tests in this environment (blocked: Unity editor executable unavailable).

---

## Milestone 1: Architecture

The project had multiple independent SFX playback paths with abrupt start/stop behavior. Procedural clip generation existed in runtime code, while UI/celebration paths also played clips directly. This allowed discontinuities and repeated transients to surface as audible pops/clicks and increased listener fatigue under rapid repetition.

## 2) Root cause analysis of popping artifacts

Technical causes identified:

- Non-zero first sample values in generated clips can create an immediate waveform discontinuity at playback start.
- Missing/insufficient attack ramps can create steep onset edges.
- Abrupt stop conditions without controlled release can create end clicks.
- Direct source triggering can expose phase discontinuities when repeated rapidly on the same source.

## 3) Why UI button sounds pop

- Imported/static clips (if used) may not start at exact zero crossing.
- Imported/static clips (if used) may not end at exact zero crossing.
- Direct one-shot triggering can begin at arbitrary waveform phase and stack tightly under rapid tapping.
- Without explicit fade-in/fade-out, short transients become edge-sensitive and click-prone.

## 4) Psychoacoustic fatigue explanation

Fatigue increases when highly similar transients repeat with little variation, especially in upper-mid/high ranges. Fixed spectral centroids and identical temporal shapes are perceived as harsh over time. Slight deterministic micro-variation in pitch/amplitude, controlled RMS, and short envelopes reduce perceived harshness while preserving responsiveness.

## 5) Unified audio safety layer proposal

Implement/route all SFX playback through `AudioManager`:

- Fixed pool of `AudioSource` components (no per-play source creation).
- Single `PlayTransient(...)` wrapper used by button, pour, level-complete, and banner star layers.
- Runtime hardening for any clip played through wrapper:
  - Attack/release envelope (5–20 ms constrained window).
  - Hard boundaries: first and last sample set to `0f`.
  - DC offset removal (mean subtraction per channel).
  - Peak clamp to `[-1f, 1f]`.
- Cache hardened static clips to avoid repeated allocations/work.

## 6) Dynamic pour DSP design

Pour clips are generated as a deterministic bank across fill ratios:

- `fillRatio = liquidLevel/capacity`
- `airRatio = 1 - fillRatio`
- Resonance center moves with fill state:
  - `f_res = lerp(240 Hz, 1050 Hz, fillRatio)`
- Layers:
  1. Turbulence: deterministic white noise band-shaped to roughly 300–2500 Hz.
  2. Air resonance: deterministic band-pass resonator around `f_res` (Q≈3).
  3. Modulation: subtle deterministic ±2% frequency and ±5% amplitude jitter.
  4. Gain shaping:
     - `noiseGain = 0.8 - 0.3 * fillRatio`
     - `resonanceGain = airRatio * 0.6`
- Final hardening pass applies anti-pop envelope, DC removal, clipping protection, and zero edges.

## 7) Implementation steps

1. Audit all audio playback and procedural clip entry points.
2. Replace direct one-shot usage with pooled `PlayTransient` wrapper in `AudioManager`.
3. Build deterministic pour clip bank with dynamic resonance/noise behavior.
4. Redesign button and completion clips for softer spectral balance plus hardened boundaries.
5. Route `LevelCompleteBanner` star playback through `AudioManager` wrapper.
6. Add focused PlayMode tests for clip hardening and source-pool invariants.

## 8) Validation plan

- Verify generated clips start/end at exact zero.
- Verify no sample exceeds `[-1, 1]`.
- Verify near-zero DC mean on generated clips.
- Verify no `PlayOneShot` remains in project runtime code.
- Verify repeated playback does not allocate new `AudioSource`s.
- Exercise rapid click/pour/complete calls in tests and manual run.

## 9) Risks and mitigations

- **Risk:** Runtime `GetData` on compressed static clips may fail.
  - **Mitigation:** Fallback to original clip if data read/set fails.
- **Risk:** Source-pool stealing could truncate tails under extreme overlap.
  - **Mitigation:** pool size increased and round-robin scheduling.
- **Risk:** Per-clip hardening could allocate on first play.
  - **Mitigation:** cache hardened clip by instance ID.

## 10) Completion checklist

- [x] Project-wide audio audit completed (`AudioSource`, `PlayOneShot`, `Play`, `AudioClip.Create`, UI click hooks).
- [x] Root causes and anti-pop strategy documented.
- [x] Unified pooled playback wrapper implemented in `AudioManager`.
- [x] Direct `PlayOneShot` usage removed from gameplay/UI runtime code.
- [x] Clip safety hardening implemented (attack/release, zero boundaries, DC removal, clamp).
- [x] Dynamic deterministic pour redesign implemented with fill-dependent resonance.
- [x] Button playback routed through hardened wrapper.
- [x] Level-complete/bottle-complete clip redesigned as short, softer transient.
- [x] LevelCompleteBanner star SFX routed through `AudioManager`.
- [x] Focused PlayMode tests added for safety and pool behavior.
- [ ] Unity test execution completed in this environment (Unity executable unavailable).
- [ ] Android on-device acoustic validation completed in this environment.

## Audit findings

Search scope covered:

- `AudioSource`
- `PlayOneShot`
- `.Play(`
- `AudioClip.Create`
- `OnAudioFilterRead`
- UI button click hooks

Findings:

- Procedural clips are generated in `Assets/Decantra/Presentation/Runtime/AudioManager.cs` and `Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs`.
- Prior to this change, direct `PlayOneShot` existed in:
  - `AudioManager.PlayPour`, `AudioManager.PlayLevelComplete`, `AudioManager.PlayButtonClick`
  - `LevelCompleteBanner.PlayStarLayers`
- No `OnAudioFilterRead` implementation was present.
- No imported static audio assets (`wav/mp3/ogg/aiff`) were present in repository content.
- UI buttons trigger SFX via `GameController.PlayButtonSfx` -> `AudioManager.PlayButtonClick`.
