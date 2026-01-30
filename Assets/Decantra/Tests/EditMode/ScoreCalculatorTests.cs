using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class ScoreCalculatorTests
    {
        [Test]
        public void CalculatePourIncrement_ScalesWithLevelAndUnits()
        {
            int low = ScoreCalculator.CalculatePourIncrement(1, 2);
            int high = ScoreCalculator.CalculatePourIncrement(3, 2);
            int moreUnits = ScoreCalculator.CalculatePourIncrement(1, 4);

            Assert.Greater(high, low);
            Assert.Greater(moreUnits, low);
        }

        [Test]
        public void CalculateStarBonus_ScalesWithStarsAndLevel()
        {
            int low = ScoreCalculator.CalculateStarBonus(1, 1);
            int high = ScoreCalculator.CalculateStarBonus(1, 5);
            int higherLevel = ScoreCalculator.CalculateStarBonus(3, 1);

            Assert.Greater(high, low);
            Assert.Greater(higherLevel, low);
        }

        [Test]
        public void EmptyTransitionIncrement_ScalesWithLevelAndUnits()
        {
            int low = ScoreCalculator.CalculateEmptyTransitionIncrement(1, 2);
            int high = ScoreCalculator.CalculateEmptyTransitionIncrement(3, 2);
            int moreUnits = ScoreCalculator.CalculateEmptyTransitionIncrement(1, 4);

            Assert.Greater(high, low);
            Assert.Greater(moreUnits, low);
        }

        [Test]
        public void CalculateScore_SumsComponents()
        {
            int score = ScoreCalculator.CalculateScore(100, 20, 30, 40);
            Assert.AreEqual(190, score);
        }
    }
}
