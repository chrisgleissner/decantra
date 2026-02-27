/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Persistence;
using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class PolishProgressTests
    {
        [Test]
        public void PerfectQualification_Fails_WhenAutoSolveUsed()
        {
            Assert.IsFalse(PerfectStreakTracker.IsPerfectCompletion(5, autoSolveUsed: true, blackBottleConverted: false));
        }

        [Test]
        public void PerfectQualification_Fails_WhenBlackBottleConverted()
        {
            Assert.IsFalse(PerfectStreakTracker.IsPerfectCompletion(5, autoSolveUsed: false, blackBottleConverted: true));
        }

        [Test]
        public void Streak_Resets_OnNonPerfectCompletion()
        {
            var data = new ProgressData { SessionCurrentPerfectStreak = 4 };
            PerfectStreakTracker.RecordCompletion(data, isPerfect: false, out bool _, out int _);
            Assert.AreEqual(0, data.SessionCurrentPerfectStreak);
        }

        [Test]
        public void LifetimeStreakAndPerformance_Persist_AcrossReset()
        {
            var bests = new List<LevelPerformanceRecord>
            {
                new LevelPerformanceRecord { LevelIndex = 1, BestStars = 4, BestMoves = 18 }
            };
            var data = new ProgressData
            {
                HighScore = 300,
                HighestUnlockedLevel = 9,
                LifetimeBestPerfectStreak = 7,
                LifetimeOptimalCount = 11,
                SessionCurrentPerfectStreak = 3,
                SessionBestPerfectStreak = 3,
                BestPerformances = bests
            };

            var reset = ProgressResetPolicy.ResetForNewGame(data);

            Assert.AreEqual(1, reset.CurrentLevel);
            Assert.AreEqual(0, reset.CurrentScore);
            Assert.AreEqual(0, reset.SessionCurrentPerfectStreak);
            Assert.AreEqual(0, reset.SessionBestPerfectStreak);
            Assert.AreEqual(7, reset.LifetimeBestPerfectStreak);
            Assert.AreEqual(11, reset.LifetimeOptimalCount);
            Assert.AreEqual(4, reset.BestPerformances[0].BestStars);
            Assert.AreEqual(18, reset.BestPerformances[0].BestMoves);
        }

        [Test]
        public void PersonalBest_Detection_Works_AfterReset()
        {
            var previous = new ProgressData
            {
                BestPerformances = new List<LevelPerformanceRecord>
                {
                    new LevelPerformanceRecord
                    {
                        LevelIndex = 1,
                        BestStars = 4,
                        BestMoves = 20,
                        BestDeviation = 5,
                        TimesCompleted = 1
                    }
                }
            };
            var reset = ProgressResetPolicy.ResetForNewGame(previous);
            var feedback = PerformanceTracker.RecordCompletion(
                reset,
                levelIndex: 1,
                stars: 4,
                moves: 18,
                optimalMoves: 15,
                efficiency: 0.8f,
                grade: PerformanceGrade.B);

            Assert.IsTrue(feedback.IsPersonalBest);
            Assert.AreEqual("Personal best", feedback.Message);
            var record = PerformanceTracker.GetBest(reset, 1);
            Assert.AreEqual(18, record.BestMoves);
            Assert.AreEqual(3, record.BestDeviation);
        }
    }
}
