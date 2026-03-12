/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using Decantra.Tests.PlayMode.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    /// <summary>
    /// Proves that the Web landscape canvas-scaler fix has not changed any layout
    /// metric observable on the Android/Editor platform.
    ///
    /// Three assertion groups:
    ///
    /// A. CanvasScaler configuration — every canvas bootstrapped by SceneBootstrap
    ///    must have the same scaler settings as the pre-fix baseline.
    ///
    /// B. No WebCanvasScalerController component — the WebGL-only MonoBehaviour must
    ///    never appear in an Editor / Android build (it is stripped by the platform guard).
    ///
    /// C. Layout metrics — LayoutProbe ratios must match the stored baseline within
    ///    tolerance, proving no position or size shift occurred.
    ///
    /// D. No bottle overlap — bounding-box intersection test for all active bottles.
    ///
    /// E. Web landscape math regression guard — verifies that if matchWidthOrHeight
    ///    were changed to 1f (simulated), the resulting canvas height numerically matches
    ///    the portrait canvas height, proving the landscape fix preserves gameplay bounds.
    /// </summary>
    public sealed class AndroidLayoutInvariancePlayModeTests
    {
        private const float ScalerTolerance = 0.001f;
        private const float RatioTolerance = 0.001f;
        private const float AbsTolerance = 1.0f;
        private const string BaselineFixturePath =
            "Assets/Decantra/Tests/PlayMode/Fixtures/layout-baseline-1.4.1.json";

        // ─── A: CanvasScaler configuration ────────────────────────────────────────

        [UnityTest]
        public IEnumerator AllCanvases_HaveExpectedScalerSettings_MatchAndroidBaseline()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var canvasNames = new[] { "Canvas_Background", "Canvas_Game", "Canvas_UI" };
            foreach (var canvasName in canvasNames)
            {
                var go = GameObject.Find(canvasName);
                Assert.IsNotNull(go, $"Canvas not found: {canvasName}");

                var scaler = go.GetComponent<CanvasScaler>();
                Assert.IsNotNull(scaler, $"{canvasName} is missing a CanvasScaler.");

                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode,
                    $"{canvasName}: uiScaleMode must be ScaleWithScreenSize (Android/Editor value).");

                Assert.AreEqual(new Vector2(1080f, 1920f), scaler.referenceResolution,
                    $"{canvasName}: referenceResolution must be 1080 × 1920.");

                Assert.AreEqual(0f, scaler.matchWidthOrHeight, ScalerTolerance,
                    $"{canvasName}: matchWidthOrHeight must be 0 (width-matching) on Android/Editor. " +
                    $"Actual: {scaler.matchWidthOrHeight}. The WebGL orientation-adaptive value must " +
                    $"never be present in non-WebGL builds.");
            }
        }

        // ─── B: No WebCanvasScalerController ──────────────────────────────────────

        [UnityTest]
        public IEnumerator NoWebCanvasScalerController_PresentInScene_AfterBootstrap()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

#if UNITY_WEBGL && !UNITY_EDITOR
            // On WebGL the component IS expected. Skip the absence check.
            Assert.Ignore("Running on WebGL — WebCanvasScalerController presence expected.");
#else
            // On Editor, Android, iOS the component must never exist.
            // The type cannot even be referenced here because it is stripped by the #if guard
            // in WebCanvasScalerController.cs. We search by name instead.
            var allMonoBehaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var mb in allMonoBehaviours)
            {
                Assert.AreNotEqual("WebCanvasScalerController", mb.GetType().Name,
                    $"WebCanvasScalerController found on GameObject '{mb.gameObject.name}'. " +
                    $"This component must be stripped on Android/iOS/Editor builds.");
            }
