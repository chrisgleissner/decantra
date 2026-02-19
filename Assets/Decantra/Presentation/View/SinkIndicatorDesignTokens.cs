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
        public const float DarkBackgroundLuminanceThreshold = 0.30f;
        public const float LightBackgroundLuminanceThreshold = 0.55f;

        // Color strategy: dual-tone stripe (lighter edge + darker core) for contrast across themes.
        public static readonly Color EdgeColor = new Color(0.78f, 0.84f, 0.94f, 0.92f);
        public static readonly Color CoreColor = new Color(0.13f, 0.16f, 0.23f, 0.96f);
        public static readonly Color EdgeColorLightVariant = new Color(0.93f, 0.96f, 1f, 0.96f);
        public static readonly Color CoreColorLightVariant = new Color(0.65f, 0.74f, 0.88f, 0.98f);
        public static readonly Color EdgeColorDarkVariant = new Color(0.26f, 0.30f, 0.40f, 0.95f);
        public static readonly Color CoreColorDarkVariant = new Color(0.08f, 0.10f, 0.15f, 0.98f);

        public static float ResolveIndicatorHeight(float bottleHeight, float baselineHeight)
        {
            return CanonicalHeight;
        }

        public static float ResolveIndicatorWidth(float bottleWidth)
        {
            return CanonicalWidth;
        }

        public static void ResolveContrastColors(float backgroundLuminance, out Color edgeColor, out Color coreColor)
        {
            if (backgroundLuminance <= DarkBackgroundLuminanceThreshold)
            {
                edgeColor = EdgeColorLightVariant;
                coreColor = CoreColorLightVariant;
                return;
            }

            if (backgroundLuminance >= LightBackgroundLuminanceThreshold)
            {
                edgeColor = EdgeColorDarkVariant;
                coreColor = CoreColorDarkVariant;
                return;
            }

            edgeColor = EdgeColor;
            coreColor = CoreColor;
        }
    }
}
