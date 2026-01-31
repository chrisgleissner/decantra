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
            session.UpdateProvisional(5, 10, 12, false, false, 0);
            int provisional = session.ProvisionalScore;
            int totalBefore = session.TotalScore;

            session.FailLevel();
            Assert.AreEqual(totalBefore, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);

            session.UpdateProvisional(5, 10, 11, false, false, 0);
            int provisionalAfter = session.ProvisionalScore;
            session.CommitLevel();

            Assert.AreEqual(totalBefore + provisionalAfter, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);
            Assert.Greater(provisionalAfter, 0);
        }

        [Test]
        public void ProvisionalScore_DeclinesWithInefficiency()
        {
            var session = new ScoreSession(0);
            session.BeginAttempt(0);
            session.UpdateProvisional(3, 12, 12, false, false, 0);
            int optimalScore = session.ProvisionalScore;

            session.UpdateProvisional(3, 12, 18, false, false, 0);
            int inefficientScore = session.ProvisionalScore;

            Assert.Greater(optimalScore, inefficientScore);
        }

        [Test]
        public void ResetAttempt_RollsBackTotalScore()
        {
            var session = new ScoreSession(250);
            session.BeginAttempt(250);
            session.UpdateProvisional(4, 8, 9, false, false, 0);
            int provisional = session.ProvisionalScore;

            session.CommitLevel();
            int committed = session.TotalScore;
            Assert.AreEqual(250 + provisional, committed);

            session.BeginAttempt(committed);
            session.UpdateProvisional(4, 8, 12, false, false, 0);
            session.ResetAttempt();

            Assert.AreEqual(committed, session.TotalScore);
            Assert.AreEqual(0, session.ProvisionalScore);
        }
    }
}
