/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
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
        private const float SizeTolerance = 1.2f;
        private const float ColorTolerance = 0.02f;
        private const float StrokeMultiplier = 1.8f;
        private const float BottomStrokeMultiplier = 2.5f;

        [UnityTest]
        public IEnumerator SinkHeavyGlass_DisablesBottomMarkerObjects()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottle = FindBottleViews().First();
            bottle.SetLevelMaxCapacity(4);
            bottle.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            var basePlate = bottle.transform.Find("BasePlate")?.gameObject;
            var anchorCollar = bottle.transform.Find("AnchorCollar")?.gameObject;
            Assert.IsNotNull(basePlate, "BasePlate object not found.");
            Assert.IsNotNull(anchorCollar, "AnchorCollar object not found.");
            Assert.IsFalse(basePlate.activeSelf, "BasePlate must be disabled for sink bottles.");
            Assert.IsFalse(anchorCollar.activeSelf, "AnchorCollar must be disabled for sink bottles.");
        }

        [UnityTest]
        public IEnumerator SinkHeavyGlass_UsesNeutralStrokeColorAndBlackInternalMarkerLines()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var pair = FindBottleViews().Take(2).ToArray();
            Assert.AreEqual(2, pair.Length, "Expected at least two bottle views.");

            var regular = pair[0];
            var sink = pair[1];
            regular.SetLevelMaxCapacity(4);
            sink.SetLevelMaxCapacity(4);

            regular.Render(CreateBottle(4, 2, isSink: false));
            sink.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            foreach (string contourName in ContourNames())
            {
                var regularContour = FindContourImage(regular, contourName);
                var sinkContour = FindContourImage(sink, contourName);
                var outerStroke = FindSinkOverlayImage(sink, contourName, "_SinkOuterStroke");
                var markerLine = FindSinkOverlayImage(sink, contourName, "_SinkMarkerLine");
                Assert.IsNotNull(regularContour, $"Regular contour not found: {contourName}");
                Assert.IsNotNull(sinkContour, $"Sink contour not found: {contourName}");
                Assert.IsNotNull(outerStroke, $"Sink outer stroke not found: {contourName}");
                Assert.IsNotNull(markerLine, $"Sink marker line not found: {contourName}");

                Assert.AreEqual(regularContour.color.r, outerStroke.color.r, ColorTolerance, $"Outer stroke must keep regular red for {contourName}");
                Assert.AreEqual(regularContour.color.g, outerStroke.color.g, ColorTolerance, $"Outer stroke must keep regular green for {contourName}");
                Assert.AreEqual(regularContour.color.b, outerStroke.color.b, ColorTolerance, $"Outer stroke must keep regular blue for {contourName}");
                Assert.AreEqual(regularContour.color.a, outerStroke.color.a, ColorTolerance, $"Outer stroke must keep regular alpha for {contourName}");

                Assert.LessOrEqual(markerLine.color.r, 0.01f, $"Marker line must be black for {contourName}");
                Assert.LessOrEqual(markerLine.color.g, 0.01f, $"Marker line must be black for {contourName}");
                Assert.LessOrEqual(markerLine.color.b, 0.01f, $"Marker line must be black for {contourName}");
                Assert.Greater(markerLine.color.a, 0.95f, $"Marker line should remain fully opaque for {contourName}");
            }
        }

        [UnityTest]
        public IEnumerator SinkHeavyGlass_ContourStrokeExpansionMatchesMultiplierRule()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var pair = FindBottleViews().Take(2).ToArray();
            Assert.AreEqual(2, pair.Length, "Expected at least two bottle views.");

            var regular = pair[0];
            var sink = pair[1];
            regular.SetLevelMaxCapacity(4);
            sink.SetLevelMaxCapacity(4);

            regular.Render(CreateBottle(4, 2, isSink: false));
            sink.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            foreach (string contourName in ContourNames())
            {
                var regularContour = FindContourImage(regular, contourName);
                var sinkOuter = FindSinkOverlayImage(sink, contourName, "_SinkOuterStroke");
                Assert.IsNotNull(regularContour, $"Regular contour not found: {contourName}");
                Assert.IsNotNull(sinkOuter, $"Sink outer stroke not found: {contourName}");

                float strokeX = ResolveStrokeThicknessUnits(regularContour, horizontal: true);
                float strokeY = ResolveStrokeThicknessUnits(regularContour, horizontal: false);
                float expectedStrokeX = strokeX * StrokeMultiplier;
                float expectedStrokeY = strokeY * StrokeMultiplier;

                float regularInnerHalfWidth = regularContour.rectTransform.rect.width * 0.5f - strokeX;
                float regularInnerHalfHeight = regularContour.rectTransform.rect.height * 0.5f - strokeY;
                float sinkOuterHalfWidth = sinkOuter.rectTransform.rect.width * 0.5f;
                float sinkOuterHalfHeight = sinkOuter.rectTransform.rect.height * 0.5f;

                float actualStrokeX = sinkOuterHalfWidth - regularInnerHalfWidth;
                float actualStrokeY = sinkOuterHalfHeight - regularInnerHalfHeight;

                Assert.AreEqual(expectedStrokeX, actualStrokeX, SizeTolerance, $"Contour width stroke mismatch for {contourName}");
                Assert.AreEqual(expectedStrokeY, actualStrokeY, SizeTolerance, $"Contour height stroke mismatch for {contourName}");
            }

            var regularOutline = FindContourImage(regular, "Outline");
            var sinkBottomBand = sink.transform.Find("Outline_SinkBottomOuterBand")?.GetComponent<Image>();
            Assert.IsNotNull(regularOutline, "Regular outline not found.");
            Assert.IsNotNull(sinkBottomBand, "Sink bottom outer band not found.");

            float regularBottomStroke = ResolveStrokeThicknessUnits(regularOutline, horizontal: false);
            float actualBottomStroke = regularBottomStroke * StrokeMultiplier + sinkBottomBand.rectTransform.rect.height;
            float expectedBottomStroke = regularBottomStroke * BottomStrokeMultiplier;
            Assert.AreEqual(expectedBottomStroke, actualBottomStroke, SizeTolerance, "Bottom stroke should be 2.5x regular thickness.");
        }

        [UnityTest]
        public IEnumerator SinkHeadContours_AreLiftedWithoutBreakingBodyConnection()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var pair = FindBottleViews().Take(2).ToArray();
            Assert.AreEqual(2, pair.Length, "Expected at least two bottle views.");

            var regular = pair[0];
            var sink = pair[1];
            regular.SetLevelMaxCapacity(4);
            sink.SetLevelMaxCapacity(4);

            regular.Render(CreateBottle(4, 2, isSink: false));
            sink.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            foreach (string contourName in new[] { "Rim", "BottleNeck", "BottleFlange", "NeckInnerShadow" })
            {
                var regularContour = FindContourImage(regular, contourName);
                var sinkContour = FindContourImage(sink, contourName);
                Assert.IsNotNull(regularContour, $"{contourName} not found on regular bottle.");
                Assert.IsNotNull(sinkContour, $"{contourName} not found on sink bottle.");
                Assert.Greater(
                    sinkContour.rectTransform.anchoredPosition.y,
                    regularContour.rectTransform.anchoredPosition.y + 0.1f,
                    $"{contourName} should move upward for sink stroke compensation.");
            }

            var outlineOuter = FindSinkOverlayImage(sink, "Outline", "_SinkOuterStroke");
            var headOuters = new[]
            {
                FindSinkOverlayImage(sink, "Rim", "_SinkOuterStroke"),
                FindSinkOverlayImage(sink, "BottleNeck", "_SinkOuterStroke"),
                FindSinkOverlayImage(sink, "BottleFlange", "_SinkOuterStroke")
            };

            Assert.IsNotNull(outlineOuter, "Outline sink outer stroke missing.");
            Assert.IsTrue(headOuters.All(h => h != null), "Head sink outer stroke overlays are required.");

            float outlineTop = RectTop(outlineOuter.rectTransform);
            float headBottom = headOuters.Min(h => RectBottom(h.rectTransform));
            Assert.LessOrEqual(Mathf.Abs(headBottom - outlineTop), 3f, "Sink head must remain connected to the body without a visible gap or overlap.");
        }

        [UnityTest]
        public IEnumerator SinkHeavyGlass_KeepsLiquidAreaPixelIdenticalToRegularBottle()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var pair = FindBottleViews().Take(2).ToArray();
            Assert.AreEqual(2, pair.Length, "Expected at least two bottle views.");

            var regular = pair[0];
            var sink = pair[1];
            regular.SetLevelMaxCapacity(4);
            sink.SetLevelMaxCapacity(4);

            regular.Render(CreateBottle(4, 2, isSink: false));
            sink.Render(CreateBottle(4, 2, isSink: true));
            yield return null;

            var regularRoot = regular.SlotRoot;
            var sinkRoot = sink.SlotRoot;
            Assert.IsNotNull(regularRoot, "Regular SlotRoot not found.");
            Assert.IsNotNull(sinkRoot, "Sink SlotRoot not found.");

            Assert.AreEqual(regularRoot.rect.width, sinkRoot.rect.width, SizeTolerance, "Liquid width changed for sink bottle.");
            Assert.AreEqual(regularRoot.rect.height, sinkRoot.rect.height, SizeTolerance, "Liquid height changed for sink bottle.");
            Assert.AreEqual(regularRoot.anchoredPosition.x, sinkRoot.anchoredPosition.x, SizeTolerance, "Liquid X position changed for sink bottle.");
            Assert.AreEqual(regularRoot.anchoredPosition.y, sinkRoot.anchoredPosition.y, SizeTolerance, "Liquid Y position changed for sink bottle.");
        }

        [UnityTest]
        public IEnumerator SinkRendering_DoesNotChangeSolvabilityResults()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var generator = new LevelGenerator(new BfsSolver());
            var solver = new BfsSolver();
            var profile = LevelDifficultyEngine.GetProfile(20);
            var level = generator.Generate(682415, profile);
            Assert.IsNotNull(level, "Failed to generate regression level.");

            var before = solver.Solve(level, 250_000, 1_500, allowSinkMoves: true);

            var bottleViews = FindBottleViews();
            int maxCap = level.Bottles.Max(b => b.Capacity);
            int count = Mathf.Min(level.Bottles.Count, bottleViews.Count);
            for (int i = 0; i < count; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCap);
                bottleViews[i].Render(level.Bottles[i]);
            }

            yield return null;

            var after = solver.Solve(level, 250_000, 1_500, allowSinkMoves: true);
            Assert.AreEqual(before.Status, after.Status, "Solver status changed after sink rendering.");
            Assert.AreEqual(before.OptimalMoves, after.OptimalMoves, "Optimal move count changed after sink rendering.");
        }

        private static IEnumerable<string> ContourNames()
        {
            yield return "Outline";
            yield return "Rim";
            yield return "BottleNeck";
            yield return "BottleFlange";
        }

        private static float ResolveStrokeThicknessUnits(Image contour, bool horizontal)
        {
            if (contour == null || contour.sprite == null)
            {
                return 4f;
            }

            float borderPixels = horizontal
                ? Mathf.Max(contour.sprite.border.x, contour.sprite.border.z)
                : Mathf.Max(contour.sprite.border.y, contour.sprite.border.w);

            if (borderPixels <= 0f)
            {
                borderPixels = 4f;
            }

            float referencePpu = contour.canvas != null ? contour.canvas.referencePixelsPerUnit : 100f;
            float spritePpu = contour.sprite.pixelsPerUnit > 0f ? contour.sprite.pixelsPerUnit : referencePpu;
            return borderPixels * (referencePpu / spritePpu);
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

        private static Image FindContourImage(BottleView bottle, string contourName)
        {
            return bottle.transform.Find(contourName)?.GetComponent<Image>();
        }

        private static Image FindSinkOverlayImage(BottleView bottle, string contourName, string suffix)
        {
            if (bottle == null) return null;
            return bottle.transform.Find(contourName + suffix)?.GetComponent<Image>();
        }

        private static float RectTop(RectTransform rect)
        {
            return rect.anchoredPosition.y + rect.rect.height * 0.5f;
        }

        private static float RectBottom(RectTransform rect)
        {
            return rect.anchoredPosition.y - rect.rect.height * 0.5f;
        }
    }
}
