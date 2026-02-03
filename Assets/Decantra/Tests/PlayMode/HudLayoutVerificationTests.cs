/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    /// <summary>
    /// Automated UI layout verification tests for HUD tiles.
    /// Verifies structural consistency, sizing, and layout invariants programmatically.
    /// </summary>
    public sealed class HudLayoutVerificationTests
    {
        private const float WidthTolerancePx = 1f;
        private const float ExpectedTileMinWidth = 220f;
        private const float ExpectedTileMinHeight = 140f;
        private const int ExpectedHudTileCount = 5; // Level, Moves, Score, MaxLevel, HighScore (Reset is a button, not a stat tile)
        private const float TextPaddingPx = 48f; // Approximate padding from panel edge to text area (24px left + 24px right)

        /// <summary>
        /// Identifies HUD stat panels by their structure: Image + LayoutElement + specific children (Shadow, GlassHighlight, Value text).
        /// Deduplicates by panel name to handle cases where multiple scene bootstraps create duplicates.
        /// </summary>
        private static List<GameObject> FindHudStatPanels()
        {
            var allImages = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var hudPanelsByName = new Dictionary<string, GameObject>();

            foreach (var image in allImages)
            {
                var go = image.gameObject;
                if (go == null) continue;

                // Check for LayoutElement (required for HUD stat panels)
                var layoutElement = go.GetComponent<LayoutElement>();
                if (layoutElement == null) continue;

                // Check for specific children that identify stat panels
                var shadow = go.transform.Find("Shadow");
                var glassHighlight = go.transform.Find("GlassHighlight");
                var valueText = go.transform.Find("Value");

                // Stat panels have Shadow, GlassHighlight, and Value children
                if (shadow != null && glassHighlight != null && valueText != null)
                {
                    // Deduplicate by name - keep the first (or active) panel found
                    if (!hudPanelsByName.ContainsKey(go.name))
                    {
                        hudPanelsByName[go.name] = go;
                    }
                    else if (go.activeInHierarchy && !hudPanelsByName[go.name].activeInHierarchy)
                    {
                        // Prefer active panel over inactive one
                        hudPanelsByName[go.name] = go;
                    }
                }
            }

            return hudPanelsByName.Values.ToList();
        }

        /// <summary>
        /// Extracts HUD tile metrics for verification.
        /// </summary>
        private struct HudTileMetrics
        {
            public string Name;
            public float Width;
            public float Height;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public int ImageCount;
            public int ChildCount;
            public float TextPreferredWidth;
            public bool HasLayoutElement;
            public float LayoutMinWidth;
            public float LayoutMinHeight;
            public float LayoutPreferredWidth;
            public bool HasShadow;
            public bool HasGlassHighlight;
            public bool HasValueText;
        }

        private static HudTileMetrics ExtractTileMetrics(GameObject tile)
        {
            var rect = tile.GetComponent<RectTransform>();
            var layoutElement = tile.GetComponent<LayoutElement>();
            var text = tile.GetComponentInChildren<Text>();

            // Count only direct child Images (excluding the main panel image)
            int imageCount = 0;
            var mainImage = tile.GetComponent<Image>();
            if (mainImage != null) imageCount++;

            // Also count images in children (Shadow, GlassHighlight, etc.)
            foreach (Transform child in tile.transform)
            {
                if (child.GetComponent<Image>() != null) imageCount++;
            }

            return new HudTileMetrics
            {
                Name = tile.name,
                Width = rect != null ? rect.rect.width : 0f,
                Height = rect != null ? rect.rect.height : 0f,
                AnchorMin = rect != null ? rect.anchorMin : Vector2.zero,
                AnchorMax = rect != null ? rect.anchorMax : Vector2.zero,
                ImageCount = imageCount,
                ChildCount = tile.transform.childCount,
                TextPreferredWidth = text != null ? text.preferredWidth : 0f,
                HasLayoutElement = layoutElement != null,
                LayoutMinWidth = layoutElement != null ? layoutElement.minWidth : 0f,
                LayoutMinHeight = layoutElement != null ? layoutElement.minHeight : 0f,
                LayoutPreferredWidth = layoutElement != null ? layoutElement.preferredWidth : 0f,
                HasShadow = tile.transform.Find("Shadow") != null,
                HasGlassHighlight = tile.transform.Find("GlassHighlight") != null,
                HasValueText = tile.transform.Find("Value") != null
            };
        }

        [UnityTest]
        public IEnumerator HudTiles_ExactlyExpectedCountDetected()
        {
            SceneBootstrap.EnsureScene();
            yield return null; // Wait one frame for UI layout

            var hudPanels = FindHudStatPanels();

            Assert.AreEqual(ExpectedHudTileCount, hudPanels.Count,
                $"Expected exactly {ExpectedHudTileCount} HUD stat tiles, found {hudPanels.Count}. " +
                $"Found tiles: {string.Join(", ", hudPanels.Select(p => p.name))}");
        }

        [UnityTest]
        public IEnumerator HudTiles_AllHaveIdenticalWidths()
        {
            SceneBootstrap.EnsureScene();
            yield return null; // Wait one frame for UI layout

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            var metrics = hudPanels.Select(ExtractTileMetrics).ToList();
            var firstWidth = metrics[0].LayoutMinWidth;

            foreach (var m in metrics)
            {
                Assert.AreEqual(firstWidth, m.LayoutMinWidth, WidthTolerancePx,
                    $"HUD tile '{m.Name}' has minWidth={m.LayoutMinWidth}, expected {firstWidth} (±{WidthTolerancePx}px)");
            }
        }

        [UnityTest]
        public IEnumerator HudTiles_AllHaveConsistentLayoutElement()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            var metrics = hudPanels.Select(ExtractTileMetrics).ToList();

            foreach (var m in metrics)
            {
                Assert.IsTrue(m.HasLayoutElement, $"HUD tile '{m.Name}' missing LayoutElement component");
                Assert.AreEqual(ExpectedTileMinWidth, m.LayoutMinWidth, WidthTolerancePx,
                    $"HUD tile '{m.Name}' has minWidth={m.LayoutMinWidth}, expected {ExpectedTileMinWidth}");
                Assert.AreEqual(ExpectedTileMinHeight, m.LayoutMinHeight, WidthTolerancePx,
                    $"HUD tile '{m.Name}' has minHeight={m.LayoutMinHeight}, expected {ExpectedTileMinHeight}");
            }
        }

        [UnityTest]
        public IEnumerator HudTiles_BottomNotNarrowerThanTop()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            var topTiles = hudPanels.Where(p =>
                p.name == "LevelPanel" || p.name == "MovesPanel" || p.name == "ScorePanel").ToList();
            var bottomTiles = hudPanels.Where(p =>
                p.name == "MaxLevelPanel" || p.name == "HighScorePanel").ToList();

            Assert.IsTrue(topTiles.Count > 0, "No top HUD tiles found");
            Assert.IsTrue(bottomTiles.Count > 0, "No bottom HUD tiles found");

            var topMinWidth = topTiles.Select(t => t.GetComponent<LayoutElement>()?.minWidth ?? 0f).Min();
            var bottomMinWidth = bottomTiles.Select(t => t.GetComponent<LayoutElement>()?.minWidth ?? 0f).Min();

            Assert.GreaterOrEqual(bottomMinWidth, topMinWidth - WidthTolerancePx,
                $"Bottom HUD tiles (minWidth={bottomMinWidth}) are narrower than top tiles (minWidth={topMinWidth})");
        }

        [UnityTest]
        public IEnumerator HudTiles_EachHasExpectedImageCount()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            // Each stat panel should have:
            // 1. Main background Image on the panel itself
            // 2. Shadow child Image
            // 3. GlassHighlight child Image
            // Total: 3 images per tile
            const int expectedImageCount = 3;

            foreach (var panel in hudPanels)
            {
                var metrics = ExtractTileMetrics(panel);
                Assert.AreEqual(expectedImageCount, metrics.ImageCount,
                    $"HUD tile '{metrics.Name}' has {metrics.ImageCount} Image components, expected {expectedImageCount}");
            }
        }

        [UnityTest]
        public IEnumerator HudTiles_AllHaveSameStructuralSignature()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            var metrics = hudPanels.Select(ExtractTileMetrics).ToList();

            foreach (var m in metrics)
            {
                Assert.IsTrue(m.HasShadow, $"HUD tile '{m.Name}' missing Shadow child");
                Assert.IsTrue(m.HasGlassHighlight, $"HUD tile '{m.Name}' missing GlassHighlight child");
                Assert.IsTrue(m.HasValueText, $"HUD tile '{m.Name}' missing Value text child");
            }
        }

        [UnityTest]
        public IEnumerator HudTiles_TextFitsWithinBounds()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            // Force a render to populate text
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller != null)
            {
                controller.LoadLevel(1, 12345);
            }
            yield return null;
            yield return null; // Extra frame for text layout

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            foreach (var panel in hudPanels)
            {
                var metrics = ExtractTileMetrics(panel);
                // Use actual rendered width (from RectTransform) since tiles have flexibleWidth
                var availableWidth = metrics.Width - TextPaddingPx;

                // Text should fit within the actual rendered panel width with padding
                // Note: preferredWidth can be 0 if text hasn't been laid out yet
                if (metrics.TextPreferredWidth > 0 && metrics.Width > 0)
                {
                    Assert.LessOrEqual(metrics.TextPreferredWidth, availableWidth + WidthTolerancePx,
                        $"HUD tile '{metrics.Name}' text preferredWidth={metrics.TextPreferredWidth} exceeds " +
                        $"available width={availableWidth} (actual tile width={metrics.Width} - padding={TextPaddingPx})");
                }
            }
        }

        [UnityTest]
        public IEnumerator VignetteEffect_IsDisabled()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var vignetteGo = GameObject.Find("BackgroundVignette");

            // Vignette should either not exist, be inactive, or have zero alpha
            if (vignetteGo != null)
            {
                var image = vignetteGo.GetComponent<Image>();
                bool isDisabled = !vignetteGo.activeSelf || (image != null && image.color.a <= 0.001f);
                Assert.IsTrue(isDisabled,
                    $"Vignette effect should be disabled. GameObject active={vignetteGo.activeSelf}, " +
                    $"Image alpha={image?.color.a ?? 0f}");
            }
            // If vignetteGo is null, that's also acceptable (completely removed)
        }

        [UnityTest]
        public IEnumerator Background_HasNoVignetteAlpha()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found");

            // Load a level to trigger background application
            controller.LoadLevel(1, 12345);
            yield return null;

            // Verify vignette image has zero alpha
            var vignetteField = typeof(GameController).GetField("backgroundVignette",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(vignetteField, "backgroundVignette field not found");

            var vignetteImage = vignetteField.GetValue(controller) as Image;
            if (vignetteImage != null && vignetteImage.gameObject.activeSelf)
            {
                Assert.LessOrEqual(vignetteImage.color.a, 0.001f,
                    $"Vignette image alpha should be 0, but is {vignetteImage.color.a}");
            }
        }

        [UnityTest]
        public IEnumerator HudLayout_SerializesToDeterministicJson()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller != null)
            {
                controller.LoadLevel(1, 12345);
            }
            yield return null;

            var hudPanels = FindHudStatPanels();
            Assert.IsTrue(hudPanels.Count > 0, "No HUD tiles found");

            // Build deterministic JSON representation
            var metrics = hudPanels
                .OrderBy(p => p.name)
                .Select(ExtractTileMetrics)
                .ToList();

            var json = new System.Text.StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"hudTileCount\": {metrics.Count},");
            json.AppendLine("  \"tiles\": [");

            for (int i = 0; i < metrics.Count; i++)
            {
                var m = metrics[i];
                json.AppendLine("    {");
                json.AppendLine($"      \"name\": \"{m.Name}\",");
                json.AppendLine($"      \"layoutMinWidth\": {m.LayoutMinWidth},");
                json.AppendLine($"      \"layoutMinHeight\": {m.LayoutMinHeight},");
                json.AppendLine($"      \"layoutPreferredWidth\": {m.LayoutPreferredWidth},");
                json.AppendLine($"      \"imageCount\": {m.ImageCount},");
                json.AppendLine($"      \"hasShadow\": {m.HasShadow.ToString().ToLower()},");
                json.AppendLine($"      \"hasGlassHighlight\": {m.HasGlassHighlight.ToString().ToLower()},");
                json.AppendLine($"      \"hasValueText\": {m.HasValueText.ToString().ToLower()}");
                json.AppendLine(i < metrics.Count - 1 ? "    }," : "    }");
            }

            json.AppendLine("  ]");
            json.AppendLine("}");

            Debug.Log($"HUD Layout Verification JSON:\n{json}");

            // Verify the serialization is valid by checking key invariants
            Assert.AreEqual(ExpectedHudTileCount, metrics.Count);
            Assert.IsTrue(metrics.All(m => m.LayoutMinWidth >= ExpectedTileMinWidth - WidthTolerancePx));
            Assert.IsTrue(metrics.All(m => m.HasShadow && m.HasGlassHighlight && m.HasValueText));
        }
    }
}
