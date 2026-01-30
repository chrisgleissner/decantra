using System;
using System.IO;
using Decantra.App.Services;
using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.PlayMode
{
    public sealed class ProgressPersistenceTests
    {
        [Test]
        public void ProgressStore_PersistsAcrossLoads()
        {
            string root = Path.Combine(Path.GetTempPath(), "decantra-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "progress.json");

            var store = new ProgressStore(new[] { path });
            var data = new ProgressData
            {
                HighestUnlockedLevel = 8,
                CurrentLevel = 6,
                CurrentSeed = 4242,
                CurrentScore = 1200,
                HighScore = 2200
            };

            store.Save(data);
            var loaded = store.Load();

            Assert.AreEqual(8, loaded.HighestUnlockedLevel);
            Assert.AreEqual(6, loaded.CurrentLevel);
            Assert.AreEqual(4242, loaded.CurrentSeed);
            Assert.AreEqual(1200, loaded.CurrentScore);
            Assert.AreEqual(2200, loaded.HighScore);
        }

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
