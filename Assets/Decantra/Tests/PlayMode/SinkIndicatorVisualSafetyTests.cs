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

        [UnityTest]
        public IEnumerator SinkStyle_UsesBlackContours_WithoutLegacyOverlayObjects()
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

                Assert.AreEqual(regularContour.rectTransform.rect.width, sinkContour.rectTransform.rect.width, SizeTolerance,
                    $"Contour width changed for {contourName}");
                Assert.AreEqual(regularContour.rectTransform.rect.height, sinkContour.rectTransform.rect.height, SizeTolerance,
                    $"Contour height changed for {contourName}");

                Assert.LessOrEqual(sinkContour.color.r, 0.01f, $"Sink contour must be black for {contourName}");
                Assert.LessOrEqual(sinkContour.color.g, 0.01f, $"Sink contour must be black for {contourName}");
                Assert.LessOrEqual(sinkContour.color.b, 0.01f, $"Sink contour must be black for {contourName}");
                Assert.AreEqual(regularContour.color.a, sinkContour.color.a, ColorTolerance,
                    $"Sink contour alpha must match regular alpha for {contourName}");

                Assert.IsNull(FindSinkOverlayImage(sink, contourName, "_SinkOuterStroke"),
                    $"Legacy sink outer stroke overlay should not exist for {contourName}");
                Assert.IsNull(FindSinkOverlayImage(sink, contourName, "_SinkMarkerLine"),
                    $"Legacy sink marker line overlay should not exist for {contourName}");
            }

            Assert.IsNull(sink.transform.Find("Outline_SinkBottomOuterBand"), "Legacy bottom outer band should not exist.");
            Assert.IsNull(sink.transform.Find("Outline_SinkBottomMarkerBand"), "Legacy bottom marker band should not exist.");
        }

        [UnityTest]
        public IEnumerator SinkStyle_HeavyBase_IsExactly2xAndExtendsDownwardOnly()
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

            var regularOutline = FindContourImage(regular, "Outline");
            var sinkOutline = FindContourImage(sink, "Outline");
            var heavyBase = sink.transform.Find("Outline_SinkHeavyBase")?.GetComponent<Image>();

            Assert.IsNotNull(regularOutline, "Regular outline not found.");
            Assert.IsNotNull(sinkOutline, "Sink outline not found.");
            Assert.IsNotNull(heavyBase, "Sink heavy base band not found.");
            Assert.IsTrue(heavyBase.gameObject.activeSelf, "Sink heavy base should be active.");

            float regularBottomStroke = ResolveStrokeThicknessUnits(regularOutline, horizontal: false);
            float sinkBottomStroke = ResolveStrokeThicknessUnits(sinkOutline, horizontal: false);
            Assert.AreEqual(regularBottomStroke, sinkBottomStroke, SizeTolerance,
                "Sink base stroke thickness must match regular outline stroke thickness.");

            Assert.AreEqual(regularBottomStroke, heavyBase.rectTransform.rect.height, SizeTolerance,
                "Heavy base extension must equal one regular bottom stroke.");

            float sinkOutlineBottom = RectBottom(sinkOutline.rectTransform);
            float heavyBaseTop = RectTop(heavyBase.rectTransform);
            float heavyBaseBottom = RectBottom(heavyBase.rectTransform);
            float regularOutlineBottom = RectBottom(regularOutline.rectTransform);

            Assert.AreEqual(regularOutlineBottom, sinkOutlineBottom, SizeTolerance,
                "Sink outline geometry must match regular outline geometry.");
            Assert.AreEqual(sinkOutlineBottom, heavyBaseTop, SizeTolerance,
                "Extra heavy-base thickness must start at regular bottom edge and extend downward only.");
            Assert.Less(heavyBaseBottom, sinkOutlineBottom - 0.1f,
                "Heavy base bottom must be below the regular outline bottom edge.");

            float sinkHeavyBaseTotalThickness = sinkBottomStroke + heavyBase.rectTransform.rect.height;
            Assert.AreEqual(regularBottomStroke * 2f, sinkHeavyBaseTotalThickness, SizeTolerance,
                "Sink heavy base must be exactly 2x regular bottom stroke thickness.");
        }

        [UnityTest]
        public IEnumerator SinkStyle_KeepsLiquidAreaPixelIdenticalToRegularBottle()
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

            Assert.AreEqual(regularRoot.rect.width, sinkRoot.rect.width, SizeTolerance, "Liquid width changed for black bottle.");
            Assert.AreEqual(regularRoot.rect.height, sinkRoot.rect.height, SizeTolerance, "Liquid height changed for black bottle.");
            Assert.AreEqual(regularRoot.anchoredPosition.x, sinkRoot.anchoredPosition.x, SizeTolerance, "Liquid X position changed for black bottle.");
            Assert.AreEqual(regularRoot.anchoredPosition.y, sinkRoot.anchoredPosition.y, SizeTolerance, "Liquid Y position changed for black bottle.");
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
