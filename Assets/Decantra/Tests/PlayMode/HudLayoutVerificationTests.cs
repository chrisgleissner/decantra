/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
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
        private const float ExpectedTopTileMinWidth = 300f;
        private const float ExpectedTileMinHeight = 140f;
        private const int ExpectedHudTileCount = 3; // Level, Moves, Score (Max Level and High Score moved to Score Details overlay)
        private const float TextPaddingPx = 64f; // Approximate padding from panel edge to text area (32px left + 32px right)
        private const float MinRowPaddingPx = 6f;
        private const float MinHudClearancePx = 6f;
        private const float RowAlignmentTolerancePx = 1f;
        private const float TopControlVisualTolerancePx = 14f;
        private const float GapEqualityTolerancePx = 1.5f;

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

        private struct VerticalBounds
        {
            public float Top;
            public float Bottom;
            public float Center => (Top + Bottom) * 0.5f;
        }

        private sealed class RowBounds
        {
            public float Top;
            public float Bottom;
            public readonly List<VerticalBounds> Children = new List<VerticalBounds>(3);
            public readonly List<RectTransform> ChildRects = new List<RectTransform>(3);
            public float Center => (Top + Bottom) * 0.5f;
        }

        private struct BottleRowEntry
        {
            public VerticalBounds Bounds;
            public RectTransform Rect;
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
        public IEnumerator BottleGrid_MaintainsRowPaddingAndHudClearance()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.IsNotNull(controller, "GameController not found.");

            int[] levels = { 1, 10, 21, 24, 36 };
            foreach (int level in levels)
            {
                yield return AssertVerticalGapInvariantsForLevel(controller, level, $"level {level}");
            }
        }

        [UnityTest]
        public IEnumerator BottleGrid_KeepsEqualVerticalGaps_AcrossResolutions()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.IsNotNull(controller, "GameController not found.");

            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            var resolutions = new[] { new Vector2Int(1080, 2400), new Vector2Int(1080, 1920) };
            for (int i = 0; i < resolutions.Length; i++)
            {
                var resolution = resolutions[i];
                Screen.SetResolution(resolution.x, resolution.y, false);
                yield return null;
                yield return null;
                yield return AssertVerticalGapInvariantsForLevel(controller, 21, $"{resolution.x}x{resolution.y}");
            }

            Screen.SetResolution(originalWidth, originalHeight, false);
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

            // Check top tiles have identical widths
            var topTileMetrics = metrics.Where(m =>
                m.Name == "LevelPanel" || m.Name == "MovesPanel" || m.Name == "ScorePanel").ToList();
            if (topTileMetrics.Count > 0)
            {
                var topWidth = topTileMetrics[0].LayoutMinWidth;
                foreach (var m in topTileMetrics)
                {
                    Assert.AreEqual(topWidth, m.LayoutMinWidth, WidthTolerancePx,
                        $"Top HUD tile '{m.Name}' has minWidth={m.LayoutMinWidth}, expected {topWidth} (±{WidthTolerancePx}px)");
                }
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

                Assert.AreEqual(ExpectedTopTileMinWidth, m.LayoutMinWidth, WidthTolerancePx,
                    $"HUD tile '{m.Name}' has minWidth={m.LayoutMinWidth}, expected {ExpectedTopTileMinWidth}");
                Assert.AreEqual(ExpectedTileMinHeight, m.LayoutMinHeight, WidthTolerancePx,
                    $"HUD tile '{m.Name}' has minHeight={m.LayoutMinHeight}, expected {ExpectedTileMinHeight}");
            }
        }

        [UnityTest]
        public IEnumerator ScoreDetails_OverlayExists()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var scoreDetailsOverlay = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == "ScoreDetailsOverlay" && g.hideFlags == HideFlags.None);
            Assert.IsNotNull(scoreDetailsOverlay, "ScoreDetailsOverlay should exist in scene.");
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
                var text = panel.GetComponentInChildren<Text>();
                if (text == null) continue;

                // Skip preferredWidth check for text with resizeTextForBestFit enabled
                // These texts automatically shrink to fit within their constrained bounds
                if (text.resizeTextForBestFit)
                {
                    // Verify the text has proper RectTransform constraints (padding)
                    var textRect = text.GetComponent<RectTransform>();
                    if (textRect != null)
                    {
                        // Check that text has horizontal padding applied (offsetMin.x and offsetMax.x)
                        var hasPadding = Mathf.Abs(textRect.offsetMin.x) > 1f || Mathf.Abs(textRect.offsetMax.x) > 1f;
                        Assert.IsTrue(hasPadding,
                            $"HUD tile '{panel.name}' text should have horizontal padding applied to RectTransform");
                    }
                    continue;
                }

                var metrics = ExtractTileMetrics(panel);
                // Use actual rendered width (from RectTransform) since tiles have flexibleWidth
                var availableWidth = metrics.Width - TextPaddingPx;

                // Text should fit within the actual rendered panel width with padding
                // Note: preferredWidth can be 0 if text hasn't been laid out yet
                if (metrics.TextPreferredWidth > 0 && metrics.Width > 0)
                {
                    Assert.LessOrEqual(metrics.TextPreferredWidth, availableWidth + WidthTolerancePx,
                        $"HUD tile '{panel.name}' text preferredWidth={metrics.TextPreferredWidth} exceeds " +
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
            Assert.IsTrue(metrics.All(m => m.LayoutMinWidth >= ExpectedTopTileMinWidth - WidthTolerancePx));
            Assert.IsTrue(metrics.All(m => m.HasShadow && m.HasGlassHighlight && m.HasValueText));
        }

        private static List<BottleView> GetControllerBottleViews(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "GameController.bottleViews field not found.");
            var views = field.GetValue(controller) as List<BottleView>;
            Assert.IsNotNull(views, "GameController.bottleViews is null.");
            Assert.AreEqual(9, views.Count, "Expected exactly 9 bottle views.");
            return views;
        }

        private static GameController ResolvePrimaryController()
        {
            var controllers = Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameController best = null;
            int bestScore = -1;
            for (int i = 0; i < controllers.Length; i++)
            {
                var candidate = controllers[i];
                if (candidate == null) continue;
                int score = 0;

                var bottleField = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
                var views = bottleField != null ? bottleField.GetValue(candidate) as List<BottleView> : null;
                if (views != null) score += views.Count;
                if (views != null && HasMatchingHudSafeLayout(views)) score += 1000;

                var hudField = typeof(GameController).GetField("hudView", BindingFlags.Instance | BindingFlags.NonPublic);
                var hudView = hudField != null ? hudField.GetValue(candidate) as HudView : null;
                if (hudView != null && hudView.gameObject.activeInHierarchy) score += 100;
                if (candidate.gameObject.activeInHierarchy) score += 10;

                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private static bool HasMatchingHudSafeLayout(List<BottleView> bottleViews)
        {
            return ResolveMatchingHudSafeLayout(bottleViews) != null;
        }

        private static HudSafeLayout ResolveMatchingHudSafeLayout(List<BottleView> bottleViews)
        {
            var bottleGrid = ResolveBottleGridRect(bottleViews);
            if (bottleGrid == null) return null;

            var safeLayouts = Object.FindObjectsByType<HudSafeLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var gridField = typeof(HudSafeLayout).GetField("bottleGrid", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gridField == null) return null;

            for (int i = 0; i < safeLayouts.Length; i++)
            {
                var layout = safeLayouts[i];
                if (layout == null) continue;
                var layoutGrid = gridField.GetValue(layout) as RectTransform;
                if (layoutGrid == bottleGrid)
                {
                    return layout;
                }
            }

            return null;
        }

        private static RectTransform ResolveBottleGridRect(List<BottleView> bottleViews)
        {
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null) continue;
                if (view.transform.parent is RectTransform parent)
                {
                    return parent;
                }
            }

            return null;
        }

        private static List<RectTransform> ResolveTopControlRects(GameController controller)
        {
            var names = new[] { "ResetButton", "OptionsButton", "StarsButton" };
            var result = new List<RectTransform>(3);
            var hudRoot = ResolveHudRoot(controller);
            if (hudRoot == null) return result;

            for (int i = 0; i < names.Length; i++)
            {
                var child = FindDescendantByName(hudRoot, names[i]);
                if (child == null) continue;
                var button = child.GetComponent<Button>();
                var graphicRect = button != null && button.targetGraphic != null ? button.targetGraphic.rectTransform : null;
                var rect = graphicRect != null ? graphicRect : child.GetComponent<RectTransform>();
                if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                result.Add(rect);
            }

            return result;
        }

        private static Transform ResolveHudRoot(GameController controller)
        {
            var hudField = typeof(GameController).GetField("hudView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hudField == null) return null;
            var hudView = hudField.GetValue(controller) as HudView;
            if (hudView == null) return null;
            return hudView.transform.parent;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var found = FindDescendantByName(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private static List<RowBounds> CollectBottleRows(List<BottleView> bottleViews, RectTransform referenceRect)
        {
            var bounds = new List<BottleRowEntry>(bottleViews.Count);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null) continue;
                var childRect = view.GetComponent<RectTransform>();
                if (childRect == null) continue;
                if (!childRect.gameObject.activeInHierarchy) continue;
                bounds.Add(new BottleRowEntry
                {
                    Bounds = GetBoundsInReference(childRect, referenceRect),
                    Rect = childRect
                });
            }

            if (bounds.Count < 3)
            {
                return new List<RowBounds>(0);
            }
            bounds.Sort((a, b) => b.Bounds.Bottom.CompareTo(a.Bounds.Bottom)); // Top rows first by bottom edge

            int rowCount = bounds.Count / 3;
            var rows = new List<RowBounds>(rowCount);
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                int offset = rowIndex * 3;
                var row = new RowBounds
                {
                    Top = Mathf.Max(bounds[offset].Bounds.Top, Mathf.Max(bounds[offset + 1].Bounds.Top, bounds[offset + 2].Bounds.Top)),
                    Bottom = Mathf.Min(bounds[offset].Bounds.Bottom, Mathf.Min(bounds[offset + 1].Bounds.Bottom, bounds[offset + 2].Bounds.Bottom))
                };
                row.Children.Add(bounds[offset].Bounds);
                row.Children.Add(bounds[offset + 1].Bounds);
                row.Children.Add(bounds[offset + 2].Bounds);
                row.ChildRects.Add(bounds[offset].Rect);
                row.ChildRects.Add(bounds[offset + 1].Rect);
                row.ChildRects.Add(bounds[offset + 2].Rect);
                rows.Add(row);
            }

            return rows;
        }

        private static VerticalBounds GetBoundsInReference(RectTransform rect, RectTransform referenceRect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float top = float.MinValue;
            float bottom = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                var local = referenceRect.InverseTransformPoint(corners[i]);
                top = Mathf.Max(top, local.y);
                bottom = Mathf.Min(bottom, local.y);
            }

            return new VerticalBounds
            {
                Top = top,
                Bottom = bottom
            };
        }

        private static VerticalBounds GetBoundsInWorld(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float top = float.MinValue;
            float bottom = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                top = Mathf.Max(top, corners[i].y);
                bottom = Mathf.Min(bottom, corners[i].y);
            }

            return new VerticalBounds
            {
                Top = top,
                Bottom = bottom
            };
        }

        private static VerticalBounds GetRowWorldBounds(RowBounds row)
        {
            float top = float.MinValue;
            float bottom = float.MaxValue;
            for (int i = 0; i < row.ChildRects.Count; i++)
            {
                var bounds = GetBoundsInWorld(row.ChildRects[i]);
                top = Mathf.Max(top, bounds.Top);
                bottom = Mathf.Min(bottom, bounds.Bottom);
            }

            return new VerticalBounds
            {
                Top = top,
                Bottom = bottom
            };
        }

        private static Dictionary<RectTransform, BottleView> BuildBottleLookupByRect(List<BottleView> bottleViews)
        {
            var map = new Dictionary<RectTransform, BottleView>(bottleViews.Count);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var view = bottleViews[i];
                if (view == null) continue;
                var rect = view.GetComponent<RectTransform>();
                if (rect == null) continue;
                map[rect] = view;
            }

            return map;
        }

        private static float GetRowVisualTopInWorld(RowBounds row, Dictionary<RectTransform, BottleView> bottleByRect)
        {
            float top = float.MinValue;
            for (int i = 0; i < row.ChildRects.Count; i++)
            {
                var rect = row.ChildRects[i];
                if (rect == null) continue;
                if (!bottleByRect.TryGetValue(rect, out var view) || view == null)
                {
                    top = Mathf.Max(top, GetBoundsInWorld(rect).Top);
                    continue;
                }

                var bounds = GetBottleVisualBoundsInWorld(view);
                top = Mathf.Max(top, bounds.Top);
            }

            return top;
        }

        private static VerticalBounds GetBottleVisualBoundsInWorld(BottleView view)
        {
            var fallbackRect = view.GetComponent<RectTransform>();
            if (fallbackRect == null)
            {
                return new VerticalBounds { Top = 0f, Bottom = 0f };
            }

            var outlineField = typeof(BottleView).GetField("outline", BindingFlags.Instance | BindingFlags.NonPublic);
            var outlineImage = outlineField != null ? outlineField.GetValue(view) as Image : null;
            var outlineRect = outlineImage != null ? outlineImage.rectTransform : null;
            if (outlineRect == null || !outlineRect.gameObject.activeInHierarchy)
            {
                return GetBoundsInWorld(fallbackRect);
            }

            return GetBoundsInWorld(outlineRect);
        }

        private static float GetTopControlsBottomInWorld(List<RectTransform> controls)
        {
            float minBottom = float.MaxValue;
            for (int i = 0; i < controls.Count; i++)
            {
                var bounds = GetBoundsInWorld(controls[i]);
                minBottom = Mathf.Min(minBottom, bounds.Bottom);
            }

            return minBottom;
        }

        private static float GetRectTopInWorld(RectTransform rect)
        {
            var bounds = GetBoundsInWorld(rect);
            return Mathf.Max(bounds.Top, bounds.Bottom);
        }

        private static RectTransform ResolveBottleAreaRect(List<BottleView> bottleViews)
        {
            var layout = ResolveMatchingHudSafeLayout(bottleViews);
            if (layout == null) return null;
            var areaField = typeof(HudSafeLayout).GetField("bottleArea", BindingFlags.Instance | BindingFlags.NonPublic);
            return areaField != null ? areaField.GetValue(layout) as RectTransform : null;
        }

        private static IEnumerator AssertVerticalGapInvariantsForLevel(GameController controller, int level, string context)
        {
            controller.LoadLevel(level, 10991 + level);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var bottleViews = GetControllerBottleViews(controller);
            var bottleGrid = ResolveBottleGridRect(bottleViews);
            Assert.IsNotNull(bottleGrid, $"Bottle grid not found for {context}.");
            var bottleArea = ResolveBottleAreaRect(bottleViews);
            Assert.IsNotNull(bottleArea, $"Bottle area not found for {context}.");
            var topControls = ResolveTopControlRects(controller);
            Assert.GreaterOrEqual(topControls.Count, 2, $"Top controls not found for {context}.");

            var rows = CollectBottleRows(bottleViews, bottleGrid);
            Assert.GreaterOrEqual(rows.Count, 3, $"Expected 3 populated rows for {context}, but got {rows.Count}.");
            rows.Sort((a, b) => Mathf.Max(b.Top, b.Bottom).CompareTo(Mathf.Max(a.Top, a.Bottom)));
            rows = rows.Take(3).ToList();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                Assert.GreaterOrEqual(row.Children.Count, 3, $"Row {rowIndex + 1} has fewer than 3 bottles for {context}.");
                float baselineBottom = row.Children[0].Bottom;
                for (int i = 1; i < row.Children.Count; i++)
                {
                    Assert.AreEqual(baselineBottom, row.Children[i].Bottom, RowAlignmentTolerancePx,
                        $"Row {rowIndex + 1} bottles do not share bottom Y for {context}.");
                }
            }

            var bottleByRect = BuildBottleLookupByRect(bottleViews);
            float topControlLower = GetTopControlsBottomInWorld(topControls);
            float topRowVisualTop = GetRowVisualTopInWorld(rows[0], bottleByRect);
            float topClearance = topControlLower - topRowVisualTop;
            float topEffectiveClearance = topClearance + TopControlVisualTolerancePx;
            Assert.GreaterOrEqual(topEffectiveClearance, MinHudClearancePx,
                $"Top-row clearance to top controls too small for {context}: raw={topClearance}px effective={topEffectiveClearance}px.");

            var rowWorld0 = GetRowWorldBounds(rows[0]);
            var rowWorld1 = GetRowWorldBounds(rows[1]);
            var rowWorld2 = GetRowWorldBounds(rows[2]);
            float bottomEdge = GetBoundsInWorld(bottleArea).Bottom;

            float gapA = topControlLower - rowWorld0.Top;
            float gapB = rowWorld0.Bottom - rowWorld1.Top;
            float gapC = rowWorld1.Bottom - rowWorld2.Top;
            float gapD = rowWorld2.Bottom - bottomEdge;

            Assert.Greater(gapA, 0f, $"Gap A must be > 0 for {context}. gapA={gapA}");
            Assert.Greater(gapD, 0f, $"Gap D must be > 0 for {context}. gapD={gapD}");
            Assert.AreEqual(gapA, gapB, GapEqualityTolerancePx, $"Gap A != Gap B for {context}. A={gapA}, B={gapB}");
            Assert.AreEqual(gapA, gapC, GapEqualityTolerancePx, $"Gap A != Gap C for {context}. A={gapA}, C={gapC}");
            Assert.AreEqual(gapA, gapD, GapEqualityTolerancePx, $"Gap A != Gap D for {context}. A={gapA}, D={gapD}");

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                for (int c = 0; c < row.Children.Count; c++)
                {
                    Assert.LessOrEqual(row.Children[c].Top, row.Top + RowAlignmentTolerancePx,
                        $"Bottle top exceeds row top for row {i + 1} in {context}.");
                    Assert.GreaterOrEqual(row.Children[c].Bottom, row.Bottom - RowAlignmentTolerancePx,
                        $"Bottle bottom exceeds row bottom for row {i + 1} in {context}.");
                }
            }
        }

    }
}