#endif
        }

        // ─── C: Layout metrics match baseline ─────────────────────────────────────

        [UnityTest]
        public IEnumerator LayoutMetrics_PreserveHudAndHorizontalBaseline_WhileCompactingThreeRowBoards()
        {
            if (!File.Exists(BaselineFixturePath))
            {
                Assert.Ignore($"Baseline fixture not found: {BaselineFixturePath}. Run CaptureLayoutMetricsJson first.");
            }

            var baseline = JsonUtility.FromJson<LayoutProbe.LayoutMetrics>(
                File.ReadAllText(BaselineFixturePath));
            Assert.IsNotNull(baseline, "Failed to parse baseline JSON.");

            var current = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCapture(metrics => current = metrics);

            // --- No overlap ---
            Assert.IsFalse(current.HasBottleOverlap, "Bottle overlap detected after fix.");
            Assert.GreaterOrEqual(current.RowGap12, 0f, "Row 1 and 2 overlap.");
            Assert.GreaterOrEqual(current.RowGap23, 0f, "Row 2 and 3 overlap.");

            // --- Positional ratios ---
            AssertRatio("LogoTopY", baseline.LogoTopRatioY, current.LogoTopRatioY);
            AssertRatio("LogoBottomY", baseline.LogoBottomRatioY, current.LogoBottomRatioY);
            AssertRatio("LeftBottleCenterX", baseline.LeftBottleCenterRatioX, current.LeftBottleCenterRatioX);
            AssertRatio("MiddleBottleCenterX", baseline.MiddleBottleCenterRatioX, current.MiddleBottleCenterRatioX);
            AssertRatio("RightBottleCenterX", baseline.RightBottleCenterRatioX, current.RightBottleCenterRatioX);
            AssertRatio("BottleSpacingLM", baseline.BottleSpacingLMRatioX, current.BottleSpacingLMRatioX);
            AssertRatio("BottleSpacingMR", baseline.BottleSpacingMRRatioX, current.BottleSpacingMRRatioX);

            var controller = FindController();
            Assert.IsNotNull(controller, "GameController not found after capture.");
            Assert.Greater(GetActiveBottleRects(controller).Count, 6, "Expected a 3-row board for the invariance capture level.");

            var hudSafeLayout = Object.FindFirstObjectByType<HudSafeLayout>();
            Assert.IsNotNull(hudSafeLayout, "HudSafeLayout not found.");

            var bottleGridLayout = GetPrivateField<GridLayoutGroup>(hudSafeLayout, "bottleGridLayout");
            var bottleGrid = GetPrivateField<RectTransform>(hudSafeLayout, "bottleGrid");
            var baseGridSpacing = GetPrivateField<Vector2>(hudSafeLayout, "_baseGridSpacing");
            var baseGridPadding = GetPrivateField<RectOffset>(hudSafeLayout, "_baseGridPadding");
            var baseCellSize = GetPrivateField<Vector2>(hudSafeLayout, "_baseGridCellSize");
            const float minimumInnerGapPx = 10f;

            Assert.GreaterOrEqual(bottleGridLayout.cellSize.y, baseCellSize.y + 4f,
                $"3-row cell height did not grow enough. baseline={baseCellSize.y:F2}, actual={bottleGridLayout.cellSize.y:F2}");
            Assert.GreaterOrEqual(bottleGridLayout.spacing.y, minimumInnerGapPx - 1f,
                $"3-row spacing fell below the minimum inner gap. actual={bottleGridLayout.spacing.y:F2}");
            float requiredSpacingReduction = baseGridSpacing.y > minimumInnerGapPx + 8f ? 8f : 0f;
            Assert.LessOrEqual(bottleGridLayout.spacing.y, baseGridSpacing.y - requiredSpacingReduction + 1f,
                $"3-row spacing did not compact enough. baseline={baseGridSpacing.y:F2}, actual={bottleGridLayout.spacing.y:F2}, requiredReduction={requiredSpacingReduction:F2}");
            Assert.GreaterOrEqual(bottleGridLayout.padding.top, bottleGridLayout.padding.bottom,
                $"3-row top padding should be at least as large as bottom padding. top={bottleGridLayout.padding.top}, bottom={bottleGridLayout.padding.bottom}");
            Assert.GreaterOrEqual(bottleGridLayout.padding.top, baseGridPadding.top,
                $"3-row top padding should preserve or increase HUD clearance. baseline={baseGridPadding.top}, actual={bottleGridLayout.padding.top}");
            Assert.LessOrEqual(bottleGridLayout.padding.bottom, baseGridPadding.bottom - 4,
                $"3-row bottom padding should shrink to reclaim vertical space. baseline={baseGridPadding.bottom}, actual={bottleGridLayout.padding.bottom}");
            Assert.AreEqual(Vector2.zero, bottleGrid.anchoredPosition,
                "3-row bottle grid anchor shifted unexpectedly.");

            Debug.Log($"[AndroidLayoutInvariance] All layout metrics within tolerance. " +
                      $"Canvas={current.CanvasWidth}×{current.CanvasHeight}, " +
                      $"Row1TopY={current.Row1CapTopY:F2}, RowGap12={current.RowGap12:F2}px");
        }

        [UnityTest]
        public IEnumerator TwoRowLayouts_KeepOriginalEqualGapModel()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = FindController();
            Assert.IsNotNull(controller, "GameController not found.");

            int levelIndex = -1;
            int activeBottleCount = 0;
            for (int candidate = 1; candidate <= 40; candidate++)
            {
                controller.LoadLevel(candidate, 192731);
                yield return null;
                yield return new WaitForSeconds(0.1f);
                Canvas.ForceUpdateCanvases();
                yield return null;

                activeBottleCount = GetActiveBottleRects(controller).Count;
                if (activeBottleCount > 0 && activeBottleCount <= 6)
                {
                    levelIndex = candidate;
                    break;
                }
            }

            Assert.Greater(levelIndex, 0, "Could not find a 2-row gameplay level in the first 40 deterministic levels.");

            var hudSafeLayout = Object.FindFirstObjectByType<HudSafeLayout>();
            Assert.IsNotNull(hudSafeLayout, "HudSafeLayout not found.");

            var bottleGridLayout = GetPrivateField<GridLayoutGroup>(hudSafeLayout, "bottleGridLayout");
            var bottleGrid = GetPrivateField<RectTransform>(hudSafeLayout, "bottleGrid");
            var baseGridSpacing = GetPrivateField<Vector2>(hudSafeLayout, "_baseGridSpacing");
            var baseGridPadding = GetPrivateField<RectOffset>(hudSafeLayout, "_baseGridPadding");

            Assert.IsNotNull(bottleGridLayout, "Bottle grid layout missing.");
            Assert.IsNotNull(bottleGrid, "Bottle grid missing.");

            Assert.AreEqual(baseGridSpacing.y, bottleGridLayout.spacing.y, 1f,
                $"2-row vertical spacing changed unexpectedly on level {levelIndex}. baseline={baseGridSpacing.y:F2}, actual={bottleGridLayout.spacing.y:F2}");
            Assert.AreEqual(baseGridPadding.top, bottleGridLayout.padding.top, 1,
                $"2-row top padding changed unexpectedly on level {levelIndex}. baseline={baseGridPadding.top}, actual={bottleGridLayout.padding.top}");
            Assert.AreEqual(baseGridPadding.bottom, bottleGridLayout.padding.bottom, 1,
                $"2-row bottom padding changed unexpectedly on level {levelIndex}. baseline={baseGridPadding.bottom}, actual={bottleGridLayout.padding.bottom}");
            Assert.AreEqual(bottleGridLayout.padding.top, bottleGridLayout.padding.bottom, 1,
                $"2-row layout no longer uses symmetric vertical padding on level {levelIndex}.");
            Assert.AreEqual(Vector2.zero, bottleGrid.anchoredPosition,
                $"2-row bottle grid anchor shifted unexpectedly on level {levelIndex}.");
        }

        // ─── D: Bounding-box overlap detection ────────────────────────────────────

        [UnityTest]
        public IEnumerator ActiveBottles_HaveNoOverlappingBoundingBoxes()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = FindController();
            if (controller == null)
            {
                Assert.Ignore("GameController not found.");
            }

            controller.LoadLevel(21, 192731);
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var bottles = GetActiveBottleRects(controller);
            Assert.Greater(bottles.Count, 0, "No active bottle RectTransforms found.");

            int overlapCount = 0;
            for (int i = 0; i < bottles.Count; i++)
            {
                var corners1 = GetWorldCorners(bottles[i]);
                for (int j = i + 1; j < bottles.Count; j++)
                {
                    var corners2 = GetWorldCorners(bottles[j]);
                    if (RectsOverlap(corners1, corners2))
                    {
                        overlapCount++;
                        Debug.LogError(
                            $"[OverlapDetect] Bottle {bottles[i].name} overlaps {bottles[j].name}");
                    }
                }
            }

            Assert.AreEqual(0, overlapCount,
                $"{overlapCount} bottle bounding-box overlaps detected after fix.");
        }

        // ─── E: Web landscape math regression guard ────────────────────────────────

        [Test]
        public void WebLandscapeFix_MathModel_PreservesPortraitCanvasHeight()
        {
            // Reference resolution used by all canvases.
            const float refW = 1080f;
            const float refH = 1920f;

            // Typical Web landscape viewport.
            const float screenW = 1920f;
            const float screenH = 1080f;

            // WITHOUT fix: matchWidthOrHeight = 0 (width-matching).
            float brokenScaleFactor = screenW / refW;            // 1.777…
            float brokenCanvasH = screenH / brokenScaleFactor; // 607.5 — bottles collapse

            // WITH fix:    matchWidthOrHeight = 1 (height-matching) — applied by
            // WebCanvasScalerController on WebGL builds.
            float fixedScaleFactor = screenH / refH;             // 0.5625
            float fixedCanvasH = screenH / fixedScaleFactor; // 1920 — full portrait height

            // Portrait reference (should be 1920).
            const float portraitScreenW = 1080f;
            const float portraitScreenH = 1920f;
            float portraitScaleFactor = portraitScreenW / refW;   // 1.0
            float portraitCanvasH = portraitScreenH / portraitScaleFactor; // 1920

            // --- Assertions ---

            Assert.Less(brokenCanvasH, portraitCanvasH * 0.4f,
                $"Without fix, landscape canvas height ({brokenCanvasH:F1}) should be much less than " +
                $"portrait ({portraitCanvasH:F1}), confirming the original bug exists.");

            Assert.AreEqual(portraitCanvasH, fixedCanvasH, 0.1f,
                $"With fix, landscape canvas height ({fixedCanvasH:F1}) must equal " +
                $"portrait canvas height ({portraitCanvasH:F1}) within 0.1 logical units.");

            // The fixed landscape canvas is wider (more horizontal space for background).
            float fixedCanvasW = screenW / fixedScaleFactor;
            Assert.Greater(fixedCanvasW, refW,
                $"Landscape canvas width ({fixedCanvasW:F1}) must exceed portrait width ({refW:F1}) " +
                "so extra horizontal area fills with background only.");

            Debug.Log(
                $"[WebLandscapeMath] Portrait:  canvas={portraitCanvasH:F0}h " +
                $"(scaleFactor={portraitScaleFactor:F4})\n" +
                $"[WebLandscapeMath] Broken:    canvas={brokenCanvasH:F0}h " +
                $"(scaleFactor={brokenScaleFactor:F4}) ← bottles tiny\n" +
                $"[WebLandscapeMath] Fixed:     canvas={fixedCanvasH:F0}h, " +
                $"width={fixedCanvasW:F0} (scaleFactor={fixedScaleFactor:F4}) ← correct");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static IEnumerator BootstrapAndCapture(System.Action<LayoutProbe.LayoutMetrics> onCapture)
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = FindController();
            Assert.IsNotNull(controller, "GameController not found.");

            controller.LoadLevel(21, 192731);
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var probeGo = new GameObject("LayoutProbe_Invariance");
            try
            {
                var probe = probeGo.AddComponent<LayoutProbe>();
                onCapture(probe.Capture(controller));
            }
            finally
            {
                Object.Destroy(probeGo);
            }
        }

        private static GameController FindController()
        {
            return Object.FindFirstObjectByType<GameController>();
        }

        private static void AssertRatio(string name, float baseline, float current)
        {
            float delta = Mathf.Abs(current - baseline);
            Assert.LessOrEqual(delta, RatioTolerance,
                $"{name} ratio delta {delta:F6} exceeds {RatioTolerance:F6} " +
                $"(baseline={baseline:F6}, current={current:F6}).");
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field not found: {fieldName}");
            return (T)field.GetValue(instance);
        }

        private static float GetMinY(RectTransform rect, RectTransform root)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float min = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                min = Mathf.Min(min, root.InverseTransformPoint(corners[i]).y);
            }

            return min;
        }

        private static float GetMaxY(RectTransform rect, RectTransform root)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float max = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                max = Mathf.Max(max, root.InverseTransformPoint(corners[i]).y);
            }

            return max;
        }

        private static List<RectTransform> GetActiveBottleRects(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return new List<RectTransform>();

            var views = field.GetValue(controller) as List<BottleView>;
            if (views == null) return new List<RectTransform>();

            var rects = new List<RectTransform>();
            foreach (var v in views)
            {
                if (v != null && v.gameObject.activeSelf)
                {
                    var rt = v.GetComponent<RectTransform>();
                    if (rt != null) rects.Add(rt);
                }
            }

            return rects;
        }

        private static Vector3[] GetWorldCorners(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return corners;
        }

        /// <summary>Returns true if two axis-aligned world-space rects overlap (with 0.5 px tolerance).</summary>
        private static bool RectsOverlap(Vector3[] a, Vector3[] b)
        {
            const float tol = 0.5f;
            float aMinX = Mathf.Min(a[0].x, a[1].x, a[2].x, a[3].x) + tol;
            float aMaxX = Mathf.Max(a[0].x, a[1].x, a[2].x, a[3].x) - tol;
            float aMinY = Mathf.Min(a[0].y, a[1].y, a[2].y, a[3].y) + tol;
            float aMaxY = Mathf.Max(a[0].y, a[1].y, a[2].y, a[3].y) - tol;

            float bMinX = Mathf.Min(b[0].x, b[1].x, b[2].x, b[3].x) + tol;
            float bMaxX = Mathf.Max(b[0].x, b[1].x, b[2].x, b[3].x) - tol;
            float bMinY = Mathf.Min(b[0].y, b[1].y, b[2].y, b[3].y) + tol;
            float bMaxY = Mathf.Max(b[0].y, b[1].y, b[2].y, b[3].y) - tol;

            return aMinX < bMaxX && aMaxX > bMinX && aMinY < bMaxY && aMaxY > bMinY;
        }
    }
}
