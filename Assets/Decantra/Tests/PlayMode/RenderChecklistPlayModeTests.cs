/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Tests.PlayMode.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    /// <summary>
    /// Automated regression tests that replace the manual verification checklist in
    /// docs/render-baseline.md.
    ///
    /// Checklist section 1 — Web portrait and Android/iOS portrait (1080 × 1920 canvas):
    ///   1a. Bottles fill a substantial portion of viewport height.
    ///   1b. HUD brand lockup (logo + stat panels) is visible and centred at the top.
    ///   1c. HUD stat panels do not overlap the bottle grid.
    ///
    /// Checklist section 2 — Web landscape (1920 × 1080).
    ///   Simulated by switching all CanvasScalers to matchWidthOrHeight = 1f
    ///   (height-matching), which is exactly what WebCanvasScalerController does on
    ///   a real WebGL build in landscape orientation.
    ///   2a. Gameplay area (BottleGrid middle column) is centred horizontally on canvas.
    ///   2b. Background fills the full viewport — structural anchoring invariant
    ///       (anchorMin = 0,0 / anchorMax = 1,1) guarantees no black bars at any canvas width.
    ///   2c. Canvas height equals the portrait reference (1920 logical units) →
    ///       bottle proportions are identical to portrait.
    ///   2d. HUD brand lockup and top stat panel remain centred on the (wider) canvas.
    ///   2e. No bottle bounding-box overlap.
    ///   2f. HUD does not overlap the bottle grid.
    ///
    /// Checklist section 3 — Android / iOS portrait pixel identity:
    ///   Covered by AndroidLayoutInvariancePlayModeTests (layout-ratio comparison vs
    ///   the committed pre-fix baseline fixture).
    /// </summary>
    public sealed class RenderChecklistPlayModeTests
    {
        // ── Tolerances ─────────────────────────────────────────────────────────────
        /// <summary>
        /// How far the centre of a UI element may deviate from canvas X = 0 (px, canvas-local).
        /// Using 2 px to allow for sub-pixel rounding in Unity UI layout.
        /// </summary>
        private const float CentreXTolerance = 2f;

        /// <summary>Tolerance for height / position comparisons (logical px).</summary>
        private const float PositionTolerance = 1f;

        /// <summary>
        /// Bottles must occupy at least this fraction of total canvas height. Chosen conservatively
        /// so the test fails only if the layout has collapsed (the web landscape bug reduced the
        /// bottle fraction to ~12% of viewport).
        /// </summary>
        // HudSafeLayout distributes ~34% of canvas height to bottles in the 640×480
        // test-runner window (canvas 1080×810). 30% is a robust lower bound that would
        // catch a collapsed canvas (such as the pre-fix landscape bug) while being safe
        // for the non-square test-runner aspect ratio.
        private const float MinBottleFractionOfCanvas = 0.30f;

        // ── Shared game level ───────────────────────────────────────────────────────
        private const int TestLevel = 21;
        private const int TestSeed = 192731;

        // ── Scaler names ────────────────────────────────────────────────────────────
        private static readonly string[] CanvasNames =
            { "Canvas_Background", "Canvas_Game", "Canvas_UI" };

        // ──────────────────────────────────────────────────────────────────────────
        // Teardown: always restore scalers to portrait default after each test to
        // avoid cross-test contamination.
        // ──────────────────────────────────────────────────────────────────────────

        [TearDown]
        public void RestoreScalers()
        {
            try
            {
                SetAllScalers(0f);
                Canvas.ForceUpdateCanvases();
            }
            catch
            {
                // Scene may not have been bootstrapped; ignore cleanup errors.
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Checklist Section 1: Web portrait / Android portrait
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 1a. Bottles occupy at least 30% of canvas height in portrait mode.
        /// Rationale: before the fix the landscape canvas collapsed to 607 logical units,
        /// which reduced the bottle area to ~12% of the viewport. A ≥ 30% threshold
        /// catches any similar regression without being brittle about exact layout.
        /// Note: the Unity test runner uses a 640×480 window (4:3 aspect), giving a
        /// canvas height of 810 logical units where HudSafeLayout fills ~34% with bottles.
        /// </summary>
        [UnityTest]
        public IEnumerator Portrait_Bottles_OccupyAtLeast30PctOfCanvasHeight()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCaptureMetrics(m => metrics = m);

            float bottleSpan = metrics.Row1CapTopY - metrics.BottomBottleBottomY;
            float fraction = bottleSpan / metrics.CanvasHeight;

            Assert.GreaterOrEqual(fraction, MinBottleFractionOfCanvas,
                $"Bottles occupy only {fraction * 100f:F1}% of canvas height. " +
                $"Expected ≥ {MinBottleFractionOfCanvas * 100f:F0}%. " +
                $"Row1CapTopY={metrics.Row1CapTopY:F1}, BottomY={metrics.BottomBottleBottomY:F1}, " +
                $"CanvasHeight={metrics.CanvasHeight:F1}. " +
                "This indicates the canvas height has collapsed (Web landscape regression) or " +
                "the portrait layout is broken.");
        }

        /// <summary>
        /// 1b. The brand lockup (logo) is visible at the top of the canvas and centred.
        /// </summary>
        [UnityTest]
        public IEnumerator Portrait_BrandLockup_IsVisibleAndCentredAtCanvasTop()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var brandRect = RequireRect("BrandLockup");

            // Non-zero width means it has been laid out and rendered.
            Assert.Greater(brandRect.rect.width, 0f,
                "BrandLockup has zero width — the logo HUD element has not been laid out.");

            // X centre must be at canvas midpoint (0 in canvas-local coords).
            float centreX = GetCanvasLocalCentreX(brandRect);
            Assert.AreEqual(0f, centreX, CentreXTolerance,
                $"BrandLockup centre X = {centreX:F1} deviates from canvas centre by more than " +
                $"{CentreXTolerance} px. The logo is not centred.");

            // Must be in the top half of the canvas (Y > 0 in pivot-centred space).
            float brandBottom = GetCanvasLocalBottom(brandRect);
            Assert.Greater(brandBottom, 0f,
                $"BrandLockup bottom edge (Y = {brandBottom:F1}) is at or below the canvas midpoint. " +
                "The logo has been pushed below the centre of the screen.");
        }

        /// <summary>
        /// 1b (continued). The TopHud stat panel (Level / Moves / Score) is visible and centred.
        /// </summary>
        [UnityTest]
        public IEnumerator Portrait_TopHud_IsVisibleAndCentredAtCanvasTop()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var topHudRect = RequireRect("TopHud");

            Assert.Greater(topHudRect.sizeDelta.y, 0f,
                "TopHud has zero height — the stat panel (Level / Moves / Score) is not visible.");

            float centreX = GetCanvasLocalCentreX(topHudRect);
            Assert.AreEqual(0f, centreX, CentreXTolerance,
                $"TopHud centre X = {centreX:F1} deviates from canvas centre by more than " +
                $"{CentreXTolerance} px. The stat panel is not centred.");

            float hudBottom = GetCanvasLocalBottom(topHudRect);
            Assert.Greater(hudBottom, 0f,
                $"TopHud bottom edge (Y = {hudBottom:F1}) is at or below the canvas midpoint. " +
                "The stat panel has been pushed too far down.");
        }

        /// <summary>
        /// 1c. The secondary HUD (action buttons row) does not overlap the bottle grid.
        /// </summary>
        [UnityTest]
        public IEnumerator Portrait_HudDoesNotOverlapBottleGrid()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCaptureMetrics(m => metrics = m);

            // Capture the HUD bottom in the same canvas-local space as Row1CapTopY.
            var secondaryHudRect = RequireRect("SecondaryHud");
            float hudBottom = GetCanvasLocalBottom(secondaryHudRect);
            float bottleTop = metrics.Row1CapTopY;

            Assert.LessOrEqual(hudBottom, bottleTop + PositionTolerance,
                $"SecondaryHud bottom (Y = {hudBottom:F1}) overlaps the first bottle row top " +
                $"(Y = {bottleTop:F1}) by {hudBottom - bottleTop:F1} px. " +
                "The HUD and bottle grid must not overlap.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Checklist Section 2: Web landscape (height-matching simulation)
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 2c. With height-matching (matchWidthOrHeight = 1f), all canvases have height = 1920 —
        /// the portrait reference. This means bottles occupy the same absolute canvas height
        /// as in portrait, so the visual proportion of the viewport is preserved.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_AllCanvases_HavePortraitReferenceHeight()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            SetAllScalers(1f);
            yield return null;
            Canvas.ForceUpdateCanvases();

            foreach (var canvasName in CanvasNames)
            {
                var go = GameObject.Find(canvasName);
                Assert.IsNotNull(go, $"{canvasName} not found after EnsureScene.");

                float canvasH = go.GetComponent<RectTransform>().rect.height;
                Assert.AreEqual(1920f, canvasH, PositionTolerance,
                    $"{canvasName} height = {canvasH:F1} with height-matching active. " +
                    $"Expected 1920 ± {PositionTolerance}. " +
                    "Height-matching must preserve the portrait reference height so bottle " +
                    "proportions are identical in portrait and landscape.");
            }
        }

        /// <summary>
        /// 2b (quantitative). With height-matching the canvas is wider than the 1080-unit
        /// portrait reference. Because the background fills the entire canvas (structural
        /// invariant below), a wider canvas means background extends beyond screen edges in
        /// world-space — no area of the viewport is left uncovered (no black bars).
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_BackgroundCanvas_IsWiderThanPortraitReference()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            SetAllScalers(1f);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var bgGo = GameObject.Find("Canvas_Background");
            Assert.IsNotNull(bgGo, "Canvas_Background not found.");

            float canvasW = bgGo.GetComponent<RectTransform>().rect.width;
            Assert.Greater(canvasW, 1080f,
                $"Canvas_Background width = {canvasW:F1} with height-matching active. " +
                "Expected > 1080 (portrait reference width). " +
                "When the canvas is not wider than the portrait reference it implies that " +
                "content is letter-boxed, potentially leaving uncovered viewport area (black bars). " +
                "Verify WebCanvasScalerController uses matchWidthOrHeight = 1f in landscape.");
        }

        /// <summary>
        /// 2b (structural). The Background layer uses full-canvas anchoring (anchorMin = 0,0 /
        /// anchorMax = 1,1). Combined with 2b-quantitative (canvas wider than screen in landscape),
        /// this guarantees the background image fills every pixel of the viewport regardless of
        /// canvas width, so there are never any black bars.
        /// </summary>
        [UnityTest]
        public IEnumerator Background_FillsFullCanvas_GuaranteesNoBlackBars()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bgRect = RequireRect("Background");

            Assert.AreEqual(Vector2.zero, bgRect.anchorMin,
                "Background.anchorMin must be (0, 0) to fill the full canvas width. " +
                "Any inset here leaves an uncovered strip (black bar) at the canvas edge.");
            Assert.AreEqual(Vector2.one, bgRect.anchorMax,
                "Background.anchorMax must be (1, 1) to fill the full canvas height. " +
                "Any inset here leaves an uncovered strip (black bar) at the canvas edge.");
            Assert.AreEqual(Vector2.zero, bgRect.offsetMin,
                "Background.offsetMin must be (0, 0) — no inset from canvas edge.");
            Assert.AreEqual(Vector2.zero, bgRect.offsetMax,
                "Background.offsetMax must be (0, 0) — no inset from canvas edge.");
        }

        /// <summary>
        /// 2a. With simulated landscape scaling the middle bottle column is centred on the
        /// canvas (canvas-local X ≈ 0). The BottleGrid is anchored at the canvas midpoint,
        /// so this also proves the gameplay area is centred horizontally.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_GameplayArea_IsCentredHorizontally()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCaptureMetrics(m => metrics = m, simulateLandscape: true);

            Assert.AreEqual(0f, metrics.MiddleBottleCenterX, CentreXTolerance,
                $"Middle bottle centre X = {metrics.MiddleBottleCenterX:F1} deviates from canvas " +
                $"centre by more than {CentreXTolerance:F0} px in simulated landscape. " +
                "The gameplay area is not centred horizontally.");
        }

        /// <summary>
        /// 2d. With simulated landscape scaling the brand lockup and top stat panel are centred
        /// on the (wider) canvas. Both elements are anchored at (0.5, 1f) in canvas-local space,
        /// so their centre X should be 0 regardless of canvas width.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_HudElements_AreCentredOnCanvas()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            SetAllScalers(1f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var brandRect = RequireRect("BrandLockup");
            float brandX = GetCanvasLocalCentreX(brandRect);
            Assert.AreEqual(0f, brandX, CentreXTolerance,
                $"BrandLockup centre X = {brandX:F1} deviates from canvas centre by more than " +
                $"{CentreXTolerance} px in simulated landscape. Logo is not centred.");

            var topHudRect = RequireRect("TopHud");
            float topHudX = GetCanvasLocalCentreX(topHudRect);
            Assert.AreEqual(0f, topHudX, CentreXTolerance,
                $"TopHud centre X = {topHudX:F1} deviates from canvas centre by more than " +
                $"{CentreXTolerance} px in simulated landscape. Stat panel is not centred.");
        }

        /// <summary>
        /// 2c (corollary). Bottles occupy at least 30% of canvas height in landscape simulation.
        /// HudSafeLayout preserves the same relative distribution regardless of canvas height, so
        /// the fraction in landscape (~34%) matches portrait (~34%) when both use the same test
        /// runner window. The key correctness property is verified by
        /// LandscapeSimulated_AllCanvases_HavePortraitReferenceHeight (canvas height = 1920);
        /// this test provides an additional sanity guard against a collapsed bottle layout.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_Bottles_OccupyAtLeast30PctOfCanvasHeight()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCaptureMetrics(m => metrics = m, simulateLandscape: true);

            Assert.AreEqual(1920f, metrics.CanvasHeight, PositionTolerance,
                $"With height-matching, Canvas_UI height = {metrics.CanvasHeight:F1} but expected 1920. " +
                "Prerequisite for the bottle-fraction assertion has failed.");

            float bottleSpan = metrics.Row1CapTopY - metrics.BottomBottleBottomY;
            float fraction = bottleSpan / metrics.CanvasHeight;

            Assert.GreaterOrEqual(fraction, MinBottleFractionOfCanvas,
                $"In simulated landscape, bottles occupy only {fraction * 100f:F1}% of canvas height. " +
                $"Expected ≥ {MinBottleFractionOfCanvas * 100f:F0}%. " +
                $"Span={bottleSpan:F1}, CanvasHeight={metrics.CanvasHeight:F1}.");
        }

        /// <summary>
        /// 2e. No bottle bounding-box overlaps exist with simulated landscape scaling.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_NoBottleOverlap()
        {
            var metrics = default(LayoutProbe.LayoutMetrics);
            yield return BootstrapAndCaptureMetrics(m => metrics = m, simulateLandscape: true);

            Assert.IsFalse(metrics.HasBottleOverlap,
                $"Bottle bounding-box overlap detected in simulated landscape. " +
                $"RowGap12 = {metrics.RowGap12:F1} px, RowGap23 = {metrics.RowGap23:F1} px.");

            Assert.GreaterOrEqual(metrics.RowGap12, 0f,
                $"Row 1 and row 2 overlap in simulated landscape (gap = {metrics.RowGap12:F1} px).");

            Assert.GreaterOrEqual(metrics.RowGap23, 0f,
                $"Row 2 and row 3 overlap in simulated landscape (gap = {metrics.RowGap23:F1} px).");
        }

        /// <summary>
        /// 2f. In the 1920-unit tall landscape-simulated canvas (height-matching active) the HUD
        /// occupies the upper half of the canvas (Y &gt; 0) and the bottle rows are in the lower half
        /// (Y &lt; 0). This verifies screen-half separation: the HUD bottom edge is above the canvas
        /// midpoint and the first bottle row top is below the midpoint, proving that HudSafeLayout's
        /// equal-gaps formula correctly distributes vertical space across the taller canvas.
        /// </summary>
        [UnityTest]
        public IEnumerator LandscapeSimulated_HudIsAboveMidpoint_BottlesAreBelowMidpoint()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            SetAllScalers(1f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found.");
            controller.LoadLevel(TestLevel, TestSeed);
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var secondaryHudRect = RequireRect("SecondaryHud");
            float hudBottom = GetCanvasLocalBottom(secondaryHudRect);

            var probeGo = new GameObject("LayoutProbe_LandscapeHudSep");
            float bottleTop;
            try
            {
                var probe = probeGo.AddComponent<LayoutProbe>();
                var metrics = probe.Capture(controller);
                bottleTop = metrics.Row1CapTopY;
            }
            finally
            {
                Object.Destroy(probeGo);
            }

            // With a 1920-tall canvas (Y range -960 to +960) the HUD lives in the top half
            // (positive Y) and the bottle rows in the bottom half (negative Y).
            Assert.Greater(hudBottom, 0f,
                $"SecondaryHud bottom edge (Y = {hudBottom:F1}) should be above the canvas " +
                "midpoint (Y = 0) in the 1920-tall landscape-simulated canvas. " +
                "The HUD has been pushed too far down.");

            // Allow a small cap-decoration margin: the Rim may sit slightly above the bottle body.
            Assert.Less(bottleTop, -10f,
                $"First bottle row cap top (Y = {bottleTop:F1}) should be well below the canvas " +
                "midpoint in the 1920-tall landscape-simulated canvas. " +
                "The bottle grid has been pushed too far up.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Math-model guard: proves identical bottle proportions between portrait and
        // landscape via the CanvasScaler formula — no scene required.
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Portrait and landscape both produce canvas height = refHeight (1920 logical units)
        /// when the correct matchWidthOrHeight value is used, so HudSafeLayout sees the same
        /// available vertical space and bottles occupy the same fraction of the viewport.
        /// </summary>
        [Test]
        public void WebPortraitAndLandscape_CanvasScalerMath_BothProduceReferenceCanvasHeight()
        {
            const float refW = 1080f;
            const float refH = 1920f;

            // Portrait 1080×1920, width-matching (matchWidthOrHeight = 0).
            const float portraitSW = 1080f;
            const float portraitSH = 1920f;
            float portraitScale = portraitSW / refW;
            float portraitCanvasH = portraitSH / portraitScale;

            // Landscape 1920×1080, height-matching (matchWidthOrHeight = 1).
            const float landscapeSW = 1920f;
            const float landscapeSH = 1080f;
            float landscapeScale = landscapeSH / refH;
            float landscapeCanvasH = landscapeSH / landscapeScale;
            float landscapeCanvasW = landscapeSW / landscapeScale;

            // Both canvas heights must equal the reference height (1920).
            Assert.AreEqual(refH, portraitCanvasH, 0.01f,
                $"Portrait canvas height = {portraitCanvasH:F2}, expected {refH}.");
            Assert.AreEqual(refH, landscapeCanvasH, 0.01f,
                $"Landscape canvas height = {landscapeCanvasH:F2}, expected {refH:F0}. " +
                "The height-matching fix must preserve the portrait canvas height in landscape.");

            // Landscape canvas is wider, meaning background extends beyond the screen viewport.
            Assert.Greater(landscapeCanvasW, refW,
                $"Landscape canvas width = {landscapeCanvasW:F1} must exceed portrait reference " +
                $"width {refW:F0} to ensure background fills the full viewport without black bars.");

            // Without the fix (width-matching in landscape), height would be much smaller.
            float brokenScale = landscapeSW / refW;
            float brokenCanvasH = landscapeSH / brokenScale;
            Assert.Less(brokenCanvasH, refH * 0.4f,
                $"Broken landscape canvas height = {brokenCanvasH:F1}. Without the fix this must " +
                $"be < {refH * 0.4f:F0} to confirm the original bug was real.");
        }

        // ══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bootstraps the scene, optionally switches all CanvasScalers to height-matching
        /// (landscape simulation), loads the test level, and captures LayoutProbe metrics.
        /// <para>
        /// TearDown is responsible for restoring the scaler to the default (0f). The scene
        /// and scaler state remain active after this coroutine so callers can make additional
        /// measurements at the same canvas dimensions.
        /// </para>
        /// </summary>
        private static IEnumerator BootstrapAndCaptureMetrics(
            System.Action<LayoutProbe.LayoutMetrics> onMetrics,
            bool simulateLandscape = false)
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            if (simulateLandscape)
            {
                SetAllScalers(1f);
                // Yield two frames: first for CanvasScaler to recompute scale factor,
                // second for HudSafeLayout LateUpdate to reflow the bottle grid.
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found after EnsureScene.");

            controller.LoadLevel(TestLevel, TestSeed);
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var probeGo = new GameObject("LayoutProbe_Checklist");
            try
            {
                var probe = probeGo.AddComponent<LayoutProbe>();
                onMetrics(probe.Capture(controller));
            }
            finally
            {
                Object.Destroy(probeGo);
            }
        }

        private static void SetAllScalers(float matchValue)
        {
            foreach (var name in CanvasNames)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var scaler = go.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.matchWidthOrHeight = matchValue;
            }
        }

        /// <summary>
        /// Returns the canvas-local Y of the bottom edge of <paramref name="rt"/>,
        /// expressed in Canvas_UI local coordinates (pivot-centred: 0 = canvas midpoint,
        /// +canvasH/2 = top edge, −canvasH/2 = bottom edge).
        /// </summary>
        private static float GetCanvasLocalBottom(RectTransform rt)
        {
            var canvasRect = RequireCanvasUIRect();
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minY = float.MaxValue;
            foreach (var c in corners)
            {
                float localY = canvasRect.InverseTransformPoint(c).y;
                if (localY < minY) minY = localY;
            }
            return minY;
        }

        /// <summary>
        /// Returns the canvas-local X of the horizontal centre of <paramref name="rt"/>.
        /// 0 = canvas midpoint; ±canvasW/2 = left/right edge.
        /// </summary>
        private static float GetCanvasLocalCentreX(RectTransform rt)
        {
            var canvasRect = RequireCanvasUIRect();
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (var c in corners)
            {
                float localX = canvasRect.InverseTransformPoint(c).x;
                if (localX < minX) minX = localX;
                if (localX > maxX) maxX = localX;
            }
            return (minX + maxX) * 0.5f;
        }

        private static RectTransform RequireCanvasUIRect()
        {
            var go = GameObject.Find("Canvas_UI");
            Assert.IsNotNull(go, "Canvas_UI not found — scene may not have been bootstrapped.");
            var rt = go.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, "Canvas_UI has no RectTransform.");
            return rt;
        }

        private static RectTransform RequireRect(string goName)
        {
            var go = GameObject.Find(goName);
            Assert.IsNotNull(go, $"'{goName}' GameObject not found — scene may not have been bootstrapped.");
            var rt = go.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, $"'{goName}' has no RectTransform.");
            return rt;
        }
    }
}
