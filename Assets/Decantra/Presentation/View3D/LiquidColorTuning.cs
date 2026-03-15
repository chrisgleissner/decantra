/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.View3D
{
    public static class LiquidColorTuning
    {
        public const float SaturationTarget = 1f;
        public const float MinimumValue = 0.97f;
        public const float ValueBoost = 1.45f;

        public static Color ApplyGameplayVibrancy(Color source)
        {
            Color.RGBToHSV(source, out float hue, out float saturation, out float value);
            var tuned = Color.HSVToRGB(
                hue,
                SaturationTarget,
                Mathf.Clamp01(Mathf.Max(value * ValueBoost, MinimumValue)));
            tuned.a = source.a;
            return tuned;
        }
    }
}