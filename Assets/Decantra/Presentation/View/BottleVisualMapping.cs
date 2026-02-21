/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.View
{
    /// <summary>
    /// Provides a canonical slot-to-pixel mapping so that the same number of slots
    /// always renders at the same pixel height regardless of bottle capacity.
    ///
    /// Strategy: bottles do NOT use transform scaling. Instead, only the liquid-holding
    /// body section (slotRoot, outline, glassBack, etc.) is resized proportionally to
    /// capacity. The top and bottom decorations (rim, neck, flange, basePlate) stay at
    /// fixed sizes across all bottles.
    ///
    /// The slotRoot height for each bottle is: RefSlotRootHeight * (cap / refCap).
    /// LocalHeightForUnits returns: slotRootHeight * units / capacity.
    /// Thus the on-screen pixel height of K slots is:
    ///   (RefSlotRootHeight * cap/refCap) * K / cap = RefSlotRootHeight * K / refCap
    ///   = CONSTANT across all bottles  ✓
    ///
    /// Every bottle fills 100% of its slotRoot at capacity.
    /// </summary>
    public static class BottleVisualMapping
    {
        /// <summary>
        /// Returns the 0.9.4 baseline scaleY tier for a given capacity.
        /// Used as the reference value for the max-capacity bottle in proportional scaling.
        /// </summary>
        public static float GetScaleY(int capacity)
        {
            if (capacity <= 3) return 0.88f;
            if (capacity >= 5) return 1.06f;
            return 1f; // capacity 4
        }

        /// <summary>
        /// Returns the 0.9.4 baseline scaleX tier for a given capacity.
        /// Used as the reference value for the max-capacity bottle in proportional scaling.
        /// </summary>
        public static float GetScaleX(int capacity)
        {
            if (capacity <= 3) return 0.95f;
            if (capacity >= 5) return 1.02f;
            return 1f; // capacity 4
        }

        /// <summary>
        /// Returns the proportional scaleY for a bottle, so that a full bottle fills
        /// its entire slotRoot and the pixel-per-slot is invariant across capacities.
        /// The reference (max-capacity) bottle uses the 0.9.4 baseline tier value.
        /// </summary>
        public static float ProportionalScaleY(int capacity, int refCapacity)
        {
            if (refCapacity <= 0) return 1f;
            float refScaleY = GetScaleY(refCapacity);
            return (float)capacity / refCapacity * refScaleY;
        }

        /// <summary>
        /// Returns the proportional scaleX for a bottle, maintaining the same aspect
        /// ratio relationship as scaleY. The reference bottle uses its 0.9.4 baseline.
        /// </summary>
        public static float ProportionalScaleX(int capacity, int refCapacity)
        {
            if (refCapacity <= 0) return 1f;
            float refScaleX = GetScaleX(refCapacity);
            return (float)capacity / refCapacity * refScaleX;
        }

        /// <summary>
        /// Computes the local-space height for a given number of slots.
        /// With proportional bottle scaling, this is simply H * units / capacity.
        /// The pixel invariant is maintained by the proportional transform scale.
        ///
        /// Full bottle (units == capacity) always returns slotRootHeight exactly.
        /// Empty bottle (units &lt;= 0) always returns 0 exactly.
        /// Result is hard-clamped to [0, slotRootHeight] to prevent overflow into neck
        /// and to avoid float accumulation errors.
        /// </summary>
        /// <param name="slotRootHeight">slotRoot.rect.height (local-space, pre-scale)</param>
        /// <param name="capacity">This bottle's capacity</param>
        /// <param name="refCapacity">Max capacity across all bottles in the current level (unused, kept for API stability)</param>
        /// <param name="units">Number of slots to convert to height</param>
        /// <returns>Local-space height for the given number of slots, clamped to [0, slotRootHeight]</returns>
        public static float LocalHeightForUnits(float slotRootHeight, int capacity, int refCapacity, float units)
        {
            if (capacity <= 0 || slotRootHeight <= 0f) return 0f;
            if (units <= 0f) return 0f;
            if (units >= capacity) return slotRootHeight;
            return Mathf.Clamp(slotRootHeight * units / capacity, 0f, slotRootHeight);
        }
    }
}
