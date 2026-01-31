/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Persistence;
using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class PerformanceRecordTests
    {
        [Test]
        public void BestPerformance_DoesNotRegress()
        {
            var data = new ProgressData();

            var first = new LevelPerformanceRecord
            {
                LevelIndex = 5,
                BestMoves = 14,
                BestEfficiency = 0.85f,
                BestGrade = PerformanceGrade.B
            };

            PerformanceTracker.UpdateBest(data, first);
            var stored = PerformanceTracker.GetBest(data, 5);
            Assert.AreEqual(14, stored.BestMoves);
            Assert.AreEqual(PerformanceGrade.B, stored.BestGrade);

            var worse = new LevelPerformanceRecord
            {
                LevelIndex = 5,
                BestMoves = 18,
                BestEfficiency = 0.7f,
                BestGrade = PerformanceGrade.C
            };

            PerformanceTracker.UpdateBest(data, worse);
            var afterWorse = PerformanceTracker.GetBest(data, 5);
            Assert.AreEqual(14, afterWorse.BestMoves);
            Assert.AreEqual(PerformanceGrade.B, afterWorse.BestGrade);
        }

        [Test]
        public void BestPerformance_ImprovesOnBetterResult()
        {
            var data = new ProgressData();
            PerformanceTracker.UpdateBest(data, new LevelPerformanceRecord
            {
                LevelIndex = 9,
                BestMoves = 22,
                BestEfficiency = 0.7f,
                BestGrade = PerformanceGrade.C
            });

            PerformanceTracker.UpdateBest(data, new LevelPerformanceRecord
            {
                LevelIndex = 9,
                BestMoves = 18,
                BestEfficiency = 0.9f,
                BestGrade = PerformanceGrade.A
            });

            var best = PerformanceTracker.GetBest(data, 9);
            Assert.AreEqual(18, best.BestMoves);
            Assert.AreEqual(PerformanceGrade.A, best.BestGrade);
        }
    }
}
