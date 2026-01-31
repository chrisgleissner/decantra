/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    public static class LevelDifficultyEngine
    {
        public static DifficultyProfile GetProfile(int levelIndex)
        {
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            var band = ResolveBand(levelIndex);
            int colorCount = ResolveColorCount(levelIndex);
            int emptyCount = ResolveEmptyCount(levelIndex);
            int bottleCount = colorCount + emptyCount;
            int reverseMoves = ComputeReverseMoves(levelIndex, band, colorCount, emptyCount, bottleCount);
            var themeId = ResolveThemeId(band, levelIndex);
            int rating = ComputeDifficultyRating(levelIndex, band, colorCount, emptyCount, bottleCount, reverseMoves);

            return new DifficultyProfile(levelIndex, band, bottleCount, colorCount, emptyCount, reverseMoves, themeId, rating);
        }

        private static LevelBand ResolveBand(int levelIndex)
        {
            if (levelIndex <= 10) return LevelBand.A;
            if (levelIndex <= 25) return LevelBand.B;
            if (levelIndex <= 50) return LevelBand.C;
            if (levelIndex <= 75) return LevelBand.D;
            return LevelBand.E;
        }

        private static int ResolveColorCount(int levelIndex)
        {
            int count = 3 + (levelIndex - 1) / 3;
            if (count < 3) count = 3;
            if (count > 8) count = 8;
            return count;
        }

        private static int ResolveEmptyCount(int levelIndex)
        {
            if (levelIndex <= 6) return 2;
            if (levelIndex >= 18) return 2;
            return 1;
        }

        private static BackgroundThemeId ResolveThemeId(LevelBand band, int levelIndex)
        {
            bool even = levelIndex % 2 == 0;
            switch (band)
            {
                case LevelBand.A: return even ? BackgroundThemeId.PastelRainbow : BackgroundThemeId.SoftGradient;
                case LevelBand.B: return even ? BackgroundThemeId.Balloons : BackgroundThemeId.LightCarnival;
                case LevelBand.C: return even ? BackgroundThemeId.CarnivalContrast : BackgroundThemeId.CarnivalPattern;
                case LevelBand.D: return even ? BackgroundThemeId.PlayfulMotifs : BackgroundThemeId.RainbowArcs;
                case LevelBand.E: return even ? BackgroundThemeId.RefinedRainbow : BackgroundThemeId.RefinedCarnival;
                default: return BackgroundThemeId.SoftGradient;
            }
        }

        private static int ComputeReverseMoves(int levelIndex, LevelBand band, int colorCount, int emptyCount, int bottleCount)
        {
            int baseMoves = 8 + colorCount * 2 - emptyCount;
            int levelBoost = levelIndex / 3;
            int bandBoost = ((int)band) * 2;
            int result = baseMoves + levelBoost + bandBoost;

            if (result < 8) result = 8;
            if (result > 60) result = 60;
            return result;
        }

        private static float GetBandProgress(int levelIndex, LevelBand band)
        {
            int start;
            int end;
            switch (band)
            {
                case LevelBand.A:
                    start = 1;
                    end = 10;
                    break;
                case LevelBand.B:
                    start = 11;
                    end = 25;
                    break;
                case LevelBand.C:
                    start = 26;
                    end = 50;
                    break;
                case LevelBand.D:
                    start = 51;
                    end = 75;
                    break;
                default:
                    start = 76;
                    end = 100;
                    break;
            }

            if (levelIndex <= start) return 0f;
            if (levelIndex >= end) return 1f;
            return (levelIndex - start) / (float)(end - start);
        }

        private static int ComputeDifficultyRating(int levelIndex, LevelBand band, int colorCount, int emptyCount, int bottleCount, int reverseMoves)
        {
            int bandScore = ((int)band) * 900;
            int levelScore = levelIndex * 25;
            int colorScore = colorCount * 70;
            int bottleScore = bottleCount * 30;
            int emptyPenalty = (3 - emptyCount) * 40;
            return Math.Max(0, bandScore + levelScore + colorScore + bottleScore + emptyPenalty);
        }
    }
}
