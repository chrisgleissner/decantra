using System;

namespace Decantra.Domain.Rules
{
    public static class LevelDifficultyEngine
    {
        public static DifficultyProfile GetProfile(int levelIndex)
        {
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            var band = ResolveBand(levelIndex);
            int colorCount = ResolveColorCount(band);
            int emptyCount = ResolveEmptyCount(band, levelIndex);
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

        private static int ResolveColorCount(LevelBand band)
        {
            switch (band)
            {
                case LevelBand.A: return 3;
                case LevelBand.B: return 4;
                case LevelBand.C: return 5;
                case LevelBand.D: return 6;
                case LevelBand.E: return 8;
                default: return 3;
            }
        }

        private static int ResolveEmptyCount(LevelBand band, int levelIndex)
        {
            switch (band)
            {
                case LevelBand.A: return 3;
                case LevelBand.B: return 2;
                case LevelBand.C: return 2;
                case LevelBand.D: return (levelIndex % 2 == 0) ? 2 : 1;
                case LevelBand.E: return 1;
                default: return 2;
            }
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
            int baseMoves;
            int maxMoves;
            switch (band)
            {
                case LevelBand.A:
                    baseMoves = 8;
                    maxMoves = 14;
                    break;
                case LevelBand.B:
                    baseMoves = 12;
                    maxMoves = 20;
                    break;
                case LevelBand.C:
                    baseMoves = 18;
                    maxMoves = 28;
                    break;
                case LevelBand.D:
                    baseMoves = 24;
                    maxMoves = 36;
                    break;
                case LevelBand.E:
                    baseMoves = 30;
                    maxMoves = 42;
                    break;
                default:
                    baseMoves = 8;
                    maxMoves = 14;
                    break;
            }

            float t = GetBandProgress(levelIndex, band);
            int scaled = (int)Math.Round(baseMoves + (maxMoves - baseMoves) * t);
            int complexity = colorCount + bottleCount + Math.Max(0, 2 - emptyCount);
            int result = scaled + (complexity / 4);
            if (result < 6) result = 6;
            if (result > 48) result = 48;
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
            int bandScore = ((int)band) * 1000;
            int levelScore = levelIndex * 20;
            int colorScore = colorCount * 60;
            int bandBottleReference = band == LevelBand.D ? 8 : bottleCount;
            int bottleScore = bandBottleReference * 20;
            return Math.Max(0, bandScore + levelScore + colorScore + bottleScore);
        }
    }
}
