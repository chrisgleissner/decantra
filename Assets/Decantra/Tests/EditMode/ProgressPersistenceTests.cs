/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class ProgressPersistenceTests
    {
        [Test]
        public void ResumePolicy_UsesCurrentLevelWhenAvailable()
        {
            var data = new ProgressData
            {
                HighestUnlockedLevel = 12,
                CurrentLevel = 4
            };

            int resume = ProgressionResumePolicy.ResolveResumeLevel(data);
            Assert.AreEqual(4, resume);
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
