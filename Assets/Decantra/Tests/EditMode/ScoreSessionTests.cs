/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class ScoreSessionTests
    {
        [Test]
        public void Commit_AddsProvisionalOnlyOnSuccess_AndResetsOnFail()
        {
            var session = new ScoreSession(100);
            session.BeginAttempt(100);
            // Difficulty 80 and movesAllowed 20 are in the scoring spec band.
            session.UpdateProvisional(10, 12, 20, 80, true);
            int provisional = session.ProvisionalScore;
            int totalBefore = session.TotalScore;

            session.FailLevel();
            Assert.AreEqual(totalBefore, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);

            session.UpdateProvisional(10, 11, 20, 80, true);
            int provisionalAfter = session.ProvisionalScore;
            session.CommitLevel();

            int expectedTotal = ScoreCalculator.CalculateTotalScore(totalBefore, provisionalAfter);
            Assert.AreEqual(expectedTotal, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);
            Assert.Greater(provisionalAfter, 0);
        }

        [Test]
        public void ProvisionalScore_DeclinesWithInefficiency()
        {
            var session = new ScoreSession(0);
            session.BeginAttempt(0);
            // Slack 10 and difficulty 80 are stable scoring inputs.
            session.UpdateProvisional(12, 12, 22, 80, true);
            int optimalScore = session.ProvisionalScore;

            session.UpdateProvisional(12, 18, 22, 80, true);
            int inefficientScore = session.ProvisionalScore;

            Assert.Greater(optimalScore, inefficientScore);
        }

        [Test]
        public void ResetAttempt_RollsBackTotalScore()
        {
            var session = new ScoreSession(250);
            session.BeginAttempt(250);
            // Difficulty 75 keeps d within the 70-100 normalization band; movesAllowed 18 gives slack 10.
            session.UpdateProvisional(8, 9, 18, 75, true);
            int provisional = session.ProvisionalScore;

            session.CommitLevel();
            int committed = session.TotalScore;
            Assert.AreEqual(ScoreCalculator.CalculateTotalScore(250, provisional), committed);

            session.BeginAttempt(committed);
            session.UpdateProvisional(8, 12, 18, 75, true);
            session.ResetAttempt();

            Assert.AreEqual(committed, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);
        }

        [Test]
        public void Commit_WithExplicitAwardedScore_UsesProvidedValue()
        {
            var session = new ScoreSession(300);
            session.BeginAttempt(300);
            session.UpdateProvisional(10, 10, 20, 80, true);

            int overrideAwardedScore = 17;
            session.CommitLevel(overrideAwardedScore);

            int expected = ScoreCalculator.CalculateTotalScore(300, overrideAwardedScore);
            Assert.AreEqual(expected, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);
            Assert.AreEqual(expected, session.AttemptStartTotalScore);
        }
    }
}
