using System;

namespace Decantra.Domain.Scoring
{
    public static class ScoreCalculator
    {
        public static int CalculateEmptyTransitionIncrement(int levelIndex, int emptiedUnits)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (emptiedUnits < 0) throw new ArgumentOutOfRangeException(nameof(emptiedUnits));

            int scaledLevel = Math.Max(1, levelIndex);
            return emptiedUnits * scaledLevel * 10;
        }

        public static int CalculatePourIncrement(int levelIndex, int pouredUnits)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (pouredUnits < 0) throw new ArgumentOutOfRangeException(nameof(pouredUnits));

            int scaledLevel = Math.Max(1, levelIndex);
            int perUnit = 8 + scaledLevel * 2;
            return pouredUnits * perUnit;
        }

        public static int CalculateStarBonus(int levelIndex, int stars)
        {
            if (levelIndex < 0) throw new ArgumentOutOfRangeException(nameof(levelIndex));
            if (stars < 0) throw new ArgumentOutOfRangeException(nameof(stars));

            int scaledLevel = Math.Max(1, levelIndex);
            int clampedStars = Math.Min(5, Math.Max(0, stars));
            return clampedStars * (200 + scaledLevel * 20);
        }

        public static int CalculateScore(int baseScore, int emptyTransitionScore, int pourScore, int starBonus)
        {
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (emptyTransitionScore < 0) throw new ArgumentOutOfRangeException(nameof(emptyTransitionScore));
            if (pourScore < 0) throw new ArgumentOutOfRangeException(nameof(pourScore));
            if (starBonus < 0) throw new ArgumentOutOfRangeException(nameof(starBonus));

            return baseScore + emptyTransitionScore + pourScore + starBonus;
        }
    }
}
