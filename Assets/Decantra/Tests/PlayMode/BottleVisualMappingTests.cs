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
    /// Strategy: no transform scaling. Instead, slotRoot height is proportional to capacity:
    ///   slotRootHeight(cap) = RefSlotRootHeight * cap / refCap
    /// LocalHeightForUnits returns: slotRootHeight * units / cap
    /// On-screen pixel height of K slots:
    ///   (RefSlotRootHeight * cap/refCap) * K / cap = RefSlotRootHeight * K / refCap
    ///   = CONSTANT across all bottles  ✓
    /// </summary>
    public class BottleVisualMappingTests
    {
        private const float RefSlotRootHeight = 300f;
        private const float PixelTolerance = 0.001f;

        /// <summary>
        /// Returns the slotRoot height for a bottle of given capacity,
        /// matching how BottleView resizes slotRoot at runtime.
        /// </summary>
        private static float SlotRootHeightForCap(int cap, int refCap)
        {
            return RefSlotRootHeight * (float)cap / refCap;
        }

        /// <summary>
        /// Cross-bottle invariance: K slots in any capacity bottle produce the same
        /// on-screen pixel height (no transform scaling, heights are proportional).
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

                    float slotH = SlotRootHeightForCap(cap, refCap);
                    float pixelHeight = BottleVisualMapping.LocalHeightForUnits(slotH, cap, refCap, k);

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

            float h3 = SlotRootHeightForCap(3, refCap);
            float px3 = BottleVisualMapping.LocalHeightForUnits(h3, 3, refCap, k);

            float h6 = SlotRootHeightForCap(6, refCap);
            float px6 = BottleVisualMapping.LocalHeightForUnits(h6, 6, refCap, k);

            Assert.AreEqual(px3, px6, PixelTolerance,
                $"2 slots: cap-3 produced {px3}px vs cap-6 produced {px6}px");
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
                        float hA = SlotRootHeightForCap(capA, refCap);
                        float pxA = BottleVisualMapping.LocalHeightForUnits(hA, capA, refCap, k);

                        float hB = SlotRootHeightForCap(capB, refCap);
                        float pxB = BottleVisualMapping.LocalHeightForUnits(hB, capB, refCap, k);

                        Assert.AreEqual(pxA, pxB, PixelTolerance,
                            $"K={k}: cap-{capA} gave {pxA}px vs cap-{capB} gave {pxB}px");
                    }
                }
            }
        }

        /// <summary>
        /// Baseline layout lock: the 0.9.4 tier values must remain unchanged
        /// (used as reference constants in the mapping).
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
        /// Every bottle at its full capacity must fill 100% of its (resized) slotRoot.
        /// </summary>
        [Test]
        public void AnyBottle_FullCapacity_FillsEntireSlotRoot()
        {
            int refCap = 8;

            for (int cap = 2; cap <= 8; cap++)
            {
                float slotH = SlotRootHeightForCap(cap, refCap);
                float localHeight = BottleVisualMapping.LocalHeightForUnits(slotH, cap, refCap, cap);

                Assert.AreEqual(slotH, localHeight, PixelTolerance,
                    $"A full cap-{cap} bottle should fill its slotRoot ({slotH}) but got {localHeight}");
            }
        }

        /// <summary>
        /// SlotRoot height ratio: slotRoot is proportional to cap/refCap.
        /// The max-cap bottle gets the full reference height.
        /// </summary>
        [Test]
        public void SlotRootHeight_ProportionalToCapacity()
        {
            int refCap = 8;

            Assert.AreEqual(RefSlotRootHeight, SlotRootHeightForCap(refCap, refCap), PixelTolerance,
                "Max-cap bottle should have full slotRoot height");

            Assert.AreEqual(RefSlotRootHeight * 0.5f, SlotRootHeightForCap(4, refCap), PixelTolerance,
                "Cap-4 bottle with refCap=8 should have half the slotRoot height");

            Assert.AreEqual(RefSlotRootHeight * 0.25f, SlotRootHeightForCap(2, refCap), PixelTolerance,
                "Cap-2 bottle with refCap=8 should have quarter the slotRoot height");
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
            float slotH = SlotRootHeightForCap(cap, refCap); // == RefSlotRootHeight

            for (int k = 1; k <= cap; k++)
            {
                float canonical = BottleVisualMapping.LocalHeightForUnits(slotH, cap, refCap, k);
                float original = RefSlotRootHeight * k / cap;

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
            float h = BottleVisualMapping.LocalHeightForUnits(RefSlotRootHeight, 4, 8, 0);
            Assert.AreEqual(0f, h, PixelTolerance);
        }

        /// <summary>
        /// Anti-regression: the old 0.9.4 approach (same slotRoot height for all bottles,
        /// H/cap * non-proportional scaleY tiers) produces DIFFERENT pixel heights.
        /// </summary>
        [Test]
        public void OldFormula_ProducesDifferentPixelHeights_ConfirmsBugExists()
        {
            int k = 2;

            // Old formula: same slotRoot height for all, pixelHeight = (H / cap) * oldTierScaleY * K
            float oldPxCap3 = (RefSlotRootHeight / 3f) * BottleVisualMapping.GetScaleY(3) * k;
            float oldPxCap6 = (RefSlotRootHeight / 6f) * BottleVisualMapping.GetScaleY(6) * k;

            float diff = Mathf.Abs(oldPxCap3 - oldPxCap6);
            Assert.Greater(diff, 1f,
                "Old formula should produce DIFFERENT pixel heights (confirming the 0.9.4 bug)");
        }
    }
}
