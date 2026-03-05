/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Model;

namespace Decantra.Presentation.Visual.Simulation
{
    /// <summary>
    /// Maps a logical <see cref="Bottle"/> state into a list of <see cref="LiquidLayerData"/>
    /// instances suitable for driving the Liquid3D shader.
    ///
    /// Rules
    /// -----
    /// 1. Run-length encode consecutive slots of the same color into a single layer.
    /// 2. Empty slots are gaps — they are skipped and do not produce a layer entry.
    /// 3. FillMin and FillMax are exact rational fractions: slotIndex / capacity.
    ///    No floating-point drift is introduced beyond single-precision representation.
    /// 4. Layers are ordered bottom-first (layer[0] is the lowest in the bottle).
    /// 5. The output list is reused across calls via Clear() to avoid allocations per frame.
    ///
    /// Thread safety: NOT thread-safe. Call only from the Unity main thread.
    /// </summary>
    public static class FillHeightMapper
    {
        /// <summary>
        /// Maximum number of distinct layers a bottle can have.
        /// With capacity ≤ 9 and no two adjacent slots sharing a color, max = 9.
        /// </summary>
        public const int MaxLayers = 9;

        /// <summary>
        /// Builds the layer list for <paramref name="bottle"/> using the supplied color resolver,
        /// and appends results into <paramref name="output"/> (which is cleared first).
        ///
        /// <paramref name="resolveColor"/> receives a ColorId integer value and must return
        /// (r, g, b) normalised floats in [0..1]. It may be null when only fill metrics
        /// (not colors) are needed — in that case colors are zeroed.
        /// </summary>
        public static void Build(
            Bottle bottle,
            List<LiquidLayerData> output,
            Func<int, (float r, float g, float b)> resolveColor = null)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Clear();

            int cap = bottle.Capacity;
            var slots = bottle.Slots;

            int runStart = -1;
            int runCount = 0;
            ColorId? runColor = null;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                if (!slot.HasValue)
                {
                    // Empty slot: flush any open run
                    if (runCount > 0)
                    {
                        AppendLayer(output, runStart, runCount, cap, runColor!.Value, resolveColor);
                        runCount = 0;
                        runColor = null;
                        runStart = -1;
                    }
                    continue;
                }

                if (runCount == 0)
                {
                    // Start new run
                    runStart = i;
                    runColor = slot;
                    runCount = 1;
                }
                else if (slot == runColor)
                {
                    // Extend current run
                    runCount++;
                }
                else
                {
                    // Color changed: flush previous run, start new
                    AppendLayer(output, runStart, runCount, cap, runColor!.Value, resolveColor);
                    runStart = i;
                    runColor = slot;
                    runCount = 1;
                }
            }

            // Flush trailing run
            if (runCount > 0)
            {
                AppendLayer(output, runStart, runCount, cap, runColor!.Value, resolveColor);
            }
        }

        /// <summary>
        /// Returns the total fill fraction: (number of non-empty slots) / capacity.
        /// Range [0..1]. Zero if bottle is empty, 1.0 if full.
        /// </summary>
        public static float TotalFill(Bottle bottle)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            return bottle.Capacity > 0 ? (float)bottle.Count / bottle.Capacity : 0f;
        }

        /// <summary>
        /// Returns the fill fraction of the top-most liquid surface.
        /// This is the FillMax of the topmost layer, i.e. the Y position
        /// where the liquid surface sits as a fraction of interior height.
        /// Returns 0 if bottle is empty.
        /// </summary>
        public static float TopSurfaceFill(Bottle bottle)
        {
            if (bottle == null) throw new ArgumentNullException(nameof(bottle));
            if (bottle.IsEmpty) return 0f;
            return TotalFill(bottle);
        }

        // -------------------------------------------------------------------------

        private static void AppendLayer(
            List<LiquidLayerData> output,
            int slotStart, int slotCount, int bottleCapacity,
            ColorId colorId,
            Func<int, (float r, float g, float b)> resolveColor)
        {
            float r = 0f, g = 0f, b = 0f;
            if (resolveColor != null)
            {
                (r, g, b) = resolveColor((int)colorId);
            }

            output.Add(new LiquidLayerData(slotStart, slotCount, bottleCapacity, r, g, b, (int)colorId));
        }
    }
}
