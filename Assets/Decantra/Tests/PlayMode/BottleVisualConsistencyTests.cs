/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Decantra.Domain.Model;
using Decantra.Presentation;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public sealed class BottleVisualConsistencyTests
    {
        private const float HeightTolerance = 0.75f;

        [UnityTest]
        public IEnumerator BottleHeights_AreProportionalToCapacity()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            var heights = new Dictionary<int, float>();

            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                var bottle = CreateBottle(capacity, 0);
                bottleViews[i].Render(bottle);
            }

            yield return null;

            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                var outline = FindChildRect(bottleViews[i].transform, "Outline");
                Assert.IsNotNull(outline, "Outline rect was not found.");
                heights[capacity] = GetWorldHeight(outline);
            }

            for (int i = 0; i < capacities.Length; i++)
            {
                for (int j = i + 1; j < capacities.Length; j++)
                {
                    int a = capacities[i];
                    int b = capacities[j];
                    float ratio = heights[a] / heights[b];
                    float expected = a / (float)b;
                    Assert.AreEqual(expected, ratio, 0.02f,
                        $"Expected height ratio {a}:{b} to be {expected}, got {ratio}.");
                }
            }
        }

        [UnityTest]
        public IEnumerator LiquidHeights_MatchSlotCountsAcrossCapacities()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            int maxCapacity = capacities.Max();

            for (int fill = 0; fill <= maxCapacity; fill++)
            {
                var heights = new List<float>();

                for (int i = 0; i < capacities.Length; i++)
                {
                    int capacity = capacities[i];
                    if (fill > capacity) continue;

                    var bottle = CreateBottle(capacity, fill);
                    bottleViews[i].Render(bottle);
                }

                yield return null;

                for (int i = 0; i < capacities.Length; i++)
                {
                    int capacity = capacities[i];
                    if (fill > capacity) continue;

                    var slotRoot = FindSlotRoot(bottleViews[i]);
                    Assert.IsNotNull(slotRoot, "LiquidRoot rect was not found.");
                    float height = GetFilledWorldHeight(slotRoot);
                    heights.Add(height);
                }

                if (heights.Count <= 1) continue;

                float expected = heights[0];
                for (int i = 1; i < heights.Count; i++)
                {
                    Assert.AreEqual(expected, heights[i], HeightTolerance,
                        $"Filled height mismatch for {fill} slots: expected {expected}, got {heights[i]}.");
                }
            }
        }

        private static List<BottleView> FindBottleViews()
        {
            return Object.FindObjectsByType<BottleView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToList();
        }

        private static Bottle CreateBottle(int capacity, int filled)
        {
            var slots = new ColorId?[capacity];
            for (int i = 0; i < filled; i++)
            {
                slots[i] = ColorId.Red;
            }
            return new Bottle(slots);
        }

        private static RectTransform FindChildRect(Transform root, string name)
        {
            var child = root.Find(name);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        private static RectTransform FindSlotRoot(BottleView bottleView)
        {
            var slotRoot = bottleView.transform.Find("LiquidMask/LiquidRoot");
            if (slotRoot != null)
            {
                return slotRoot.GetComponent<RectTransform>();
            }
            return null;
        }

        private static float GetWorldHeight(RectTransform rect)
        {
            return rect.rect.height * rect.lossyScale.y;
        }

        private static float GetFilledWorldHeight(RectTransform slotRoot)
        {
            float maxLocal = 0f;
            for (int i = 0; i < slotRoot.childCount; i++)
            {
                var child = slotRoot.GetChild(i);
                if (child == null || !child.gameObject.activeSelf) continue;

                if (child.name.StartsWith("Segment_"))
                {
                    var rect = child.GetComponent<RectTransform>();
                    if (rect == null) continue;
                    float top = rect.anchoredPosition.y + rect.rect.height;
                    if (top > maxLocal)
                    {
                        maxLocal = top;
                    }
                }
            }

            return maxLocal * slotRoot.lossyScale.y;
        }
    }
}
