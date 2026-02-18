/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Decantra.Domain.Generation;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using Decantra.Presentation;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    public sealed class SinkIndicatorVisualSafetyTests
    {
        private const float WidthTolerance = 0.5f;
        private const float RatioTolerance = 0.005f;
        private const float SpacingTolerance = 0.01f;
        private const float MinContrastDelta = 0.1f;

        [UnityTest]
        public IEnumerator SinkIndicator_StaysWithinBottleWidth_AndMaxHeightRatio()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottle = FindBottleViews().First();
            bottle.SetLevelMaxCapacity(4);
            bottle.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            var indicatorEdge = FindSinkIndicatorEdge(bottle);
            var outline = FindOutlineRect(bottle);
            Assert.IsNotNull(indicatorEdge, "Sink indicator edge rect not found.");
            Assert.IsNotNull(outline, "Bottle outline rect not found.");

            float indicatorWidth = indicatorEdge.rect.width;
            float outlineWidth = outline.rect.width;
            Assert.LessOrEqual(indicatorWidth, outlineWidth + WidthTolerance,
                $"Indicator width {indicatorWidth} exceeds outline width {outlineWidth}.");

            float indicatorHeight = indicatorEdge.rect.height;
            float maxAllowedHeight = outline.rect.height * SinkIndicatorDesignTokens.MaxHeightRatio;
            Assert.LessOrEqual(indicatorHeight, maxAllowedHeight + RatioTolerance,
                $"Indicator height {indicatorHeight} exceeds max ratio bound {maxAllowedHeight}.");
        }

        [UnityTest]
        public IEnumerator SinkIndicator_DoesNotOverlapAdjacentBottle_AndSpacingUnchanged()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottles = FindBottleViews();
            Assert.GreaterOrEqual(bottles.Count, 2, "Expected at least two bottle views.");

            var pair = bottles.OrderBy(v => v.transform.position.x).Take(2).ToArray();
            var sinkView = pair[0];
            var adjacentView = pair[1];

            float spacingBefore = Mathf.Abs(adjacentView.transform.position.x - sinkView.transform.position.x);

            sinkView.SetLevelMaxCapacity(4);
            adjacentView.SetLevelMaxCapacity(4);
            sinkView.Render(CreateBottle(4, 2, isSink: true));
            adjacentView.Render(CreateBottle(4, 2, isSink: false));
            yield return null;

            float spacingAfter = Mathf.Abs(adjacentView.transform.position.x - sinkView.transform.position.x);
            Assert.AreEqual(spacingBefore, spacingAfter, SpacingTolerance,
                "Bottle spacing changed after sink indicator rendering.");

            var indicatorRect = GetWorldRect(FindSinkIndicatorEdge(sinkView));
            var adjacentRect = GetWorldRect(adjacentView.transform as RectTransform);
            Assert.IsFalse(indicatorRect.Overlaps(adjacentRect),
                "Sink indicator overlaps adjacent bottle bounds.");
        }

        [UnityTest]
        public IEnumerator SinkIndicator_ContrastVisibleOnLightAndDark_AndEvidenceCaptured()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottles = FindBottleViews().OrderBy(v => v.transform.position.x).ToList();
            Assert.GreaterOrEqual(bottles.Count, 2, "Expected at least two bottle views.");

            var sinkView = bottles[0];
            var normalView = bottles[1];
            sinkView.SetLevelMaxCapacity(4);
            normalView.SetLevelMaxCapacity(4);
            sinkView.Render(CreateBottle(4, 2, isSink: true));
            normalView.Render(CreateBottle(4, 2, isSink: false));

            var background = FindBackgroundImage();
            Assert.IsNotNull(background, "Background image was not found.");

            string outputDir = GetArtifactOutputDirectory();
            Directory.CreateDirectory(outputDir);

            Color original = background.color;
            try
            {
                yield return ValidateContrastAndCapture(
                    sinkView,
                    background,
                    new Color(0.94f, 0.97f, 1f, 1f),
                    Path.Combine(outputDir, "sink_indicator_light_test.png"),
                    "light");

                yield return ValidateContrastAndCapture(
                    sinkView,
                    background,
                    new Color(0.08f, 0.1f, 0.16f, 1f),
                    Path.Combine(outputDir, "sink_indicator_dark_test.png"),
                    "dark");

                background.color = original;
                background.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return CaptureAndAssertScreenshot(Path.Combine(outputDir, "sink_indicator_side_by_side_test.png"));
            }
            finally
            {
                background.color = original;
                background.SetAllDirty();
            }
        }

        [UnityTest]
        public IEnumerator SinkIndicator_Rendering_DoesNotChangeSolvabilityResults()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var generator = new LevelGenerator(new BfsSolver());
            var solver = new BfsSolver();
            var profile = LevelDifficultyEngine.GetProfile(20);
            var level = generator.Generate(682415, profile);
            Assert.IsNotNull(level, "Failed to generate regression level.");

            var before = solver.Solve(level, 2_000_000, 10_000, allowSinkMoves: true);

            var bottleViews = FindBottleViews();
            int maxCap = level.Bottles.Max(b => b.Capacity);
            int count = Mathf.Min(level.Bottles.Count, bottleViews.Count);
            for (int i = 0; i < count; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCap);
                bottleViews[i].Render(level.Bottles[i]);
            }

            yield return null;

            var after = solver.Solve(level, 2_000_000, 10_000, allowSinkMoves: true);
            Assert.AreEqual(before.Status, after.Status, "Solver status changed after sink visual rendering.");
            Assert.AreEqual(before.OptimalMoves, after.OptimalMoves, "Optimal move count changed after sink visual rendering.");
        }

        private static IEnumerator ValidateContrastAndCapture(
            BottleView sinkView,
            Image background,
            Color backgroundColor,
            string screenshotPath,
            string label)
        {
            background.color = backgroundColor;
            background.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            float contrast = MeasureIndicatorContrast(sinkView);
            Assert.GreaterOrEqual(contrast, MinContrastDelta,
                $"Sink indicator contrast too low on {label} background. Measured={contrast}.");

            yield return CaptureAndAssertScreenshot(screenshotPath);
        }

        private static IEnumerator CaptureAndAssertScreenshot(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ScreenCapture.CaptureScreenshot(path);
            float timeout = 5f;
            while (timeout > 0f)
            {
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    if (info.Length > 0)
                    {
                        yield break;
                    }
                }

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail($"Expected screenshot artifact was not written: {path}");
        }

        private static float MeasureIndicatorContrast(BottleView sinkView)
        {
            var indicatorCore = FindSinkIndicatorCore(sinkView);
            Assert.IsNotNull(indicatorCore, "Sink indicator core rect not found.");

            var worldCenter = indicatorCore.TransformPoint(indicatorCore.rect.center);
            Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(null, worldCenter);

            float localBodyOffset = indicatorCore.rect.height * 2f;
            var worldAbove = indicatorCore.TransformPoint(new Vector3(0f, localBodyOffset, 0f));
            Vector2 aboveScreen = RectTransformUtility.WorldToScreenPoint(null, worldAbove);

            var indicatorColor = ReadScreenPixel(centerScreen);
            var bottleBodyColor = ReadScreenPixel(aboveScreen);

            return Mathf.Abs(Luminance(indicatorColor) - Luminance(bottleBodyColor));
        }

        private static Color ReadScreenPixel(Vector2 screenPosition)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(screenPosition.x), 0, Screen.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(screenPosition.y), 0, Screen.height - 1);

            var texture = new Texture2D(1, 1, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            texture.Apply();
            Color pixel = texture.GetPixel(0, 0);
            Object.Destroy(texture);
            return pixel;
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }

        private static List<BottleView> FindBottleViews()
        {
            var grid = GameObject.Find("BottleGrid")?.transform;
            if (grid == null)
            {
                return Object.FindObjectsByType<BottleView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .OrderBy(v => v.name)
                    .ToList();
            }

            var result = new List<BottleView>(9);
            for (int i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;
                var view = child.GetComponent<BottleView>();
                if (view != null)
                {
                    result.Add(view);
                }
            }

            return result;
        }

        private static Bottle CreateBottle(int capacity, int filled, bool isSink)
        {
            var slots = new ColorId?[capacity];
            for (int i = 0; i < filled; i++)
            {
                slots[i] = ColorId.Red;
            }

            return new Bottle(slots, isSink);
        }

        private static RectTransform FindSinkIndicatorEdge(BottleView bottle)
        {
            return bottle.transform.Find("LiquidMask/LiquidRoot/BasePlate")?.GetComponent<RectTransform>();
        }

        private static RectTransform FindSinkIndicatorCore(BottleView bottle)
        {
            return bottle.transform.Find("LiquidMask/LiquidRoot/AnchorCollar")?.GetComponent<RectTransform>();
        }

        private static RectTransform FindOutlineRect(BottleView bottle)
        {
            return bottle.transform.Find("Outline")?.GetComponent<RectTransform>();
        }

        private static Image FindBackgroundImage()
        {
            return GameObject.Find("Background")?.GetComponent<Image>();
        }

        private static Rect GetWorldRect(RectTransform rect)
        {
            Assert.IsNotNull(rect, "Expected rect transform was null.");

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }

        private static string GetArtifactOutputDirectory()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(root, "Artifacts", "sink-indicator-validation");
        }
    }
}
