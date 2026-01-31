/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    public static class BackgroundRules
    {
        public static int ComputePaletteIndex(int levelIndex, int seed, BackgroundThemeId themeId, int paletteCount)
        {
            if (paletteCount <= 0) throw new ArgumentOutOfRangeException(nameof(paletteCount));
            unchecked
            {
                int mix = levelIndex * 73856093;
                mix ^= seed * 19349663;
                mix ^= ((int)themeId + 1) * 83492791;
                int value = Math.Abs(mix);
                return value % paletteCount;
            }
        }
    }
}
