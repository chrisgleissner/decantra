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
        private const float SizeTolerance = 0.8f;
        private const float ColorTolerance = 0.03f;
        private const float StrokeMultiplier = 1.6f;
        private const float DarkenMultiplier = 0.8f;

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
        public IEnumerator SinkHeavyGlass_ContoursAreTwentyPercentDarkerThanRegularBottle()
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
                Assert.IsNotNull(regularContour, $"Regular contour not found: {contourName}");
                Assert.IsNotNull(sinkContour, $"Sink contour not found: {contourName}");

                Assert.AreEqual(regularContour.color.r * DarkenMultiplier, sinkContour.color.r, ColorTolerance,
                    $"Contour red channel not darkened by 20% for {contourName}");
                Assert.AreEqual(regularContour.color.g * DarkenMultiplier, sinkContour.color.g, ColorTolerance,
                    $"Contour green channel not darkened by 20% for {contourName}");
                Assert.AreEqual(regularContour.color.b * DarkenMultiplier, sinkContour.color.b, ColorTolerance,
                    $"Contour blue channel not darkened by 20% for {contourName}");
                Assert.AreEqual(regularContour.color.a, sinkContour.color.a, ColorTolerance,
                    $"Contour alpha should remain unchanged for {contourName}");
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
                var sinkContour = FindContourImage(sink, contourName);
                Assert.IsNotNull(regularContour, $"Regular contour not found: {contourName}");
                Assert.IsNotNull(sinkContour, $"Sink contour not found: {contourName}");

                float strokeX = ResolveStrokeThicknessUnits(regularContour, horizontal: true);
                float strokeY = ResolveStrokeThicknessUnits(regularContour, horizontal: false);
                float expectedDeltaX = 2f * strokeX * (StrokeMultiplier - 1f);
                float expectedDeltaY = 2f * strokeY * (StrokeMultiplier - 1f);

                float actualDeltaX = sinkContour.rectTransform.rect.width - regularContour.rectTransform.rect.width;
                float actualDeltaY = sinkContour.rectTransform.rect.height - regularContour.rectTransform.rect.height;

                Assert.AreEqual(expectedDeltaX, actualDeltaX, SizeTolerance,
                    $"Contour width expansion mismatch for {contourName}");
                Assert.AreEqual(expectedDeltaY, actualDeltaY, SizeTolerance,
                    $"Contour height expansion mismatch for {contourName}");
            }
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
            yield return "GlassBack";
            yield return "GlassFront";
            yield return "Rim";
            yield return "BottleNeck";
            yield return "BottleFlange";
            yield return "NeckInnerShadow";
        }

        private static float ResolveStrokeThicknessUnits(Image contour, bool horizontal)
        {
            if (contour == null || contour.sprite == null)
            {
                return 4f;
            }

            float ppu = contour.sprite.pixelsPerUnit;
            if (ppu <= 0f)
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

            return borderPixels / ppu;
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
    }
}
