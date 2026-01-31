/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Scoring
{
    public sealed class ScoreSession
    {
        public int TotalScore { get; private set; }
        public int ProvisionalScore { get; private set; }
        public int AttemptStartTotalScore { get; private set; }

        public ScoreSession(int startingTotal = 0)
        {
            if (startingTotal < 0) throw new ArgumentOutOfRangeException(nameof(startingTotal));
            TotalScore = startingTotal;
            AttemptStartTotalScore = startingTotal;
            ProvisionalScore = 0;
        }

        public void BeginAttempt(int totalScore)
        {
            if (totalScore < 0) throw new ArgumentOutOfRangeException(nameof(totalScore));
            AttemptStartTotalScore = totalScore;
            TotalScore = totalScore;
            ProvisionalScore = 0;
        }

        public void UpdateProvisional(int levelIndex, int optimalMoves, int movesUsed, bool usedUndo, bool usedHints, int streak)
        {
            ProvisionalScore = ScoreCalculator.CalculateLevelScore(levelIndex, optimalMoves, movesUsed, usedUndo, usedHints, streak);
        }

        public void CommitLevel()
        {
            TotalScore = AttemptStartTotalScore + ProvisionalScore;
            ProvisionalScore = 0;
            AttemptStartTotalScore = TotalScore;
        }

        public void FailLevel()
        {
            ResetAttempt();
        }

        public void ResetAttempt()
        {
            TotalScore = AttemptStartTotalScore;
            ProvisionalScore = 0;
        }

        public void ResetTotal(int total)
        {
            BeginAttempt(total);
        }
    }
}
