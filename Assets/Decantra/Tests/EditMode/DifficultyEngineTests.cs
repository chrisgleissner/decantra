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
        public void ProfileCounts_FollowBandRules()
        {
            var bandA = LevelDifficultyEngine.GetProfile(1);
            Assert.AreEqual(3, bandA.ColorCount);
            Assert.AreEqual(3, bandA.EmptyBottleCount);
            Assert.AreEqual(6, bandA.BottleCount);

            var bandB = LevelDifficultyEngine.GetProfile(11);
            Assert.AreEqual(4, bandB.ColorCount);
            Assert.AreEqual(2, bandB.EmptyBottleCount);
            Assert.AreEqual(6, bandB.BottleCount);

            var bandC = LevelDifficultyEngine.GetProfile(26);
            Assert.AreEqual(5, bandC.ColorCount);
            Assert.AreEqual(2, bandC.EmptyBottleCount);
            Assert.AreEqual(7, bandC.BottleCount);

            var bandDodd = LevelDifficultyEngine.GetProfile(51);
            Assert.AreEqual(6, bandDodd.ColorCount);
            Assert.AreEqual(1, bandDodd.EmptyBottleCount);
            Assert.AreEqual(7, bandDodd.BottleCount);

            var bandDeven = LevelDifficultyEngine.GetProfile(52);
            Assert.AreEqual(6, bandDeven.ColorCount);
            Assert.AreEqual(2, bandDeven.EmptyBottleCount);
            Assert.AreEqual(8, bandDeven.BottleCount);

            var bandE = LevelDifficultyEngine.GetProfile(76);
            Assert.AreEqual(8, bandE.ColorCount);
            Assert.AreEqual(1, bandE.EmptyBottleCount);
            Assert.AreEqual(9, bandE.BottleCount);
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
            const int optimalMoves = 24;
            var levels = new[] { 5, 15, 30, 60, 90 };
            int previousSurplus = int.MaxValue;

            for (int i = 0; i < levels.Length; i++)
            {
                var profile = LevelDifficultyEngine.GetProfile(levels[i]);
                int allowed = MoveAllowanceCalculator.ComputeMovesAllowed(profile, optimalMoves);
                int surplus = allowed - optimalMoves;
                Assert.GreaterOrEqual(allowed, optimalMoves);
                Assert.LessOrEqual(surplus, previousSurplus, $"Surplus increased at level {levels[i]}");
                previousSurplus = surplus;
            }
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
