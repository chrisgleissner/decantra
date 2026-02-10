/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.PlayMode.Tests
{
    /// <summary>
    /// Tests for slot-to-pixel invariance in the bottle liquid rendering.
    /// These tests validate the core guarantee: the same number of slots must produce
    /// the same on-screen pixel height regardless of which bottle they are in.
    ///
    /// The pixel height of K slots in a bottle is:
    ///   LocalHeightForUnits(H, cap, refCap, K) * scaleY(cap)
    ///
    /// For invariance, this must be the same for all capacities and equal to:
    ///   (H / refCap) * scaleY(refCap) * K
    /// </summary>
    public class BottleVisualMappingTests
    {
        private const float SlotRootHeight = 300f;
        private const float PixelTolerance = 0.001f;

        /// <summary>
        /// Test 1 — Cross-bottle invariance:
        /// For each capacity in {2,3,4,5,6,7,8} with refCap=8, verify that K slots
        /// produce the same on-screen pixel height.
        /// </summary>
        [Test]
        public void CrossBottleInvariance_SameSlotCount_SamePixelHeight()
        {
            int refCap = 8;
            int[] capacities = { 2, 3, 4, 5, 6, 7, 8 };

            for (int k = 1; k <= 4; k++)
            {
                float referencePixelHeight = -1f;

                foreach (int cap in capacities)
                {
                    if (k > cap) continue;

                    float localHeight = BottleVisualMapping.LocalHeightForUnits(
                        SlotRootHeight, cap, refCap, k);
                    float scaleY = BottleVisualMapping.GetScaleY(cap);
                    float pixelHeight = localHeight * scaleY;

                    if (referencePixelHeight < 0f)
                    {
                        referencePixelHeight = pixelHeight;
                    }
                    else
                    {
                        Assert.AreEqual(referencePixelHeight, pixelHeight, PixelTolerance,
                            $"K={k} slots: cap={cap} produced {pixelHeight}px but reference is {referencePixelHeight}px");
                    }
                }
            }
        }

        /// <summary>
        /// Test 2 — Pour invariance regression:
        /// Simulate pouring 2 slots from a cap-3 bottle into a cap-6 bottle.
        /// The 2-slot stack must have the same pixel height in both bottles.
        /// This is the exact scenario that produced visible "growth" in 0.9.4.
        /// </summary>
        [Test]
        public void PourInvariance_Cap3ToCap6_SamePixelHeight()
        {
            int refCap = 6;
            int k = 2;

            float localHeightCap3 = BottleVisualMapping.LocalHeightForUnits(
                SlotRootHeight, 3, refCap, k);
            float pixelsCap3 = localHeightCap3 * BottleVisualMapping.GetScaleY(3);

            float localHeightCap6 = BottleVisualMapping.LocalHeightForUnits(
                SlotRootHeight, 6, refCap, k);
            float pixelsCap6 = localHeightCap6 * BottleVisualMapping.GetScaleY(6);

            Assert.AreEqual(pixelsCap3, pixelsCap6, PixelTolerance,
                $"2 slots: cap-3 produced {pixelsCap3}px vs cap-6 produced {pixelsCap6}px");
        }

        /// <summary>
        /// Test 2b — Pour invariance for all mixed-capacity pairs in {3,4,5,6,7,8}.
        /// </summary>
        [Test]
        public void PourInvariance_AllCapacityPairs_SamePixelHeight()
        {
            int[] caps = { 3, 4, 5, 6, 7, 8 };

            // Use the max capacity as the reference
            int refCap = 8;

            for (int i = 0; i < caps.Length; i++)
            {
                for (int j = i + 1; j < caps.Length; j++)
                {
                    int capA = caps[i];
                    int capB = caps[j];
                    int minCap = capA < capB ? capA : capB;

                    for (int k = 1; k <= minCap; k++)
                    {
                        float localA = BottleVisualMapping.LocalHeightForUnits(
                            SlotRootHeight, capA, refCap, k);
                        float pxA = localA * BottleVisualMapping.GetScaleY(capA);

                        float localB = BottleVisualMapping.LocalHeightForUnits(
                            SlotRootHeight, capB, refCap, k);
                        float pxB = localB * BottleVisualMapping.GetScaleY(capB);

                        Assert.AreEqual(pxA, pxB, PixelTolerance,
                            $"K={k}: cap-{capA} gave {pxA}px vs cap-{capB} gave {pxB}px");
                    }
                }
            }
        }

        /// <summary>
        /// Test 3 — Baseline layout lock:
        /// The scaleX and scaleY values for each capacity tier must match 0.9.4 baselines.
        /// </summary>
        [Test]
        public void BaselineLayoutLock_ScaleMatchesTag094()
        {
            // 0.9.4 baseline scale values (from ApplyCapacityScale)
            // capacity <= 3: scaleX=0.95, scaleY=0.88
            // capacity  = 4: scaleX=1.00, scaleY=1.00
            // capacity >= 5: scaleX=1.02, scaleY=1.06

            Assert.AreEqual(0.95f, BottleVisualMapping.GetScaleX(2), 0.001f, "cap-2 scaleX");
            Assert.AreEqual(0.88f, BottleVisualMapping.GetScaleY(2), 0.001f, "cap-2 scaleY");

            Assert.AreEqual(0.95f, BottleVisualMapping.GetScaleX(3), 0.001f, "cap-3 scaleX");
            Assert.AreEqual(0.88f, BottleVisualMapping.GetScaleY(3), 0.001f, "cap-3 scaleY");

            Assert.AreEqual(1.00f, BottleVisualMapping.GetScaleX(4), 0.001f, "cap-4 scaleX");
            Assert.AreEqual(1.00f, BottleVisualMapping.GetScaleY(4), 0.001f, "cap-4 scaleY");

            Assert.AreEqual(1.02f, BottleVisualMapping.GetScaleX(5), 0.001f, "cap-5 scaleX");
            Assert.AreEqual(1.06f, BottleVisualMapping.GetScaleY(5), 0.001f, "cap-5 scaleY");

            Assert.AreEqual(1.02f, BottleVisualMapping.GetScaleX(8), 0.001f, "cap-8 scaleX");
            Assert.AreEqual(1.06f, BottleVisualMapping.GetScaleY(8), 0.001f, "cap-8 scaleY");
        }

        /// <summary>
        /// Verify that the max-capacity bottle fills 100% of its slotRoot.
        /// </summary>
        [Test]
        public void MaxCapacityBottle_FillsEntireSlotRoot()
        {
            int refCap = 8;
            float localHeight = BottleVisualMapping.LocalHeightForUnits(
                SlotRootHeight, refCap, refCap, refCap);

            Assert.AreEqual(SlotRootHeight, localHeight, PixelTolerance,
                $"A full cap-{refCap} bottle should fill {SlotRootHeight} but got {localHeight}");
        }

        /// <summary>
        /// When all bottles have the same capacity (tutorial levels), the canonical
        /// mapping should produce the same result as the 0.9.4 formula: H/cap * units.
        /// </summary>
        [Test]
        public void UniformCapacity_MatchesOriginalFormula()
        {
            int cap = 4;
            int refCap = 4;

            for (int k = 1; k <= cap; k++)
            {
                float canonical = BottleVisualMapping.LocalHeightForUnits(
                    SlotRootHeight, cap, refCap, k);
                float original = SlotRootHeight * k / cap;

                Assert.AreEqual(original, canonical, PixelTolerance,
                    $"Cap={cap} K={k}: canonical={canonical} vs original={original}");
            }
        }

        /// <summary>
        /// Verify zero slots returns zero height.
        /// </summary>
        [Test]
        public void ZeroSlots_ZeroHeight()
        {
            float h = BottleVisualMapping.LocalHeightForUnits(SlotRootHeight, 4, 8, 0);
            Assert.AreEqual(0f, h, PixelTolerance);
        }

        /// <summary>
        /// Anti-regression: verify that the old (broken) formula H/cap * K produces
        /// DIFFERENT pixel heights across capacities — confirming the bug existed.
        /// </summary>
        [Test]
        public void OldFormula_ProducesDifferentPixelHeights_ConfirmsBugExists()
        {
            int k = 2;

            // Old formula: pixelHeight = (H / cap) * scaleY * K
            float oldPxCap3 = (SlotRootHeight / 3f) * 0.88f * k;
            float oldPxCap6 = (SlotRootHeight / 6f) * 1.06f * k;

            // These should NOT be equal — that's the bug
            float diff = Mathf.Abs(oldPxCap3 - oldPxCap6);
            Assert.Greater(diff, 1f,
                "Old formula should produce DIFFERENT pixel heights (confirming the 0.9.4 bug)");
        }
    }
}
