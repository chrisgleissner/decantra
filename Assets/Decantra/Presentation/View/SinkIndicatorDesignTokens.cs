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
        // Canonical sink-indicator dimensions (based on approved medium / second-sink sizing).
        public const float CanonicalWidth = 112f;
        public const float CanonicalHeight = 5.02f;
        public const float ThicknessScale = 2.4f;
        public const float MinHeightRatio = 0.018f;
        public const float MaxHeightRatio = 0.065f;
        public const float InnerStripeHeightRatio = 0.6f;

        // Color strategy: dual-tone stripe (lighter edge + darker core) for contrast across themes.
        public static readonly Color EdgeColor = new Color(0.78f, 0.84f, 0.94f, 0.92f);
        public static readonly Color CoreColor = new Color(0.13f, 0.16f, 0.23f, 0.96f);

        public static float ResolveIndicatorHeight(float bottleHeight, float baselineHeight)
        {
            return CanonicalHeight;
        }

        public static float ResolveIndicatorWidth(float bottleWidth)
        {
            return CanonicalWidth;
        }
    }
}
