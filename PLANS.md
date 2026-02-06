# PLANS - Orientation + Rendering Regression (Android)

## Scope
Fix the Android device regression where the portrait-only app starts upside down, shows the logo inverted, then renders a black screen after the splash while a small UI overlay remains visible. Ensure Unity is the single orientation authority, no orientation race exists during surface creation, and rendering remains stable through splash -> gameplay.

## Failure Modes (Observed / Hypothesized)
- FM1: Android manifest resolves `screenOrientation` to reverse portrait, causing upside-down splash on device.
- FM2: Runtime orientation lock triggers an orientation change after splash, leading to a black screen (surface recreation / render pipeline disruption).
- FM3: Multiple orientation authorities (PlayerSettings + AndroidManifest + runtime code) create conflicts/races.
- FM4: Scene bootstrap renders UI but background/game cameras fail after orientation change.

## Mandatory Tests / Assertions (Define Before Fix)
1. EditMode: Orientation authority is single-source in PlayerSettings.
2. EditMode: Custom AndroidManifest does not enforce `android:screenOrientation`.
3. EditMode: PlayerSettings orientation is Portrait-only (no auto-rotate, no upside-down).
4. PlayMode: SceneBootstrap does not force `Screen.orientation` at runtime.
5. Manual device tests (post-fix).
6. Device cold-start, no rotation.
7. Device rotation stress during startup.
8. Splash -> gameplay transition.

## Plan (Ordered, Checkable)
- [x] 1. Git forensics: identify commits in last ~3 days touching PlayerSettings, AndroidManifest, Screen APIs, splash/bootstrap, cameras/canvas.
- [x] 2. Orientation authority audit: PlayerSettings vs AndroidManifest vs runtime code. Record conflicts.
- [x] 3. Runtime audit: locate all `Screen.orientation` / `Screen.autorotate*` usage and call timing.
- [x] 4. Rendering path audit: cameras, canvas render modes, splash -> gameplay path.
- [x] 5. Implement tests/guards for orientation invariants (EditMode + PlayMode).
- [x] 6. Apply fixes to enforce portrait once (Unity/PlayerSettings only) and remove runtime/manifest conflicts.
- [x] 7. Rebuild Android manifest via Unity build to verify `screenOrientation=portrait`.
- [x] 8. Run EditMode + PlayMode tests.
- [ ] 9. Device verification: cold-start, rotation stress, splash->gameplay (emulator screenshots complete; physical device pending).
- [ ] 10. Document root cause + evidence, mark all steps complete.

## Findings Log
- Git forensics summary (last ~3 days).
- 2026-02-05 `462a259` added `ForcePortraitOrientation()` in `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs`.
- 2026-02-04 `31a5ee7` changed `androidAutoRotationBehavior` from `1` -> `0` in `ProjectSettings/ProjectSettings.asset`.
- 2026-02-04 `412e3ef` changed `defaultScreenOrientation` `4` -> `1`, disabled all autorotate except portrait, and set `useOSAutorotation` `1` -> `0`.
- 2026-02-01 `893efa4` / `e0f2769` introduced/edited `Assets/Plugins/Android/AndroidManifest.xml` with `android:screenOrientation=\"portrait\"`.
- Orientation authority audit summary.
- PlayerSettings showed `defaultScreenOrientation: 1`, portrait-only autorotate flags, `useOSAutorotation: 0`, `androidAutoRotationBehavior: 0`.
- Custom manifest explicitly set `android:screenOrientation=\"portrait\"` (duplicate authority).
- Runtime `SceneBootstrap.EnsureScene()` called `Screen.orientation = ScreenOrientation.Portrait` after scene load.
- Generated manifest (pre-fix) showed `android:screenOrientation=\"reversePortrait\"`.
- Runtime audit summary.
- Only `Screen.orientation` usage was in `SceneBootstrap` at `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)`.
- No `Screen.autorotate*` usage elsewhere.
- Rendering path audit summary.
- Main scene only contains `Main Camera`; UI/background/game objects are created in `SceneBootstrap`.
- `SceneBootstrap.EnsureRenderCameras()` creates three cameras with identity rotation; canvases are `ScreenSpaceCamera`.
- Splash is Unity splash; no separate splash scene transition beyond Unity splash -> `Main.unity` -> runtime bootstrap.
- Post-fix validation summary.
- `defaultScreenOrientation` set to `0` (Portrait per Unity `UIOrientation` enum).
- Regenerated manifest now reports `android:screenOrientation=\"portrait\"`.

## Root Cause (Confirmed)
- PlayerSettings `defaultScreenOrientation` was set to `1`, which maps to `UIOrientation.PortraitUpsideDown` in Unity 6. This caused the generated Android manifest to use `reversePortrait`, making the splash/logo render upside down on devices.
- A runtime `Screen.orientation = Portrait` call in `SceneBootstrap` attempted to correct orientation after scene load, which created an orientation change after the splash and led to the black screen on device.
- Orientation enforcement was duplicated across PlayerSettings, custom AndroidManifest, and runtime code, creating a race.

## Evidence Log
- Generated manifest before fix (2026-02-05) showed `android:screenOrientation=\"reversePortrait\"` at `Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/AndroidManifest.xml`.
- Generated manifest after fix (2026-02-06) shows `android:screenOrientation=\"portrait\"` at `Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/AndroidManifest.xml`.
- EditMode + PlayMode tests completed via `tools/test.sh`; coverage gate passed with line coverage 0.913 (min 0.800).
- 2026-02-06: `./build --screenshots` completed (tests + coverage passed, release APK built, Play Store screenshots captured).
- Screenshots captured to `doc/play-store-assets/screenshots/phone` (files `screenshot-01` through `screenshot-09`).
- Emulator `DecantraPhone` used for automated screenshot capture; physical device validation still pending.
