/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using NUnit.Framework;
using Decantra.Domain.Model;
using Decantra.Presentation.Visual.Simulation;

namespace Decantra.Presentation.Visual.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="FillHeightMapper"/>.
    ///
    /// Validates:
    ///   1.  Empty bottle produces zero layers.
    ///   2.  Full monochrome bottle produces one layer covering full height.
    ///   3.  Per-layer FillMin/FillMax are exact rational fractions.
    ///   4.  No layer overlap (FillMax[i] ≤ FillMin[i+1]).
    ///   5.  Layers are ordered bottom-first.
    ///   6.  Non-contiguous colors each produce a separate layer.
    ///   7.  Empty slots between layers create a gap (lower layer ends before gap starts).
    ///   8.  TotalFill is exact.
    ///   9.  TopSurfaceFill reports the highest occupied slot, including bottles with gaps.
    ///   10. MaxLayers constant matches max possible layer count (9).
    /// </summary>
    [TestFixture]
    public sealed class FillHeightMapperTests
    {
        private static (float r, float g, float b) FixedColorResolver(int colorId)
            => (colorId * 0.1f, colorId * 0.05f, 0f);

        private readonly List<LiquidLayerData> _output = new List<LiquidLayerData>();

        // ── 1. Empty bottle ───────────────────────────────────────────────────
        [Test]
        public void EmptyBottle_ProducesZeroLayers()
        {
            var bottle = new Bottle(4); // capacity 4, all empty
            FillHeightMapper.Build(bottle, _output);
            Assert.AreEqual(0, _output.Count, "Empty bottle must produce 0 layers");
        }

        // ── 2. Full monochrome bottle: one layer ──────────────────────────────
        [Test]
        public void FullMonochrome_ProducesOneLayer_CoveringFullHeight()
        {
            var bottle = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red
            });
            FillHeightMapper.Build(bottle, _output, FixedColorResolver);

            Assert.AreEqual(1, _output.Count, "Full monochrome bottle must produce exactly 1 layer");
            var layer = _output[0];
            Assert.AreEqual(0f, layer.FillMin, 1e-6f, "FillMin should be 0");
            Assert.AreEqual(1f, layer.FillMax, 1e-6f, "FillMax should be 1");
            Assert.AreEqual(4, layer.SlotCount, "SlotCount should equal capacity");
            Assert.AreEqual((int)ColorId.Red, layer.ColorId, "ColorId should match Red");
        }

        // ── 3. Per-layer fill fractions are exact rationals ───────────────────
        [Test]
        public void TwoColorBottle_ExactFills()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Blue, null, null
            });
            FillHeightMapper.Build(b, _output);

            Assert.AreEqual(2, _output.Count, "Two adjacent unique colors must produce 2 layers");

            var l0 = _output[0]; // bottom layer
            var l1 = _output[1]; // second layer

            Assert.AreEqual(0f, l0.FillMin, 1e-6f, "Layer0 FillMin");
            Assert.AreEqual(0.25f, l0.FillMax, 1e-6f, "Layer0 FillMax (1/4)");
            Assert.AreEqual(0.25f, l1.FillMin, 1e-6f, "Layer1 FillMin (1/4)");
            Assert.AreEqual(0.5f, l1.FillMax, 1e-6f, "Layer1 FillMax (2/4)");
        }

        // ── 4. No layer overlap ───────────────────────────────────────────────
        [Test]
        public void Layers_NeverOverlap()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red,  ColorId.Red,
                ColorId.Blue,
                ColorId.Green, ColorId.Green
            });
            FillHeightMapper.Build(b, _output);

            for (int i = 0; i < _output.Count - 1; i++)
            {
                float maxI = _output[i].FillMax;
                float minNext = _output[i + 1].FillMin;
                Assert.LessOrEqual(maxI, minNext + 1e-6f,
                    $"Layer {i} FillMax={maxI:F6} must be ≤ Layer {i + 1} FillMin={minNext:F6}");
            }
        }

        // ── 5. Layers ordered bottom-first ────────────────────────────────────
        [Test]
        public void Layers_OrderedBottomFirst()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Blue, ColorId.Green
            });
            FillHeightMapper.Build(b, _output);

            for (int i = 0; i < _output.Count - 1; i++)
            {
                Assert.Less(_output[i].FillMin, _output[i + 1].FillMin,
                    $"Layer {i} must have smaller FillMin than layer {i + 1}");
            }
        }

        // ── 6. Non-contiguous colors produce separate layers ──────────────────
        [Test]
        public void AlternatingColors_ProduceSeparateLayers()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Blue, ColorId.Red
            });
            FillHeightMapper.Build(b, _output);
            Assert.AreEqual(3, _output.Count,
                "Three slots of alternating colors must produce 3 layers");
        }

        // ── 7. Empty slots create gap (no phantom layer covers gap) ──────────
        [Test]
        public void EmptySlots_DoNotCreateLayers()
        {
            // Bottom 2 slots filled, top 2 empty
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Red, null, null
            });
            FillHeightMapper.Build(b, _output);

            Assert.AreEqual(1, _output.Count, "Only filled slots produce layers");
            Assert.AreEqual(0.5f, _output[0].FillMax, 1e-6f,
                "Top of layer should be at 0.5 (2/4 filled)");
        }

        // ── 8. TotalFill is exact ─────────────────────────────────────────────
        [Test]
        public void TotalFill_ExactForPartialFill()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, null, null, null
            });
            float fill = FillHeightMapper.TotalFill(b);
            Assert.AreEqual(0.25f, fill, 1e-6f, "1/4 filled bottle: TotalFill = 0.25");
        }

        [Test]
        public void TotalFill_ZeroForEmptyBottle()
        {
            var b = new Bottle(4);
            Assert.AreEqual(0f, FillHeightMapper.TotalFill(b), 1e-6f);
        }

        [Test]
        public void TotalFill_OneForFullBottle()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Red, ColorId.Red, ColorId.Red, ColorId.Red
            });
            Assert.AreEqual(1f, FillHeightMapper.TotalFill(b), 1e-6f);
        }

        // ── 9. TopSurfaceFill equals TotalFill ────────────────────────────────
        [Test]
        public void TopSurfaceFill_EqualsTotalFill()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Blue, ColorId.Blue, null, null
            });
            float total = FillHeightMapper.TotalFill(b);
            float surface = FillHeightMapper.TopSurfaceFill(b);
            Assert.AreEqual(total, surface, 1e-6f,
                "TopSurfaceFill must equal TotalFill");
        }

        [Test]
        public void TopSurfaceFill_UsesHighestOccupiedSlotWhenBottleHasGap()
        {
            var b = new Bottle(new ColorId?[] {
                ColorId.Blue, null, ColorId.Red, null
            });

            float surface = FillHeightMapper.TopSurfaceFill(b);

            Assert.AreEqual(0.75f, surface, 1e-6f,
                "TopSurfaceFill must report the highest occupied slot rather than compact fill count");
        }

        // ── 10. MaxLayers matches bottle capacity ceiling ─────────────────────
        [Test]
        public void MaxLayers_IsSufficientForMaxCapacityBottle()
        {
            Assert.GreaterOrEqual(FillHeightMapper.MaxLayers, 9,
                "MaxLayers must cover max bottle capacity (9)");
        }

        // ── Regression: Output list is cleared on each Build call ─────────────
        [Test]
        public void Build_ClearsOutputListBeforeBuilding()
        {
            var b1 = new Bottle(new ColorId?[] { ColorId.Red });
            var b2 = new Bottle(new ColorId?[] { ColorId.Blue });

            FillHeightMapper.Build(b1, _output);
            Assert.AreEqual(1, _output.Count, "First build: 1 layer");

            FillHeightMapper.Build(b2, _output);
            Assert.AreEqual(1, _output.Count, "Second build must clear previous results");
            Assert.AreEqual((int)ColorId.Blue, _output[0].ColorId,
                "Second build must use new bottle state");
        }
    }
}
