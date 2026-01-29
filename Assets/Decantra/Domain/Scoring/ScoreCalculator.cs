using System;

namespace Decantra.Domain.Scoring
{
    public static class ScoreCalculator
    {
        public static int CalculateScore(int baseScore, int movesUsed, int optimalMoves)
        {
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (movesUsed < 0) throw new ArgumentOutOfRangeException(nameof(movesUsed));
            if (optimalMoves < 0) throw new ArgumentOutOfRangeException(nameof(optimalMoves));

            if (optimalMoves == 0)
            {
                return baseScore;
            }

            int delta = movesUsed - optimalMoves;
            int bonus;
            if (delta <= 0)
            {
                bonus = optimalMoves * 5 + Math.Abs(delta) * 2;
            }
            else
            {
                bonus = Math.Max(0, optimalMoves * 3 - delta * 2);
            }

            return baseScore + bonus;
        }
    }
}
