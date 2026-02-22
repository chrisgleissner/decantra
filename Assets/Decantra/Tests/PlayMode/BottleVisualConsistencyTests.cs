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

        // Outline geometry constants (must match SceneBootstrap values).
        private const float OutlineOuterHeight = 372f;
        private const float OutlineBorderThickness = 12f; // 12 sprite-px / effectivePPU 1 = 12 canvas units
        private const float ExpectedGapFraction = OutlineBorderThickness / OutlineOuterHeight;

        [UnityTest]
        public IEnumerator SlotRootHeights_AreProportionalToCapacity()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            int levelMaxCapacity = capacities.Max();
            var heights = new Dictionary<int, float>();

            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                bottleViews[i].SetLevelMaxCapacity(levelMaxCapacity);
                var bottle = CreateBottle(capacity, 0);
                bottleViews[i].Render(bottle);
            }

            yield return null;

            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                var slotRoot = FindSlotRoot(bottleViews[i]);
                Assert.IsNotNull(slotRoot, "slotRoot rect was not found.");
                heights[capacity] = GetWorldHeight(slotRoot);
            }

            float referenceHeight = heights[levelMaxCapacity];
            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                float expected = referenceHeight * (capacity / (float)levelMaxCapacity);
                Assert.AreEqual(expected, heights[capacity], HeightTolerance,
                    $"Expected slotRoot height for cap-{capacity} to be {expected}, got {heights[capacity]}.");
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
            for (int i = 0; i < capacities.Length; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCapacity);
            }

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
                    Assert.IsNotNull(slotRoot, "slotRoot rect was not found.");
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

        private static Bottle CreateBottle(int capacity, int filled)
        {
            var slots = new ColorId?[capacity];
            for (int i = 0; i < filled; i++)
            {
                slots[i] = ColorId.Red;
            }
            return new Bottle(slots);
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

        /// <summary>
        /// Returns the world-space Y of the top edge of a RectTransform.
        /// </summary>
        private static float GetWorldTopY(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // corners[1] = top-left, corners[2] = top-right
            return corners[1].y;
        }

        /// <summary>
        /// Returns the world-space Y of the bottom edge of a RectTransform.
        /// </summary>
        private static float GetWorldBottomY(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // corners[0] = bottom-left, corners[3] = bottom-right
            return corners[0].y;
        }

        /// <summary>
        /// Test: the first filled segment's bottom must align with the LiquidRoot bottom edge,
        /// which represents the interior bottom of the bottle (Invariant 1 — Interior Fill Bounds:
        /// segments start at interiorBottomY, not above it).
        ///
        /// Verifies for bottles of different capacities in the same level.
        /// </summary>
        [UnityTest]
        public IEnumerator EmptyBottle_FirstSegment_AlignsToInteriorBottom()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            int maxCapacity = 10;

            for (int i = 0; i < capacities.Length; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCapacity);
                // Render exactly 1 filled slot so we can check the first segment's bottom
                bottleViews[i].Render(CreateBottle(capacities[i], 1));
            }

            yield return null;

            for (int i = 0; i < capacities.Length; i++)
            {
                var slotRoot = FindSlotRoot(bottleViews[i]);
                Assert.IsNotNull(slotRoot, $"Bottle_{i + 1}: LiquidRoot RectTransform not found.");

                // Find the first active Segment_1
                Transform segmentTransform = null;
                for (int c = 0; c < slotRoot.childCount; c++)
                {
                    var child = slotRoot.GetChild(c);
                    if (child.gameObject.activeSelf && child.name.StartsWith("Segment_"))
                    {
                        segmentTransform = child;
                        break;
                    }
                }

                Assert.IsNotNull(segmentTransform,
                    $"Bottle_{i + 1} cap-{capacities[i]}: No active Segment_ child found in LiquidRoot.");

                var segmentRect = segmentTransform.GetComponent<RectTransform>();
                Assert.IsNotNull(segmentRect);

                float slotRootBottomWorld = GetWorldBottomY(slotRoot);
                float segmentBottomWorld = GetWorldBottomY(segmentRect);

                Assert.AreEqual(slotRootBottomWorld, segmentBottomWorld, HeightTolerance,
                    $"cap-{capacities[i]}: first segment bottom ({segmentBottomWorld:F3}) must align " +
                    $"with LiquidRoot bottom / interior bottom ({slotRootBottomWorld:F3}).");
            }
        }

        /// <summary>
        /// Test: a full bottle's liquid top must be exactly one border-thickness below the outline top,
        /// and this gap must be consistent across all bottle capacities in the same level.
        ///
        /// Border thickness = 12 canvas units (sprite border 12 px / effective PPU 1 = 12 cu).
        /// The gap in world-space is scale-invariant when expressed as a fraction of the outline height.
        ///
        /// Expected gap fraction = borderThickness / OutlineHeight = 12 / 372 ≈ 0.0323.
        /// Invariant B: gap is consistent across capacities (max-min ≤ tolerance).
        /// </summary>
        [UnityTest]
        public IEnumerator FullBottle_LiquidTopAligned_ToInnerBodyTop_AcrossCapacities()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            int maxCapacity = 10;

            for (int i = 0; i < capacities.Length; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCapacity);
                bottleViews[i].Render(CreateBottle(capacities[i], capacities[i]));
            }

            yield return null;

            var gaps = new float[capacities.Length];

            // Border thickness expressed as a fraction of the outline outer height.
            // This ratio is resolution-independent.
            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                var outlineRect = bottleViews[i].transform.Find("Outline")?.GetComponent<RectTransform>();
                var slotRoot = FindSlotRoot(bottleViews[i]);

                Assert.IsNotNull(outlineRect, $"Bottle_{i + 1}: Outline RectTransform not found.");
                Assert.IsNotNull(slotRoot, $"Bottle_{i + 1}: LiquidRoot RectTransform not found.");

                float outlineHeightWorld = GetWorldHeight(outlineRect);
                float outlineTopWorld = GetWorldTopY(outlineRect);
                float slotRootTopWorld = GetWorldTopY(slotRoot);

                gaps[i] = outlineTopWorld - slotRootTopWorld;

                float expectedGapWorld = outlineHeightWorld * ExpectedGapFraction;
                Assert.AreEqual(expectedGapWorld, gaps[i], HeightTolerance,
                    $"Full cap-{capacity}: outline top minus liquid top should be " +
                    $"{expectedGapWorld:F2} world units (one border thickness) but was {gaps[i]:F2}.");
            }

            float minGap = gaps[0];
            float maxGap = gaps[0];
            for (int i = 1; i < gaps.Length; i++)
            {
                if (gaps[i] < minGap) minGap = gaps[i];
                if (gaps[i] > maxGap) maxGap = gaps[i];
            }

            Assert.LessOrEqual(maxGap - minGap, HeightTolerance * 2f,
                $"Top gap is not consistent across capacities: min={minGap:F2}, max={maxGap:F2} world units.");
        }

        /// <summary>
        /// Test: the LiquidMask bottom must sit above the Outline bottom by exactly one border
        /// thickness, so no liquid can ever be rendered behind the bottom border of the bottle.
        ///
        /// This is the primary fix for the reported defect ("bottom segment overlapped by border").
        /// Expected gap fraction = 12 / 372 ≈ 0.0323 of the outline height (resolution-independent).
        ///
        /// FAILS with the old code (LiquidMask bottom == Outline bottom, gap ≈ 0).
        /// PASSES with the fix  (LiquidMask bottom == Outline inner bottom, gap ≈ border thickness).
        /// </summary>
        [UnityTest]
        public IEnumerator BottomBorder_NotOverlappedByLiquid()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var bottleViews = FindBottleViews();
            Assert.GreaterOrEqual(bottleViews.Count, 4, "Expected at least 4 bottle views.");

            int[] capacities = { 4, 6, 8, 10 };
            int maxCapacity = 10;

            for (int i = 0; i < capacities.Length; i++)
            {
                bottleViews[i].SetLevelMaxCapacity(maxCapacity);
                bottleViews[i].Render(CreateBottle(capacities[i], 1));
            }

            yield return null;

            for (int i = 0; i < capacities.Length; i++)
            {
                int capacity = capacities[i];
                var outlineRect = bottleViews[i].transform.Find("Outline")?.GetComponent<RectTransform>();
                var liquidMaskRect = bottleViews[i].transform.Find("LiquidMask")?.GetComponent<RectTransform>();

                Assert.IsNotNull(outlineRect, $"Bottle_{i + 1}: Outline RectTransform not found.");
                Assert.IsNotNull(liquidMaskRect, $"Bottle_{i + 1}: LiquidMask RectTransform not found.");

                float outlineHeightWorld = GetWorldHeight(outlineRect);
                float outlineBottomWorld = GetWorldBottomY(outlineRect);
                float liquidMaskBottomWorld = GetWorldBottomY(liquidMaskRect);

                float actualGap = liquidMaskBottomWorld - outlineBottomWorld;
                float expectedGapWorld = outlineHeightWorld * ExpectedGapFraction;

                Assert.AreEqual(expectedGapWorld, actualGap, HeightTolerance,
                    $"cap-{capacity}: LiquidMask bottom must be {expectedGapWorld:F2} world units " +
                    $"above Outline bottom (one border thickness), but gap was {actualGap:F2}. " +
                    $"Bottom segment must NOT be rendered behind the border.");
            }
        }
    }
}
