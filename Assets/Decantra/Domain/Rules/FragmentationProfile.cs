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
    /// Defines color fragmentation requirements for a given difficulty tier.
    /// Controls how scattered colors are across bottles, which directly impacts human difficulty.
    /// </summary>
    public sealed class FragmentationProfile
    {
        /// <summary>
        /// Minimum average number of fragments per color.
        /// A fragment is a contiguous run of a single color in a bottle.
        /// Higher values mean more scattered colors = harder puzzle.
        /// </summary>
        public float MinAvgFragmentsPerColor { get; }

        /// <summary>
        /// Minimum variance in fragment sizes.
        /// Higher variance creates asymmetric distributions requiring more planning.
        /// </summary>
        public float MinFragmentSizeVariance { get; }

        /// <summary>
        /// Minimum number of mixed bottles (bottles with more than one color).
        /// </summary>
        public int MinMixedBottles { get; }

        public FragmentationProfile(float minAvgFragments, float minFragmentVariance, int minMixedBottles)
        {
            MinAvgFragmentsPerColor = Math.Max(1.0f, minAvgFragments);
            MinFragmentSizeVariance = Math.Max(0.0f, minFragmentVariance);
            MinMixedBottles = Math.Max(0, minMixedBottles);
        }

        /// <summary>
        /// Gets the fragmentation profile for a given level index.
        /// All parameters clamp at level 100 (maximum difficulty).
        /// </summary>
        public static FragmentationProfile ForLevel(int levelIndex)
        {
            // Clamp at level 100 for maximum difficulty plateau
            int eff = Math.Min(levelIndex, 100);

            // Linear interpolation from level 1 to 100
            float t = (eff - 1) / 99.0f;

            // MinAvgFragmentsPerColor: 1.0 at level 1, 2.5 at level 100
            float avgFragments = 1.0f + t * 1.5f;

            // MinFragmentSizeVariance: 0.0 at level 1, 0.8 at level 100
            float variance = t * 0.8f;

            // MinMixedBottles: scales with color count (approximated)
            // Level 1-5: 1, Level 6-15: 2, Level 16-30: 3, Level 31+: 4
            int mixedBottles;
            if (eff <= 5) mixedBottles = 1;
            else if (eff <= 15) mixedBottles = 2;
            else if (eff <= 30) mixedBottles = 3;
            else mixedBottles = Math.Min(5, 4 + (eff - 31) / 20);

            return new FragmentationProfile(avgFragments, variance, mixedBottles);
        }
    }
}
