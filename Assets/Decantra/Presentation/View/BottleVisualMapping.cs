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
    /// Root cause of the invariance bug: 0.9.4 computed liquid unit height as
    /// (slotRoot.rect.height / capacity). Because bottles of different capacities
    /// have non-proportional scaleY values, the on-screen pixel height per slot
    /// varied by up to 3.3× across capacities.
    ///
    /// Fix: compute a canonical local unit height that compensates for the bottle's
    /// scaleY relative to the reference (max-capacity) bottle's scaleY, so that the
    /// on-screen pixel height per slot is identical for all bottles.
    ///
    /// Rounding policy: Mathf.Round() on pixel values (banker's rounding). Max ±1px.
    /// </summary>
    public static class BottleVisualMapping
    {
        /// <summary>
        /// Returns the scaleY that BottleView.ApplyCapacityScale applies for a given capacity.
        /// Must stay in sync with BottleView.ApplyCapacityScale.
        /// </summary>
        public static float GetScaleY(int capacity)
        {
            if (capacity <= 3) return 0.88f;
            if (capacity >= 5) return 1.06f;
            return 1f; // capacity 4
        }

        /// <summary>
        /// Returns the scaleX that BottleView.ApplyCapacityScale applies for a given capacity.
        /// </summary>
        public static float GetScaleX(int capacity)
        {
            if (capacity <= 3) return 0.95f;
            if (capacity >= 5) return 1.02f;
            return 1f; // capacity 4
        }

        /// <summary>
        /// Computes the local-space height for a given number of slots, such that the
        /// on-screen pixel height is invariant across bottles of any capacity.
        ///
        /// The height of 1 slot in local-space is:
        ///   localUnitHeight = (slotRootHeight / refCapacity) * (refScaleY / bottleScaleY)
        ///
        /// The on-screen pixel height of that slot is:
        ///   localUnitHeight * bottleScaleY * parentScales
        ///   = (slotRootHeight / refCapacity) * refScaleY * parentScales
        ///   = CONSTANT across all bottles  ✓
        /// </summary>
        /// <param name="slotRootHeight">slotRoot.rect.height (local-space, pre-scale)</param>
        /// <param name="capacity">This bottle's capacity</param>
        /// <param name="refCapacity">Max capacity across all bottles in the current level</param>
        /// <param name="units">Number of slots to convert to height</param>
        /// <returns>Local-space height for the given number of slots</returns>
        public static float LocalHeightForUnits(float slotRootHeight, int capacity, int refCapacity, float units)
        {
            if (refCapacity <= 0 || slotRootHeight <= 0f) return 0f;

            float bottleScaleY = GetScaleY(capacity);
            float refScaleY = GetScaleY(refCapacity);

            // Canonical local unit height, compensated for this bottle's scale
            float localUnitHeight = (slotRootHeight / refCapacity) * (refScaleY / bottleScaleY);

            return localUnitHeight * units;
        }

        /// <summary>
        /// Computes the local-space Y offset for a segment that starts after unitsBefore
        /// slots, using the canonical mapping.
        /// </summary>
        public static float LocalYForUnitsBefore(float slotRootHeight, int capacity, int refCapacity, float unitsBefore)
        {
            return LocalHeightForUnits(slotRootHeight, capacity, refCapacity, unitsBefore);
        }
    }
}
