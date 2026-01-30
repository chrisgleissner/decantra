using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class ScoreSessionTests
    {
        [Test]
        public void Commit_AddsProvisionalOnlyOnSuccess()
        {
            var session = new ScoreSession();
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
            var session = new ScoreSession();
            session.UpdateProvisional(3, 12, 12, false, false, 0);
            int optimalScore = session.ProvisionalScore;

            session.UpdateProvisional(3, 12, 18, false, false, 0);
            int inefficientScore = session.ProvisionalScore;

            Assert.Greater(optimalScore, inefficientScore);
        }
    }
}
