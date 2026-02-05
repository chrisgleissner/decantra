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
    public class ScoreCalculatorTests
    {
        [Test]
        public void EfficiencyScore_DropsWithInefficientPlay()
        {
            // Difficulty 80 uses the 70-100 band; movesAllowed 20 gives slack 10 for x.
            int nearOptimal = ScoreCalculator.CalculateLevelScore(10, 10, 20, 80, true);
            int inefficient = ScoreCalculator.CalculateLevelScore(10, 18, 20, 80, true);
            Assert.Greater(nearOptimal, inefficient);
        }

        [Test]
        public void EfficiencyScore_DeterministicForSameInputs()
        {
            // Same inputs must return identical score by design (slack 10, difficulty 75 in band).
            int scoreA = ScoreCalculator.CalculateLevelScore(12, 16, 22, 75, false);
            int scoreB = ScoreCalculator.CalculateLevelScore(12, 16, 22, 75, false);
            Assert.AreEqual(scoreA, scoreB);
        }

        [Test]
        public void Stars_FollowSlackThresholds()
        {
            // slack=10 => thresholds at 2,4,6,8 moves over optimal.
            Assert.AreEqual(5, ScoreCalculator.CalculateStars(10, 10, 20));
            Assert.AreEqual(4, ScoreCalculator.CalculateStars(10, 12, 20));
            Assert.AreEqual(3, ScoreCalculator.CalculateStars(10, 14, 20));
            Assert.AreEqual(2, ScoreCalculator.CalculateStars(10, 16, 20));
            Assert.AreEqual(1, ScoreCalculator.CalculateStars(10, 18, 20));
            Assert.AreEqual(0, ScoreCalculator.CalculateStars(10, 19, 20));
        }

        [Test]
        public void TotalScore_UsesDiminishingReturns()
        {
            // Higher current total should reduce the incremental gain.
            int baseScore = 200; // Simple score to compare increments.
            int firstTotal = ScoreCalculator.CalculateTotalScore(0, baseScore);
            int laterTotal = ScoreCalculator.CalculateTotalScore(800000, baseScore);
            int firstIncrement = firstTotal - 0;
            int laterIncrement = laterTotal - 800000;
            Assert.Greater(firstIncrement, laterIncrement);
        }

        [Test]
        public void Grade_TracksEfficiencyThresholds()
        {
            Assert.AreEqual(PerformanceGrade.S, ScoreCalculator.CalculateGrade(10, 10));
            Assert.AreEqual(PerformanceGrade.A, ScoreCalculator.CalculateGrade(10, 11));
            Assert.AreEqual(PerformanceGrade.B, ScoreCalculator.CalculateGrade(10, 12));
            Assert.AreEqual(PerformanceGrade.C, ScoreCalculator.CalculateGrade(10, 15));
            Assert.AreEqual(PerformanceGrade.D, ScoreCalculator.CalculateGrade(10, 19));
            Assert.AreEqual(PerformanceGrade.E, ScoreCalculator.CalculateGrade(10, 25));
        }
    }
}
