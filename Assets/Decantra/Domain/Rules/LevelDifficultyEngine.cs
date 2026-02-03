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
        /// <summary>
        /// Maximum effective level for generation parameters.
        /// All difficulty-driving parameters clamp at this level.
        /// Levels beyond this differ only by seed-based layout randomness.
        /// </summary>
        public const int MaxEffectiveLevel = 100;

        /// <summary>
        /// Linear difficulty is enforced through level 200.
        /// </summary>
        public const int LinearDifficultyMaxLevel = 200;

        /// <summary>
        /// Clamp value applied after LinearDifficultyMaxLevel.
        /// </summary>
        public const int DifficultyClampValue = 100;

        /// <summary>
        /// Returns the effective level index for difficulty calculations.
        /// This enforces the difficulty plateau at level 100+.
        /// </summary>
        public static int GetEffectiveLevel(int levelIndex)
        {
            return Math.Min(levelIndex, MaxEffectiveLevel);
        }

        /// <summary>
        /// Returns the deterministic difficulty value for solver output.
        /// Levels 1..200 map to their level index; levels 201+ clamp to 100.
        /// </summary>
        public static int GetDifficultyForLevel(int levelIndex)
        {
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            if (levelIndex <= LinearDifficultyMaxLevel)
            {
                return levelIndex;
            }

            return DifficultyClampValue;
        }

        /// <summary>
        /// Linear interpolation factor for level 1 to MaxEffectiveLevel.
        /// Returns 0.0 at level 1, 1.0 at level MaxEffectiveLevel.
        /// </summary>
        public static float GetLinearProgress(int levelIndex)
        {
            int eff = GetEffectiveLevel(levelIndex);
            return (eff - 1) / (float)(MaxEffectiveLevel - 1);
        }

        public static DifficultyProfile GetProfile(int levelIndex)
        {
            if (levelIndex <= 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));

            var band = ResolveBand(levelIndex);
            int colorCount = ResolveColorCount(levelIndex);
            int emptyCount = ResolveEmptyCount(levelIndex);

            // Invariant: Max 9 Bottles
            if (colorCount + emptyCount > 9)
            {
                // Prioritize preserving color count for real difficulty; reduce empties first.
                // Keep the minimum empties required for sinks (>=18) when possible.
                int overflow = colorCount + emptyCount - 9;
                int minEmpty = ResolveSinkCount(levelIndex) > 0 ? 2 : 1;
                int reducibleEmpty = Math.Max(0, emptyCount - minEmpty);
                int emptyReduction = Math.Min(overflow, reducibleEmpty);
                emptyCount -= emptyReduction;
                overflow -= emptyReduction;

                if (overflow > 0)
                {
                    colorCount = Math.Max(1, colorCount - overflow);
                }
            }

            int bottleCount = colorCount + emptyCount;
            int reverseMoves = ComputeReverseMoves(levelIndex, band, colorCount, emptyCount, bottleCount);
            var themeId = ResolveThemeId(band, levelIndex);
            int rating = ComputeDifficultyRating(levelIndex, band, colorCount, emptyCount, bottleCount, reverseMoves);

            return new DifficultyProfile(levelIndex, band, bottleCount, colorCount, emptyCount, reverseMoves, themeId, rating);
        }

        private static LevelBand ResolveBand(int levelIndex)
        {
            int eff = GetEffectiveLevel(levelIndex);
            if (eff <= 10) return LevelBand.A;
            if (eff <= 25) return LevelBand.B;
            if (eff <= 50) return LevelBand.C;
            if (eff <= 75) return LevelBand.D;
            return LevelBand.E;
        }

        private static int ResolveColorCount(int levelIndex)
        {
            int eff = GetEffectiveLevel(levelIndex);

            // Stepwise scaling to keep bottle counts monotonic while respecting
            // the 9-bottle cap and early-game pacing.
            if (eff <= 6) return 3;
            if (eff <= 10) return 4;
            if (eff <= 17) return 5;
            if (eff <= 19) return 6;
            return 7;
        }

        private static int ResolveEmptyCount(int levelIndex)
        {
            int eff = GetEffectiveLevel(levelIndex);

            // Tutorial levels (1-6): 2 empties for easier play
            if (eff <= 6) return 2;

            // Mid levels (7-17): 1 empty for tighter constraints
            if (eff <= 17) return 1;

            // Sink levels (18+): 2 empties, with one being a sink
            return 2;
        }

        /// <summary>
        /// Resolves the number of sink bottles for a level.
        /// Sinks cannot be poured from, creating strategic constraints.
        /// </summary>
        public static int ResolveSinkCount(int levelIndex)
        {
            int eff = GetEffectiveLevel(levelIndex);

            // No sinks before level 18
            if (eff < 18) return 0;

            // Levels 18+: keep a single sink to avoid excessive solver complexity
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
            int eff = GetEffectiveLevel(levelIndex);

            // Linear scaling: 6 reverse moves at level 1, 30 at level 100
            float t = GetLinearProgress(levelIndex);
            int baseMoves = 6 + (int)Math.Round(t * 24);

            // Color complexity bonus
            int colorBonus = (colorCount - 3) * 1;

            // Sink level bonus (levels 18+): deeper scrambling needed
            int sinkBonus = (eff >= 18) ? 4 : 0;

            int result = baseMoves + colorBonus + sinkBonus;

            // Clamp to valid range
            result = Math.Max(8, result);
            result = Math.Min(30, result);

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
            // This is the PROFILE difficulty rating, not the actual intrinsic difficulty.
            // Actual difficulty is computed by DifficultyScorer from solver metrics.
            int eff = GetEffectiveLevel(levelIndex);

            int bandScore = ((int)band) * 900;
            int levelScore = eff * 25;
            int colorScore = colorCount * 70;
            int bottleScore = bottleCount * 30;
            int emptyScore = emptyCount * 50;
            return Math.Max(0, bandScore + levelScore + colorScore + bottleScore + emptyScore);
        }
    }
}
