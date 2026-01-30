using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class ScoreCalculatorTests
    {
        [Test]
        public void CalculateScore_RewardsOptimalOrBetter()
        {
            int bonus;
            int scoreAtOptimal = ScoreCalculator.CalculateScore(100, 200, 10, 10, 10, out bonus);
            int scoreBetter = ScoreCalculator.CalculateScore(100, 200, 8, 10, 10, out bonus);
            Assert.GreaterOrEqual(scoreBetter, scoreAtOptimal);
        }

        [Test]
        public void CalculateScore_PenalizesWorseThanOptimal()
        {
            int bonus;
            int scoreOptimal = ScoreCalculator.CalculateScore(100, 200, 10, 10, 10, out bonus);
            int scoreWorse = ScoreCalculator.CalculateScore(100, 200, 15, 10, 10, out bonus);
            Assert.Less(scoreWorse, scoreOptimal);
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
    }
}
