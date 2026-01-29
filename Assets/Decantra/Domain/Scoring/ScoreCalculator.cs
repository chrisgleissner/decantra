using System;

namespace Decantra.Domain.Scoring
{
    public static class ScoreCalculator
    {
        public static int CalculateScore(int baseScore, int filledUnits, int movesUsed, int movesAllowed, int optimalMoves, out int bonus)
        {
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (filledUnits < 0) throw new ArgumentOutOfRangeException(nameof(filledUnits));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (movesAllowed < 0) throw new ArgumentOutOfRangeException(nameof(movesAllowed));
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));

            int fillScore = filledUnits * 10;
            bonus = 0;

            if (movesAllowed > 0 && movesUsed <= movesAllowed)
            {
                bonus += (movesAllowed - movesUsed + 1) * 50;
                if (optimalMoves > 0)
                {
                    bonus += Math.Max(0, (optimalMoves - movesUsed) * 20);
                }
            }

            return baseScore + fillScore + bonus;
        }
    }
}
