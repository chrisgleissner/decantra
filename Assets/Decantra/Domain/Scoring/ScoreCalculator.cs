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

        public static int CalculateScore(int baseScore, int emptyTransitionScore, int movesUsed, int movesAllowed, int optimalMoves, out int bonus)
        {
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (emptyTransitionScore < 0) throw new ArgumentOutOfRangeException(nameof(emptyTransitionScore));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));
            bonus = 0;

            if (movesAllowed > 0 && movesUsed <= movesAllowed)
            {
                bonus += (movesAllowed - movesUsed + 1) * 50;
                if (optimalMoves > 0)
                {
                    bonus += Math.Max(0, (optimalMoves - movesUsed) * 20);
                }
            }

            return baseScore + emptyTransitionScore + bonus;
        }
    }
}
