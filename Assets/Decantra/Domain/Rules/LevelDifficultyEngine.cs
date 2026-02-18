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
            int sinkCount = DetermineSinkCount(levelIndex);
            int emptyCount = ResolveEmptyCount(levelIndex, sinkCount);
            int colorCount = ResolveColorCount(levelIndex, emptyCount);

            // Invariant: Max 9 Bottles
            if (colorCount + emptyCount > 9)
            {
                // Prioritize preserving color count for real difficulty; reduce empties first.
                // Keep the minimum empties required for sinks when possible.
                int overflow = colorCount + emptyCount - 9;
                int minEmpty = sinkCount > 0 ? Math.Min(6, sinkCount + 1) : 1;
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

        private static int ResolveColorCount(int levelIndex, int emptyCount)
        {
            int eff = GetEffectiveLevel(levelIndex);

            // Stepwise scaling to keep bottle counts monotonic while respecting
            // the 9-bottle cap and early-game pacing.
            int baseColorCount;
            if (eff <= 6) baseColorCount = 3;
            else if (eff <= 10) baseColorCount = 4;
            else if (eff <= 17) baseColorCount = 5;
            else if (eff <= 19) baseColorCount = 6;
            else baseColorCount = 7;

            int maxByBottleLimit = Math.Max(3, 9 - Math.Max(0, emptyCount));
            return Math.Min(baseColorCount, maxByBottleLimit);
        }

        private static int ResolveEmptyCount(int levelIndex, int sinkCount)
        {
            int eff = GetEffectiveLevel(levelIndex);

            // Tutorial levels (1-6): 2 empties for easier play
            int baseEmptyCount;
            if (eff <= 6) baseEmptyCount = 2;
            else if (eff <= 19) baseEmptyCount = 1;
            else baseEmptyCount = 2;

            if (sinkCount <= 0)
            {
                return baseEmptyCount;
            }

            // Keep at least one non-sink empty bottle available.
            int requiredForSinks = sinkCount + 1;
            int resolved = Math.Max(baseEmptyCount, requiredForSinks);
            return Math.Min(6, resolved);
        }

        /// <summary>
        /// Resolves the number of sink bottles for a level.
        /// Sinks cannot be poured from, creating strategic constraints.
        /// </summary>
        public static int ResolveSinkCount(int levelIndex)
        {
            return DetermineSinkCount(levelIndex);
        }

        /// <summary>
        /// Deterministically determines sink count by level number only.
        /// </summary>
        public static int DetermineSinkCount(int levelNumber)
        {
            if (levelNumber <= 0) throw new ArgumentOutOfRangeException(nameof(levelNumber));

            if (levelNumber <= 19)
            {
                return 0;
            }

            int roll = HashToPercent(levelNumber);

            if (levelNumber <= 99)
            {
                // 70% -> 0, 30% -> 1
                return roll < 70 ? 0 : 1;
            }

            if (levelNumber <= 299)
            {
                // 20% -> 0, 50% -> 1, 30% -> 2
                if (roll < 20) return 0;
                if (roll < 70) return 1;
                return 2;
            }

            if (levelNumber <= 599)
            {
                // 10% -> 0, 40% -> 1, 35% -> 2, 15% -> 3
                if (roll < 10) return 0;
                if (roll < 50) return 1;
                if (roll < 85) return 2;
                return 3;
            }

            if (levelNumber <= 999)
            {
                // 5% -> 0, 30% -> 1, 35% -> 2, 20% -> 3, 10% -> 4
                if (roll < 5) return 0;
                if (roll < 35) return 1;
                if (roll < 70) return 2;
                if (roll < 90) return 3;
                return 4;
            }

            // 1000+: 5% -> 0, 20% -> 1, 30% -> 2, 25% -> 3, 15% -> 4, 5% -> 5
            if (roll < 5) return 0;
            if (roll < 25) return 1;
            if (roll < 55) return 2;
            if (roll < 80) return 3;
            if (roll < 95) return 4;
            return 5;
        }

        /// <summary>
        /// Deterministic sink role class split: approximately 50/50 by level hash.
        /// True => requires at least one sink usage.
        /// False => sink usage can be fully avoided.
        /// </summary>
        public static bool IsSinkRequiredClass(int levelNumber)
        {
            if (levelNumber <= 0) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            return (HashLevel(levelNumber) & 1) == 0;
        }

        private static int HashToPercent(int levelNumber)
        {
            uint hash = HashLevel(levelNumber);
            return (int)(hash % 100u);
        }

        private static uint HashLevel(int levelNumber)
        {
            unchecked
            {
                uint value = (uint)levelNumber;
                value ^= 0x9E3779B9u;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                return value;
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
            int eff = GetEffectiveLevel(levelIndex);

            // Keep scramble depth bounded to preserve generation/solve latency guarantees.
            float t = GetLinearProgress(levelIndex);
            int baseMoves = 4 + (int)Math.Round(t * 12);

            // Color complexity bonus (bounded).
            int colorBonus = Math.Max(0, (colorCount - 3) / 2);

            // Sink levels get a modest bump without exploding search cost.
            int sinkBonus = (eff >= 18) ? 1 : 0;

            int result = baseMoves + colorBonus + sinkBonus;

            // Clamp to valid range
            result = Math.Max(6, result);
            result = Math.Min(18, result);

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
            // Keep profile rating strictly deterministic and monotonic through linear difficulty progression.
            // Intrinsic difficulty is scored separately from solver-derived metrics.
            return GetDifficultyForLevel(levelIndex) * 100;
        }
    }
}
