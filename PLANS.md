# PLANS - 0.1.0 Android Release CI Recovery

## Scope
Diagnose and fix the tag-triggered CI failure for Decantra 0.1.0. Iterate release candidate tags until a fully green post-tag workflow is achieved.

## Root Cause (Confirmed)
- **Failing run**: https://github.com/chrisgleissner/decantra/actions/runs/21732854464 (Run #201)
- **Failing job**: Build Android APK/AAB
- **Failing step**: Build Release APK (step #10, 5m 55s, conclusion: failure)
- **Error**: `AndroidBuild: Release signing required but keystore env vars are missing. Expected KEYSTORE_STORE_FILE, KEYSTORE_STORE_PASSWORD, KEYSTORE_KEY_ALIAS.`
- **Secondary symptom**: Version resolved as `0.9.0` (project default) instead of CI-computed `0.1.0`.
- **Root cause**: `game-ci/unity-builder@v4` runs Unity inside a Docker container. Environment variables set via `$GITHUB_ENV` (KEYSTORE_STORE_FILE, VERSION_NAME, VERSION_CODE, etc.) are not forwarded into the container. The C# build method reads `Environment.GetEnvironmentVariable()` which returns null inside the container.

## Fix Applied (0.1.0-RC1)

### Workflow (build.yml)
- [x] Add `androidKeystoreName`, `androidKeystorePass`, `androidKeyaliasName`, `androidKeyaliasPass` inputs to Build Release APK and Build Release AAB steps (uses game-ci built-in signing mechanism which configures PlayerSettings inside the container).
- [x] Pass `-versionName` and `-versionCode` via `customParameters` to both build steps (command-line args are reliably forwarded to Unity).

### C# (AndroidBuild.cs)
- [x] Add early-return in `ConfigureAndroidSigningFromEnv()` to detect signing pre-configured by game-ci (checks `PlayerSettings.Android.useCustomKeystore` and keystore file existence).
- [x] Add `GetCommandLineArg()` helper for parsing Unity command-line arguments.
- [x] Add command-line arg parsing (`-versionName`, `-versionCode`) at top of `ResolveVersionName()` and `ResolveVersionCode()`.

## Execution Log

### Step 1 - Inspect failing CI run
- [x] Obtained failing workflow run URL and logs for tag `0.1.0`.
- [x] Confirmed failure in Build Release APK step (step #10) with keystore env vars missing.
- [x] Confirmed version mismatch (0.9.0 vs 0.1.0) from same root cause.

### Step 2 - Minimal fix
- [x] Applied workflow changes to pass signing config and version via game-ci inputs and customParameters.
- [x] Applied C# changes for pre-configured signing detection and command-line version parsing.

### Step 3 - Commit and tag RC
- [ ] Create atomic commit referencing the failing CI step.
- [ ] Create 0.1.0-RC1 tag and push.

### Step 4 - Observe post-tag CI
- [ ] Verify all four stages succeed: license setup, tests, Android build, GitHub Release.
- [ ] If any stage fails, loop with new evidence.

### Step 5 - Final tag
- [ ] Once RC tag is fully green, retag `0.1.0` and push.
- [ ] Confirm GitHub Release exists with APK/AAB artifacts.

## Evidence Log
- Failing run URL: https://github.com/chrisgleissner/decantra/actions/runs/21732854464
- Failing job/step: Build Android APK/AAB / Build Release APK (step #10)
- Error output: `AndroidBuild: Release signing required but keystore env vars are missing.`
- Fix summary: Use game-ci built-in androidKeystore* inputs + pass version via customParameters; C# detects pre-configured signing and parses CLI args for version
- Commit: (pending)
- RC tag(s): (pending)
- Final tag: (pending)
- Release URL: (pending)
