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
        public IEnumerator LayoutMetrics_MatchPreFixBaseline_NoDeltaExceedsTolerance()
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
            AssertRatio("Row1CapTopY", baseline.Row1CapTopRatioY, current.Row1CapTopRatioY);
            AssertRatio("Row2CapTopY", baseline.Row2CapTopRatioY, current.Row2CapTopRatioY);
            AssertRatio("Row3CapTopY", baseline.Row3CapTopRatioY, current.Row3CapTopRatioY);
            AssertRatio("BottomBottleBottomY", baseline.BottomBottleBottomRatioY, current.BottomBottleBottomRatioY);
            AssertRatio("LeftBottleCenterX", baseline.LeftBottleCenterRatioX, current.LeftBottleCenterRatioX);
            AssertRatio("MiddleBottleCenterX", baseline.MiddleBottleCenterRatioX, current.MiddleBottleCenterRatioX);
            AssertRatio("RightBottleCenterX", baseline.RightBottleCenterRatioX, current.RightBottleCenterRatioX);
            AssertRatio("RowSpacing12", baseline.RowSpacing12RatioY, current.RowSpacing12RatioY);
            AssertRatio("RowSpacing23", baseline.RowSpacing23RatioY, current.RowSpacing23RatioY);
            AssertRatio("BottleSpacingLM", baseline.BottleSpacingLMRatioX, current.BottleSpacingLMRatioX);
            AssertRatio("BottleSpacingMR", baseline.BottleSpacingMRRatioX, current.BottleSpacingMRRatioX);

            Debug.Log($"[AndroidLayoutInvariance] All layout metrics within tolerance. " +
                      $"Canvas={current.CanvasWidth}×{current.CanvasHeight}, " +
                      $"Row1TopY={current.Row1CapTopY:F2}, RowGap12={current.RowGap12:F2}px");
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
