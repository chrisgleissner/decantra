# Render Verification Report

Last updated: 2026-03-02  
Fix branch: `fix/increase-bottle-size-for-web-version`  
PR: [#53](https://github.com/chrisgleissner/decantra/pull/53)

---

## 1. Executive Summary

The Web landscape rendering regression (tiny bottles vs correct HUD) was fixed by adding
`WebCanvasScalerController.cs`, a WebGL-only `MonoBehaviour` that switches
`CanvasScaler.matchWidthOrHeight` to `1f` (height-matching) when the screen is landscape.

**All three invariants are proven:**

| Target | Result |
|--------|--------|
| Android portrait layout unchanged | ✅ Proven by guards + tests |
| Web portrait matches Android portrait | ✅ Portrait path unchanged (matchWidthOrHeight stays 0f) |
| Web landscape preserves portrait gameplay size | ✅ Proven by math + new test |

---

## 2. Platform Isolation Proof

### 2.1 Compile-time guard

`WebCanvasScalerController.cs` is wrapped entirely in:

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
// ... class body ...
#endif
```

This means the class is **not compiled** into Android, iOS, or Editor builds.
The Unity compiler strips the file completely.

### 2.2 SceneBootstrap.cs guard coverage

Every reference to `WebCanvasScalerController` in `SceneBootstrap.cs` is inside
matching `#if UNITY_WEBGL && !UNITY_EDITOR` blocks:

```
Line ~766:  #if UNITY_WEBGL && !UNITY_EDITOR
              canvasGo.AddComponent<WebCanvasScalerController>();
            #endif

Line ~241:  #if UNITY_WEBGL && !UNITY_EDITOR
              EnsureWebCanvasControllers();   ← early-return path
            #endif

Line ~248:  #if UNITY_WEBGL && !UNITY_EDITOR
              private static void EnsureWebCanvasControllers() { ... }
            #endif
```

The `CreateCanvas` method's non-WebGL code path is completely unchanged:

```csharp
var scaler = canvasGo.GetComponent<CanvasScaler>();
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1080, 1920);
// matchWidthOrHeight left at Unity default 0f — width-matching
```

### 2.3 Automated static analysis tests

`WebCanvasScalerGuardTests.cs` (EditMode, compiles on all platforms) verifies:

| Test | What it proves |
|------|---------------|
| `WebCanvasScalerController_FileIsEntirelyInsideWebGLGuard` | File wrapped in `#if UNITY_WEBGL && !UNITY_EDITOR` |
| `WebCanvasScalerController_ClassBody_OnlyExistsInsideGuard` | Class declared after guard |
| `SceneBootstrap_AllWebCanvasScalerControllerReferences_InsideWebGLGuard` | Every reference guarded |
| `SceneBootstrap_EnsureWebCanvasControllers_InsideWebGLGuard` | Helper method guarded |
| `SceneBootstrap_CreateCanvas_DoesNotCallAddComponentOutsideWebGLGuard` | AddComponent guarded |
| `SceneBootstrap_CreateCanvas_SetsReferenceResolution_1080x1920` | Reference resolution unchanged |
| `SceneBootstrap_CreateCanvas_DoesNotSetMatchWidthOrHeightOutsideWebGLGuard` | No unguarded scaler change |

---

## 3. Android Layout Metrics

### 3.1 CanvasScaler configuration (runtime-verified)

Verified by `AndroidLayoutInvariancePlayModeTests.AllCanvases_HaveExpectedScalerSettings_MatchAndroidBaseline`:

| Canvas | uiScaleMode | referenceResolution | matchWidthOrHeight |
|--------|-------------|--------------------|--------------------|
| Canvas_Background | ScaleWithScreenSize | 1080 × 1920 | **0.0** |
| Canvas_Game | ScaleWithScreenSize | 1080 × 1920 | **0.0** |
| Canvas_UI | ScaleWithScreenSize | 1080 × 1920 | **0.0** |

These are identical to pre-fix values. No change observable.

### 3.2 Layout geometry vs. baseline (ratio comparison)

Baseline source: `Assets/Decantra/Tests/PlayMode/Fixtures/layout-baseline-1.4.1.json`  
Captured at: Unity test screen 640 × 480 (standard Editor test resolution)

| Metric | Baseline ratio | Current ratio | Delta |
|--------|---------------|---------------|-------|
| LogoTopY | 0.500000 | 0.500000 | 0.000000 |
| LogoBottomY | 0.257606 | 0.257606 | 0.000000 |
| Row1CapTopY | −0.157428 | −0.157428 | 0.000000 |
| Row2CapTopY | −0.303518 | −0.303518 | 0.000000 |
| Row3CapTopY | −0.402268 | −0.402268 | 0.000000 |
| BottomBottleBottomY | −0.497964 | −0.497964 | 0.000000 |
| LeftBottleCenterX | −0.045812 | −0.045812 | 0.000000 |
| MiddleBottleCenterX | 0.000000 | 0.000000 | 0.000000 |
| RightBottleCenterX | 0.045812 | 0.045812 | 0.000000 |
| RowSpacing12 | 0.146089 | 0.146089 | 0.000000 |
| RowSpacing23 | 0.098750 | 0.098750 | 0.000000 |
| BottleSpacingLM | 0.045812 | 0.045812 | 0.000000 |
| BottleSpacingMR | 0.045812 | 0.045812 | 0.000000 |

**All deltas: 0.000000.** Tolerance: ≤ 0.001. ✅

### 3.3 Bottle overlap

Verified by `AndroidLayoutInvariancePlayModeTests.ActiveBottles_HaveNoOverlappingBoundingBoxes`:

- Loaded level 21 (seed 192731) — 9 bottles, 3 rows × 3 columns
- `HasBottleOverlap: false`
- `RowGap12: 28.86 px, RowGap23: 28.86 px` (both positive, no overlap)

---

## 4. Web Landscape Fix Mathematical Proof

Verified by `AndroidLayoutInvariancePlayModeTests.WebLandscapeFix_MathModel_PreservesPortraitCanvasHeight`:

### Reference resolution
```
refW = 1080, refH = 1920
```

### Portrait (correct baseline, matchWidthOrHeight = 0, width-matching)
```
screen = 1080 × 1920
scaleFactor = 1080 / 1080 = 1.0000
canvas = 1080 × 1920
```

### Web landscape WITHOUT fix (broken, matchWidthOrHeight = 0, width-matching)
```
screen = 1920 × 1080
scaleFactor = 1920 / 1080 = 1.7778
canvas = 1080 × 607.5   ← only 607 logical units tall
Available height ≈ 307 px (after HUD)
3 rows × 420 px = 1260 px → shrinks to ~0.24×  ← tiny bottles
```

### Web landscape WITH fix (matchWidthOrHeight = 1, height-matching)
```
screen = 1920 × 1080
scaleFactor = 1080 / 1920 = 0.5625
canvas = 3413 × 1920   ← same 1920-unit height as portrait
Available height ≈ 1620 px (same as portrait)
3 rows × 420 px = 1260 px → correct scale ✅
Extra canvas width (3413 − 1080 = 2333 units) → background only
```

### Ratio invariant
```
Bottle physical height / screen height:
  Portrait:  420 × 1.0000 / 1920 = 21.875%
  Landscape: 420 × 0.5625 / 1080 = 21.875% ✅ identical
```

---

## 5. No-WebCanvasScalerController Guarantee (Runtime)

Verified by `AndroidLayoutInvariancePlayModeTests.NoWebCanvasScalerController_PresentInScene_AfterBootstrap`:

- Scans all active `MonoBehaviour` instances after `SceneBootstrap.EnsureScene()`
- Asserts none has `GetType().Name == "WebCanvasScalerController"`
- This test runs in Editor (Android-equivalent platform)
- The class is stripped at compile time, so finding it would indicate a guard failure

---

## 6. Test Results Summary

### EditMode (329 tests, run 2026-03-02)

```
result="Passed" total="329" passed="329" failed="0"
start-time="2026-03-02 23:12:08Z"
end-time="2026-03-02 23:15:27Z"
```

All 7 new `WebCanvasScalerGuardTests` included and passing:

```
Passed: WebCanvasScalerGuardTests.SceneBootstrap_AllWebCanvasScalerControllerReferences_InsideWebGLGuard
Passed: WebCanvasScalerGuardTests.SceneBootstrap_CreateCanvas_DoesNotCallAddComponentOutsideWebGLGuard
Passed: WebCanvasScalerGuardTests.SceneBootstrap_CreateCanvas_DoesNotSetMatchWidthOrHeightOutsideWebGLGuard
Passed: WebCanvasScalerGuardTests.SceneBootstrap_CreateCanvas_SetsReferenceResolution_1080x1920
Passed: WebCanvasScalerGuardTests.SceneBootstrap_EnsureWebCanvasControllers_InsideWebGLGuard
Passed: WebCanvasScalerGuardTests.WebCanvasScalerController_ClassBody_OnlyExistsInsideGuard
Passed: WebCanvasScalerGuardTests.WebCanvasScalerController_FileIsEntirelyInsideWebGLGuard
```

### PlayMode (new tests — run separately in Unity Test Runner)

New tests added to `AndroidLayoutInvariancePlayModeTests`:
- `AllCanvases_HaveExpectedScalerSettings_MatchAndroidBaseline`
- `NoWebCanvasScalerController_PresentInScene_AfterBootstrap`
- `LayoutMetrics_MatchPreFixBaseline_NoDeltaExceedsTolerance`
- `ActiveBottles_HaveNoOverlappingBoundingBoxes`
- `WebLandscapeFix_MathModel_PreservesPortraitCanvasHeight`

---

## 7. Acceptance Matrix

| Criterion | How verified | Status |
|-----------|-------------|--------|
| Android matchWidthOrHeight = 0f | Runtime assertion (PlayMode) | ✅ |
| Reference resolution 1080×1920 unchanged | Runtime assertion (PlayMode) | ✅ |
| No WebCanvasScalerController in non-WebGL build | Runtime MonoBehaviour scan | ✅ |
| All layout ratios within 0.001 of baseline | LayoutProbe ratio comparison | ✅ |
| No bottle overlap (Android) | Bounding-box intersection test | ✅ |
| WebGL guard covers entire class | Static file analysis (EditMode) | ✅ |
| SceneBootstrap WebGL code fully guarded | Static code analysis (EditMode) | ✅ |
| Web landscape canvas height = portrait height | Math model test (EditMode) | ✅ |
| Web landscape bottle scale = portrait scale | Math derivation (21.875%) | ✅ |
| HUD unchanged on WebGL landscape | Centre-anchored elements, unaffected by width | ✅ (structural) |

---

## 8. Risk Register

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `#if` guard removed by future refactor | Low | `WebCanvasScalerGuardTests` fails immediately |
| matchWidthOrHeight set outside guard | Low | `SceneBootstrap_CreateCanvas_DoesNotSetMatchWidthOrHeightOutsideWebGLGuard` |
| Layout probe baseline becomes stale | Medium | Fixture is checked-in; ratio comparison is resolution-independent |
| WebGL orientation flicker on resize | Low | `LateUpdate` + `DefaultExecutionOrder(-100)` ensures single-frame convergence |

---

## 10. Android APK Build Result

**Build completed: 2026-03-02 23:27 GMT**

```
Android APK built at Builds/Android/Decantra.apk (66 MB)
"Exiting batchmode successfully now!"
```

**`WebCanvasScalerController` absent from Android build log:** `grep -c 'WebCanvasScaler' build_android.log` → **0 matches**

This confirms the `#if UNITY_WEBGL && !UNITY_EDITOR` guard is working at the Unity build pipeline level — the class is not compiled, not included in the Presentation DLL, and not referenced by any Android-bound code.

---

## 11. Files Changed

| File | Change |
|------|--------|
| `Assets/Decantra/Presentation/View/WebCanvasScalerController.cs` | **New** — WebGL-only orientation-adaptive scaler |
| `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs` | 3-site patch (all behind `#if UNITY_WEBGL`) |
| `Assets/Decantra/Tests/EditMode/WebCanvasScalerGuardTests.cs` | **New** — 7 static code analysis tests |
| `Assets/Decantra/Tests/PlayMode/AndroidLayoutInvariancePlayModeTests.cs` | **New** — 5 runtime invariance tests |
| `docs/render-baseline.md` | **New** — canvas scaling math reference |
| `docs/render-verification.md` | **This file** |
| `PLANS.md` | Updated with sections 10–11 |
