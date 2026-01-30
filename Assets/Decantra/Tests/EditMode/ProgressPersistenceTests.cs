using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class ProgressPersistenceTests
    {
        [Test]
        public void ResumePolicy_UsesLastPlayableLevel()
        {
            var data = new ProgressData
            {
                HighestUnlockedLevel = 12,
                CurrentLevel = 4
            };

            int resume = ProgressionResumePolicy.ResolveResumeLevel(data);
            Assert.AreEqual(12, resume);
        }

        [Test]
        public void ResumePolicy_NeverRegressesBelowOne()
        {
            var data = new ProgressData
            {
                HighestUnlockedLevel = 0,
                CurrentLevel = 0
            };

            int resume = ProgressionResumePolicy.ResolveResumeLevel(data);
            Assert.AreEqual(1, resume);
        }
    }
}
