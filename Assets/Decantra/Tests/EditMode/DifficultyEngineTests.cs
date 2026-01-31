using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class DifficultyEngineTests
    {
        [TestCase(1, LevelBand.A)]
        [TestCase(10, LevelBand.A)]
        [TestCase(11, LevelBand.B)]
        [TestCase(25, LevelBand.B)]
        [TestCase(26, LevelBand.C)]
        [TestCase(50, LevelBand.C)]
        [TestCase(51, LevelBand.D)]
        [TestCase(75, LevelBand.D)]
        [TestCase(76, LevelBand.E)]
        [TestCase(120, LevelBand.E)]
        public void BandSelection_UsesDefinedBoundaries(int level, LevelBand expected)
        {
            var profile = LevelDifficultyEngine.GetProfile(level);
            Assert.AreEqual(expected, profile.Band);
        }

        [Test]
        public void ProfileCounts_ProgressMonotonically()
        {
            var levels = new[] { 1, 10, 25, 50, 100, 200, 500 };
            int previousColors = 0;
            int previousBottles = 0;
            int previousEmpty = int.MaxValue;

            foreach (int level in levels)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                Assert.GreaterOrEqual(profile.ColorCount, previousColors, $"Color count regressed at level {level}");
                Assert.GreaterOrEqual(profile.BottleCount, previousBottles, $"Bottle count regressed at level {level}");
                Assert.LessOrEqual(profile.EmptyBottleCount, previousEmpty, $"Empty bottle count increased at level {level}");
                Assert.AreEqual(profile.ColorCount + profile.EmptyBottleCount, profile.BottleCount);
                previousColors = profile.ColorCount;
                previousBottles = profile.BottleCount;
                previousEmpty = profile.EmptyBottleCount;
            }
        }

        [Test]
        public void Level20To25_MeetsStructuralTargets()
        {
            var profile20 = LevelDifficultyEngine.GetProfile(20);
            var profile25 = LevelDifficultyEngine.GetProfile(25);

            Assert.GreaterOrEqual(profile20.BottleCount, 9);
            Assert.GreaterOrEqual(profile20.ColorCount, 6);
            Assert.LessOrEqual(profile20.EmptyBottleCount, 1);

            Assert.GreaterOrEqual(profile25.BottleCount, 9);
            Assert.GreaterOrEqual(profile25.ColorCount, 6);
            Assert.LessOrEqual(profile25.EmptyBottleCount, 1);
        }

        [Test]
        public void DifficultyRating_IsMonotonic()
        {
            int previous = -1;
            for (int level = 1; level <= 120; level++)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                Assert.GreaterOrEqual(profile.DifficultyRating, previous, $"Difficulty dropped at level {level}");
                previous = profile.DifficultyRating;
            }
        }

        [Test]
        public void MoveAllowance_SurplusTightensByBand()
        {
            const int optimalMoves = 10;
            var levels = new[] { 1, 25, 50, 100, 250, 500 };
            int previousAllowed = int.MaxValue;

            foreach (int level in levels)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                int allowed = MoveAllowanceCalculator.ComputeMovesAllowed(profile, optimalMoves);
                Assert.GreaterOrEqual(allowed, optimalMoves);
                Assert.LessOrEqual(allowed, previousAllowed, $"Allowed moves increased at level {level}");
                previousAllowed = allowed;
            }
        }

        [Test]
        public void MoveAllowance_UsesSlackFactorEndpoints()
        {
            const int optimalMoves = 10;
            var earlyProfile = LevelDifficultyEngine.GetProfile(1);
            var lateProfile = LevelDifficultyEngine.GetProfile(500);

            int earlyAllowed = MoveAllowanceCalculator.ComputeMovesAllowed(earlyProfile, optimalMoves);
            int lateAllowed = MoveAllowanceCalculator.ComputeMovesAllowed(lateProfile, optimalMoves);

            Assert.AreEqual(20, earlyAllowed, "Slack factor should start at 2.0 for early levels.");
            Assert.AreEqual(10, lateAllowed, "Slack factor should reach 1.0 by level 500.");
        }

        [Test]
        public void BackgroundTheme_IsDeterministicByBand()
        {
            var profileA = LevelDifficultyEngine.GetProfile(1);
            var profileARepeat = LevelDifficultyEngine.GetProfile(1);
            Assert.AreEqual(profileA.ThemeId, profileARepeat.ThemeId);

            var profileB = LevelDifficultyEngine.GetProfile(12);
            Assert.AreNotEqual(profileA.ThemeId, profileB.ThemeId);

            var profileD = LevelDifficultyEngine.GetProfile(51);
            var profileDNext = LevelDifficultyEngine.GetProfile(52);
            Assert.AreNotEqual(profileD.ThemeId, profileDNext.ThemeId);
        }
    }
}
