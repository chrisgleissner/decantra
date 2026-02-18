/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
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
                HighScore = 2200,
                CompletedLevels = new List<int> { 1, 2, 6 }
            };

            store.Save(data);
            var loaded = store.Load();

            Assert.AreEqual(8, loaded.HighestUnlockedLevel);
            Assert.AreEqual(6, loaded.CurrentLevel);
            Assert.AreEqual(4242, loaded.CurrentSeed);
            Assert.AreEqual(1200, loaded.CurrentScore);
            Assert.AreEqual(2200, loaded.HighScore);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 6 }, loaded.CompletedLevels);
        }

        [Test]
        public void ProgressStore_PersistsStarBalance()
        {
            string root = Path.Combine(Path.GetTempPath(), "decantra-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "progress.json");

            var store = new ProgressStore(new[] { path });
            var data = new ProgressData
            {
                HighestUnlockedLevel = 5,
                CurrentLevel = 3,
                CurrentSeed = 999,
                StarBalance = 42
            };

            store.Save(data);
            var loaded = store.Load();

            Assert.AreEqual(42, loaded.StarBalance, "Star balance should survive round-trip.");
        }

        [Test]
        public void ProgressStore_NegativeStarBalance_ClampedToZeroOnLoad()
        {
            string root = Path.Combine(Path.GetTempPath(), "decantra-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "progress.json");

            // Write corrupt data with negative balance
            File.WriteAllText(path, "{\"HighestUnlockedLevel\":1,\"CurrentLevel\":1,\"CurrentSeed\":1,\"StarBalance\":-10}");

            var store = new ProgressStore(new[] { path });
            var loaded = store.Load();

            Assert.GreaterOrEqual(loaded.StarBalance, 0, "Negative star balance should be clamped on load.");
        }

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
