/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
            int previousBottles = 0;

            foreach (int level in levels)
            {
                var profile = LevelDifficultyEngine.GetProfile(level);
                Assert.GreaterOrEqual(profile.BottleCount, previousBottles, $"Bottle count regressed at level {level}");
                Assert.AreEqual(profile.ColorCount + profile.EmptyBottleCount, profile.BottleCount);
                Assert.LessOrEqual(profile.BottleCount, 9, $"Bottle cap exceeded at level {level}");

                int sinkCount = LevelDifficultyEngine.DetermineSinkCount(level);
                Assert.LessOrEqual(sinkCount, 5, $"Sink cap exceeded at level {level}");
                if (sinkCount > 0)
                {
                    Assert.GreaterOrEqual(profile.EmptyBottleCount, sinkCount,
                        $"Expected at least {sinkCount} empty bottles for sinks at level {level}");
                }

                previousBottles = profile.BottleCount;
            }
        }

        [Test]
        public void Level20To25_MeetsStructuralTargets()
        {
            var profile20 = LevelDifficultyEngine.GetProfile(20);
            var profile25 = LevelDifficultyEngine.GetProfile(25);

            Assert.GreaterOrEqual(profile20.BottleCount, 7);
            Assert.LessOrEqual(profile20.BottleCount, 9);
            Assert.GreaterOrEqual(profile20.ColorCount, 3);
            Assert.LessOrEqual(profile20.EmptyBottleCount, 6);

            Assert.GreaterOrEqual(profile25.BottleCount, 7);
            Assert.LessOrEqual(profile25.BottleCount, 9);
            Assert.GreaterOrEqual(profile25.ColorCount, 3);
            Assert.LessOrEqual(profile25.EmptyBottleCount, 6);
        }

        [Test]
        public void DetermineSinkCount_RespectsHardBounds()
        {
            for (int level = 1; level <= 1500; level++)
            {
                int sinks = LevelDifficultyEngine.DetermineSinkCount(level);
                Assert.GreaterOrEqual(sinks, 0);
                Assert.LessOrEqual(sinks, 5);
                if (level < 20)
                {
                    Assert.AreEqual(0, sinks, $"No sinks expected before level 20 (level {level}).");
                }
            }
        }

        [Test]
        public void SinkRoleClass_IsDeterministicByLevel()
        {
            for (int level = 1; level <= 1000; level++)
            {
                bool a = LevelDifficultyEngine.IsSinkRequiredClass(level);
                bool b = LevelDifficultyEngine.IsSinkRequiredClass(level);
                Assert.AreEqual(a, b, $"Sink class must be deterministic at level {level}");
            }
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

        [Test]
        public void DifficultyInvariant_UsesLinearTo200ThenClamp()
        {
            Assert.AreEqual(1, LevelDifficultyEngine.GetDifficultyForLevel(1));
            Assert.AreEqual(200, LevelDifficultyEngine.GetDifficultyForLevel(200));
            Assert.AreEqual(100, LevelDifficultyEngine.GetDifficultyForLevel(201));
            Assert.AreEqual(100, LevelDifficultyEngine.GetDifficultyForLevel(500));

            for (int level = 1; level <= 200; level++)
            {
                Assert.AreEqual(level, LevelDifficultyEngine.GetDifficultyForLevel(level),
                    $"Difficulty should match level for {level}");
            }
        }

        [Test]
        public void DetermineSinkCount_DistributionBands_20To99()
        {
            int sinkCount = 0;
            int total = 99 - 20 + 1; // 80 levels
            for (int level = 20; level <= 99; level++)
            {
                int sinks = LevelDifficultyEngine.DetermineSinkCount(level);
                Assert.LessOrEqual(sinks, 1, $"Max 1 sink at level {level} in 20-99 band");
                if (sinks > 0) sinkCount++;
            }

            // 30% target → expect at least 10% and at most 60% (generous bounds for hash-based determinism)
            float ratio = sinkCount / (float)total;
            Assert.GreaterOrEqual(ratio, 0.10f, $"Too few sinks in 20-99: {sinkCount}/{total}");
            Assert.LessOrEqual(ratio, 0.60f, $"Too many sinks in 20-99: {sinkCount}/{total}");
        }

        [Test]
        public void DetermineSinkCount_DistributionBands_100To299()
        {
            int[] counts = new int[3]; // 0, 1, 2
            for (int level = 100; level <= 299; level++)
            {
                int sinks = LevelDifficultyEngine.DetermineSinkCount(level);
                Assert.LessOrEqual(sinks, 2, $"Max 2 sinks at level {level} in 100-299 band");
                counts[sinks]++;
            }

            int total = 200;
            // Verify each bucket has a non-trivial share
            Assert.Greater(counts[0], 0, "Expected some 0-sink levels in 100-299.");
            Assert.Greater(counts[1], 0, "Expected some 1-sink levels in 100-299.");
            Assert.Greater(counts[2], 0, "Expected some 2-sink levels in 100-299.");
            // 2-sink ratio should be meaningful (30% target; allow 10-60%)
            float twoSinkRatio = counts[2] / (float)total;
            Assert.GreaterOrEqual(twoSinkRatio, 0.10f, $"Too few 2-sink in 100-299: {counts[2]}/{total}");
            Assert.LessOrEqual(twoSinkRatio, 0.60f, $"Too many 2-sink in 100-299: {counts[2]}/{total}");
        }

        [Test]
        public void DetermineSinkCount_DistributionBands_1000Plus()
        {
            int maxSeen = 0;
            for (int level = 1000; level <= 2000; level++)
            {
                int sinks = LevelDifficultyEngine.DetermineSinkCount(level);
                Assert.LessOrEqual(sinks, 5, $"Max 5 sinks at level {level}");
                if (sinks > maxSeen) maxSeen = sinks;
            }

            // In 1001 levels at 1000+, with 5% → 5 sinks,
            // we should see at least one level with 5 sinks.
            Assert.AreEqual(5, maxSeen, "Expected max sink count of 5 in levels 1000-2000.");
        }

        [Test]
        public void DetermineSinkCount_BandBoundaries_ProduceDifferentMaximums()
        {
            int max20_99 = 0;
            for (int level = 20; level <= 99; level++)
            {
                int s = LevelDifficultyEngine.DetermineSinkCount(level);
                if (s > max20_99) max20_99 = s;
            }
            Assert.LessOrEqual(max20_99, 1, "Band 20-99 should have max 1 sink.");

            int max100_299 = 0;
            for (int level = 100; level <= 299; level++)
            {
                int s = LevelDifficultyEngine.DetermineSinkCount(level);
                if (s > max100_299) max100_299 = s;
            }
            Assert.LessOrEqual(max100_299, 2, "Band 100-299 should have max 2 sinks.");
            Assert.GreaterOrEqual(max100_299, 2, "Band 100-299 should reach 2 sinks.");
        }

        [Test]
        public void SinkRoleClass_HasApproximatelyEqualDistribution()
        {
            int requiredCount = 0;
            int totalWithSinks = 0;

            for (int level = 20; level <= 1000; level++)
            {
                if (LevelDifficultyEngine.DetermineSinkCount(level) > 0)
                {
                    totalWithSinks++;
                    if (LevelDifficultyEngine.IsSinkRequiredClass(level))
                    {
                        requiredCount++;
                    }
                }
            }

            Assert.Greater(totalWithSinks, 0, "Expected some levels with sinks.");
            float requiredRatio = requiredCount / (float)totalWithSinks;
            // Hash-based 50/50 split: expect between 30%-70%
            Assert.GreaterOrEqual(requiredRatio, 0.30f,
                $"Sink-required ratio too low: {requiredCount}/{totalWithSinks} ({requiredRatio:P0})");
            Assert.LessOrEqual(requiredRatio, 0.70f,
                $"Sink-required ratio too high: {requiredCount}/{totalWithSinks} ({requiredRatio:P0})");
        }
    }
}
