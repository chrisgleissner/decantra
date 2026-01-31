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
