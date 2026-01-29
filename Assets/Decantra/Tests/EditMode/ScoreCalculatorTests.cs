using Decantra.Domain.Scoring;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public class ScoreCalculatorTests
    {
        [Test]
        public void CalculateScore_RewardsOptimalOrBetter()
        {
            int scoreAtOptimal = ScoreCalculator.CalculateScore(100, 10, 10);
            int scoreBetter = ScoreCalculator.CalculateScore(100, 8, 10);
            Assert.GreaterOrEqual(scoreBetter, scoreAtOptimal);
        }

        [Test]
        public void CalculateScore_PenalizesWorseThanOptimal()
        {
            int scoreOptimal = ScoreCalculator.CalculateScore(100, 10, 10);
            int scoreWorse = ScoreCalculator.CalculateScore(100, 15, 10);
            Assert.Less(scoreWorse, scoreOptimal);
        }
    }
}
