/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.View
{
    public static class SinkIndicatorDesignTokens
    {
        // Thickness strategy: increase baseline marker thickness by a controlled multiplier.
        public const float ThicknessScale = 2.4f;
        public const float MinHeightRatio = 0.018f;
        public const float MaxHeightRatio = 0.065f;
        public const float InnerStripeHeightRatio = 0.6f;

        // Color strategy: dual-tone stripe (lighter edge + darker core) for contrast across themes.
        public static readonly Color EdgeColor = new Color(0.78f, 0.84f, 0.94f, 0.92f);
        public static readonly Color CoreColor = new Color(0.13f, 0.16f, 0.23f, 0.96f);

        public static float ResolveIndicatorHeight(float bottleHeight, float baselineHeight)
        {
            float safeBottleHeight = Mathf.Max(1f, bottleHeight);
            float clampedBaseline = Mathf.Max(0.5f, baselineHeight);
            float target = clampedBaseline * ThicknessScale;
            float minHeight = safeBottleHeight * MinHeightRatio;
            float maxHeight = safeBottleHeight * MaxHeightRatio;
            return Mathf.Clamp(target, minHeight, maxHeight);
        }
    }
}
