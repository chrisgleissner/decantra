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
    /// Strategy: bottles use proportional scaleY = (cap / refCap) * refScaleY.
    /// The local-space height is H * units / cap.
    /// The on-screen pixel height of K slots is:
    ///   (H * K / cap) * proportionalScaleY(cap, refCap)
    ///   = (H * K / cap) * (cap / refCap * refScaleY)
    ///   = H * K * refScaleY / refCap = CONSTANT
    /// </summary>
    public class BottleVisualMappingTests
    {
        private const float SlotRootHeight = 300f;
        private const float PixelTolerance = 0.001f;

        /// <summary>
        /// Cross-bottle invariance: K slots in any capacity bottle produce the same
        /// on-screen pixel height when using proportional scaling.
        /// </summary>
        [Test]
        public void CrossBottleInvariance_SameSlotCount_SamePixelHeight()
        {
            int refCap = 8;
            int[] capacities = { 2, 3, 4, 5, 6, 7, 8 };

            for (int k = 1; k <= 2; k++)
            {
                float referencePixelHeight = -1f;

                foreach (int cap in capacities)
                {
                    if (k > cap) continue;

                    float localHeight = BottleVisualMapping.LocalHeightForUnits(
                        SlotRootHeight, cap, refCap, k);
                    float scaleY = BottleVisualMapping.ProportionalScaleY(cap, refCap);
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
        /// Pour invariance regression: pouring 2 slots from cap-3 into cap-6 must
        /// produce the same pixel height in both bottles.
        /// </summary>
        [Test]
        public void PourInvariance_Cap3ToCap6_SamePixelHeight()
        {
            int refCap = 6;
            int k = 2;

            float localHeightCap3 = BottleVisualMapping.LocalHeightForUnits(
                SlotRootHeight, 3, refCap, k);
            float pixelsCap3 = localHeightCap3 * BottleVisualMapping.ProportionalScaleY(3, refCap);

            float localHeightCap6 = BottleVisualMapping.LocalHeightForUnits(
                SlotRootHeight, 6, refCap, k);
            float pixelsCap6 = localHeightCap6 * BottleVisualMapping.ProportionalScaleY(6, refCap);

            Assert.AreEqual(pixelsCap3, pixelsCap6, PixelTolerance,
                $"2 slots: cap-3 produced {pixelsCap3}px vs cap-6 produced {pixelsCap6}px");
        }

        /// <summary>
        /// Pour invariance for all mixed-capacity pairs in {3,4,5,6,7,8}.
        /// </summary>
        [Test]
        public void PourInvariance_AllCapacityPairs_SamePixelHeight()
        {
            int[] caps = { 3, 4, 5, 6, 7, 8 };
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
                        float pxA = localA * BottleVisualMapping.ProportionalScaleY(capA, refCap);

                        float localB = BottleVisualMapping.LocalHeightForUnits(
                            SlotRootHeight, capB, refCap, k);
                        float pxB = localB * BottleVisualMapping.ProportionalScaleY(capB, refCap);

                        Assert.AreEqual(pxA, pxB, PixelTolerance,
                            $"K={k}: cap-{capA} gave {pxA}px vs cap-{capB} gave {pxB}px");
                    }
                }
            }
        }

        /// <summary>
        /// Baseline layout lock: the 0.9.4 tier values used as reference for the
        /// max-capacity bottle must remain unchanged.
        /// </summary>
        [Test]
        public void BaselineLayoutLock_ReferenceTiersMatchTag094()
        {
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
        /// Every bottle at its full capacity must fill 100% of its slotRoot,
        /// not just the max-capacity bottle.
        /// </summary>
        [Test]
        public void AnyBottle_FullCapacity_FillsEntireSlotRoot()
        {
            int refCap = 8;

            for (int cap = 2; cap <= 8; cap++)
            {
                float localHeight = BottleVisualMapping.LocalHeightForUnits(
                    SlotRootHeight, cap, refCap, cap);

                Assert.AreEqual(SlotRootHeight, localHeight, PixelTolerance,
                    $"A full cap-{cap} bottle should fill {SlotRootHeight} but got {localHeight}");
            }
        }

        /// <summary>
        /// Proportional scaleY: cap/refCap * GetScaleY(refCap).
        /// The max-capacity bottle must use the 0.9.4 reference tier value.
        /// </summary>
        [Test]
        public void ProportionalScaleY_ValuesCorrect()
        {
            int refCap = 8;
            float refScaleY = BottleVisualMapping.GetScaleY(8); // 1.06

            for (int cap = 2; cap <= 8; cap++)
            {
                float expected = (float)cap / refCap * refScaleY;
                float actual = BottleVisualMapping.ProportionalScaleY(cap, refCap);
                Assert.AreEqual(expected, actual, PixelTolerance,
                    $"ProportionalScaleY({cap}, {refCap}) expected {expected} got {actual}");
            }

            // Max-cap bottle uses the 0.9.4 tier value
            Assert.AreEqual(refScaleY, BottleVisualMapping.ProportionalScaleY(refCap, refCap), PixelTolerance);
        }

        /// <summary>
        /// When all bottles have the same capacity (tutorial levels), the mapping
        /// produces the same result as the 0.9.4 formula: H/cap * units.
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
        /// Zero slots returns zero height.
        /// </summary>
        [Test]
        public void ZeroSlots_ZeroHeight()
        {
            float h = BottleVisualMapping.LocalHeightForUnits(SlotRootHeight, 4, 8, 0);
            Assert.AreEqual(0f, h, PixelTolerance);
        }

        /// <summary>
        /// Anti-regression: the old 0.9.4 approach (H/cap * non-proportional scaleY tiers)
        /// produces DIFFERENT pixel heights, confirming the original bug existed.
        /// </summary>
        [Test]
        public void OldFormula_ProducesDifferentPixelHeights_ConfirmsBugExists()
        {
            int k = 2;

            // Old formula: pixelHeight = (H / cap) * oldTierScaleY * K
            float oldPxCap3 = (SlotRootHeight / 3f) * BottleVisualMapping.GetScaleY(3) * k;
            float oldPxCap6 = (SlotRootHeight / 6f) * BottleVisualMapping.GetScaleY(6) * k;

            // These should NOT be equal — that's the bug
            float diff = Mathf.Abs(oldPxCap3 - oldPxCap6);
            Assert.Greater(diff, 1f,
                "Old formula should produce DIFFERENT pixel heights (confirming the 0.9.4 bug)");
        }
    }
}
