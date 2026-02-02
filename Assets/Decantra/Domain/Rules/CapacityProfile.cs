/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    /// <summary>
    /// Defines bottle capacity configuration for a given difficulty tier.
    /// Controls bottle size diversity which directly impacts human difficulty.
    /// </summary>
    public sealed class CapacityProfile
    {
        /// <summary>
        /// Pool of available bottle capacities for this tier.
        /// Higher tiers have wider pools with more extreme sizes.
        /// </summary>
        public int[] CapacityPool { get; }

        /// <summary>
        /// Minimum number of distinct capacities required in the level.
        /// Enforced during generation to ensure bottle diversity.
        /// </summary>
        public int MinDistinctCapacities { get; }

        /// <summary>
        /// Minimum number of "small" bottles (capacity ≤ 3) required.
        /// Small bottles create planning constraints.
        /// </summary>
        public int MinSmallBottles { get; }

        /// <summary>
        /// Minimum number of "large" bottles (capacity ≥ 6) required.
        /// Large bottles create asymmetric pour dynamics.
        /// </summary>
        public int MinLargeBottles { get; }

        public CapacityProfile(int[] capacityPool, int minDistinctCapacities, int minSmallBottles, int minLargeBottles)
        {
            if (capacityPool == null || capacityPool.Length == 0)
                throw new ArgumentNullException(nameof(capacityPool));
            if (minDistinctCapacities < 1)
                throw new ArgumentOutOfRangeException(nameof(minDistinctCapacities));

            CapacityPool = capacityPool;
            MinDistinctCapacities = minDistinctCapacities;
            MinSmallBottles = Math.Max(0, minSmallBottles);
            MinLargeBottles = Math.Max(0, minLargeBottles);
        }

        /// <summary>
        /// Gets the capacity profile for a given level index.
        /// All parameters clamp at level 100 (maximum difficulty).
        /// </summary>
        public static CapacityProfile ForLevel(int levelIndex)
        {
            // Clamp at level 100 for maximum difficulty plateau
            int eff = Math.Min(levelIndex, 100);

            if (eff <= 5)
            {
                // Tutorial tier: simple, uniform bottles
                return new CapacityProfile(
                    capacityPool: new[] { 4 },
                    minDistinctCapacities: 1,
                    minSmallBottles: 0,
                    minLargeBottles: 0);
            }
            else if (eff <= 15)
            {
                // Early tier: introduce size variation
                return new CapacityProfile(
                    capacityPool: new[] { 3, 4, 5 },
                    minDistinctCapacities: 2,
                    minSmallBottles: 0,
                    minLargeBottles: 0);
            }
            else if (eff <= 30)
            {
                // Mid-early tier: meaningful size differences
                return new CapacityProfile(
                    capacityPool: new[] { 3, 4, 5, 6 },
                    minDistinctCapacities: 2,
                    minSmallBottles: 1,
                    minLargeBottles: 0);
            }
            else if (eff <= 50)
            {
                // Mid tier: require diversity with some large bottles
                return new CapacityProfile(
                    capacityPool: new[] { 2, 3, 4, 5, 6, 7 },
                    minDistinctCapacities: 3,
                    minSmallBottles: 1,
                    minLargeBottles: 1);
            }
            else if (eff <= 70)
            {
                // Mid-late tier: strong diversity requirement
                return new CapacityProfile(
                    capacityPool: new[] { 2, 3, 4, 5, 6, 7, 8 },
                    minDistinctCapacities: 4,
                    minSmallBottles: 1,
                    minLargeBottles: 1);
            }
            else if (eff <= 85)
            {
                // Late tier: extreme size differences
                return new CapacityProfile(
                    capacityPool: new[] { 2, 3, 4, 5, 6, 7, 8 },
                    minDistinctCapacities: 4,
                    minSmallBottles: 2,
                    minLargeBottles: 2);
            }
            else
            {
                // Maximum tier (86-100+): full diversity
                return new CapacityProfile(
                    capacityPool: new[] { 2, 3, 4, 5, 6, 7, 8 },
                    minDistinctCapacities: 5,
                    minSmallBottles: 2,
                    minLargeBottles: 2);
            }
        }

        /// <summary>
        /// Returns true if capacity is considered "small" (hard constraint).
        /// </summary>
        public static bool IsSmall(int capacity) => capacity <= 3;

        /// <summary>
        /// Returns true if capacity is considered "large" (creates asymmetry).
        /// </summary>
        public static bool IsLarge(int capacity) => capacity >= 6;
    }
}
