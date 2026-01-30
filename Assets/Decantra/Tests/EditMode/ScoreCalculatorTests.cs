using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class ScoreCalculatorTests
    {
        [Test]
        public void EfficiencyScore_DropsWithInefficientPlay()
        {
            int nearOptimal = ScoreCalculator.CalculateLevelScore(5, 10, 10, false, false, 0);
            int inefficient = ScoreCalculator.CalculateLevelScore(5, 10, 20, false, false, 0);
            Assert.Greater(nearOptimal, inefficient);
        }

        [Test]
        public void EfficiencyScore_DeterministicForSameInputs()
        {
            int scoreA = ScoreCalculator.CalculateLevelScore(12, 16, 18, false, false, 1);
            int scoreB = ScoreCalculator.CalculateLevelScore(12, 16, 18, false, false, 1);
            Assert.AreEqual(scoreA, scoreB);
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
