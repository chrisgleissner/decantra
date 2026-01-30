using System;

namespace Decantra.Domain.Scoring
{
    public sealed class ScoreSession
    {
        public int TotalScore { get; private set; }
        public int ProvisionalScore { get; private set; }

        public ScoreSession(int startingTotal = 0)
        {
            if (startingTotal < 0) throw new ArgumentOutOfRangeException(nameof(startingTotal));
            TotalScore = startingTotal;
            ProvisionalScore = 0;
        }

        public void UpdateProvisional(int levelIndex, int optimalMoves, int movesUsed, bool usedUndo, bool usedHints, int streak)
        {
            ProvisionalScore = ScoreCalculator.CalculateLevelScore(levelIndex, optimalMoves, movesUsed, usedUndo, usedHints, streak);
        }

        public void CommitLevel()
        {
            TotalScore += ProvisionalScore;
            ProvisionalScore = 0;
        }

        public void FailLevel()
        {
            ProvisionalScore = 0;
        }

        public void ResetTotal(int total)
        {
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
            TotalScore = total;
        }
    }
}
