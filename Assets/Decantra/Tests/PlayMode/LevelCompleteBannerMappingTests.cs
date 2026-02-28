/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Presentation;
using NUnit.Framework;

namespace Decantra.Tests.PlayMode
{
    public sealed class LevelCompleteBannerMappingTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        public void StarTierMapping_IsCorrect(int stars, int expectedTier)
        {
            Assert.AreEqual(expectedTier, LevelCompleteBanner.ResolveTierFromStars(stars));
        }

        [Test]
        public void StyleRotation_IsDeterministic()
        {
            Assert.AreEqual(1, LevelCompleteBanner.ResolveStyleIndex(1));
            Assert.AreEqual(2, LevelCompleteBanner.ResolveStyleIndex(2));
            Assert.AreEqual(3, LevelCompleteBanner.ResolveStyleIndex(3));
            Assert.AreEqual(0, LevelCompleteBanner.ResolveStyleIndex(4));
            Assert.AreEqual(1, LevelCompleteBanner.ResolveStyleIndex(5));
        }

        // ── Phase architecture tests ──

        [TestCase(0, 0f)]
        [TestCase(1, 0f)]
        [TestCase(2, 0f)]
        [TestCase(3, 0f)]
        [TestCase(4, 0f)]
        [TestCase(5, 0.15f)]
        public void CelebrationProfile_FreezeSeconds_ScalesWithTier(int stars, float expectedFreeze)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.AreEqual(expectedFreeze, profile.FreezeSeconds, 0.001f);
        }

        [TestCase(0, 1.01f)]
        [TestCase(2, 1.02f)]
        [TestCase(4, 1.03f)]
        [TestCase(5, 1.05f)]
        public void CelebrationProfile_PulseScale_ScalesWithTier(int stars, float expectedPulse)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.AreEqual(expectedPulse, profile.PulseScale, 0.001f);
        }

        // ── Gold tint only for tier 3 (5 stars) ──

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        public void GoldTint_OnlyForTier3(int stars, bool expectedGoldTint)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.AreEqual(expectedGoldTint, profile.GoldTint);
        }

        // ── Wave thickness scales with tier ──

        [Test]
        public void WaveThickness_ScalesWithTier()
        {
            var tier0 = LevelCompleteBanner.BuildCelebrationProfile(0, 0);
            var tier1 = LevelCompleteBanner.BuildCelebrationProfile(2, 0);
            var tier2 = LevelCompleteBanner.BuildCelebrationProfile(4, 0);
            var tier3 = LevelCompleteBanner.BuildCelebrationProfile(5, 0);

            Assert.Less(tier0.WaveThicknessScale, tier1.WaveThicknessScale);
            Assert.Less(tier1.WaveThicknessScale, tier2.WaveThicknessScale);
            Assert.Less(tier2.WaveThicknessScale, tier3.WaveThicknessScale);
            Assert.AreEqual(1f, tier0.WaveThicknessScale, 0.001f);
            Assert.AreEqual(1.3f, tier3.WaveThicknessScale, 0.001f);
        }

        // ── Sequential star reveal stagger ──

        [TestCase(0, 0.04f)]
        [TestCase(2, 0.05f)]
        [TestCase(4, 0.06f)]
        [TestCase(5, 0.06f)]
        public void StarRevealStagger_IsPositiveAndScaled(int stars, float expectedStagger)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.Greater(profile.StarRevealStaggerSeconds, 0f);
            Assert.AreEqual(expectedStagger, profile.StarRevealStaggerSeconds, 0.001f);
        }

        // ── Streak milestone boosts ──

        [Test]
        public void StreakMilestone_BoostsCelebration()
        {
            var noStreak = LevelCompleteBanner.BuildCelebrationProfile(5, 0);
            var withStreak = LevelCompleteBanner.BuildCelebrationProfile(5, 5);

            Assert.Greater(withStreak.PulseScale, noStreak.PulseScale);
            Assert.Greater(withStreak.SparkleDensity, noStreak.SparkleDensity);
        }

        [Test]
        public void StreakMilestone_OnlyAffectsTier3()
        {
            var noStreakTier0 = LevelCompleteBanner.BuildCelebrationProfile(0, 0);
            var withStreakTier0 = LevelCompleteBanner.BuildCelebrationProfile(0, 5);
            Assert.AreEqual(noStreakTier0.PulseScale, withStreakTier0.PulseScale, 0.001f);

            var noStreakTier1 = LevelCompleteBanner.BuildCelebrationProfile(2, 0);
            var withStreakTier1 = LevelCompleteBanner.BuildCelebrationProfile(2, 5);
            Assert.AreEqual(noStreakTier1.PulseScale, withStreakTier1.PulseScale, 0.001f);
        }

        // ── Vignette bump scales with tier ──

        [Test]
        public void VignetteBump_IncreasesWithTier()
        {
            var tier0 = LevelCompleteBanner.BuildCelebrationProfile(0, 0);
            var tier1 = LevelCompleteBanner.BuildCelebrationProfile(2, 0);
            var tier2 = LevelCompleteBanner.BuildCelebrationProfile(4, 0);
            var tier3 = LevelCompleteBanner.BuildCelebrationProfile(5, 0);

            Assert.Greater(tier0.VignetteBump, 0f);
            Assert.Less(tier0.VignetteBump, tier1.VignetteBump);
            Assert.Less(tier1.VignetteBump, tier2.VignetteBump);
            Assert.Less(tier2.VignetteBump, tier3.VignetteBump);
        }

        // ── Emission scale increases monotonically ──

        [Test]
        public void EmissionScale_IncreasesWithTier()
        {
            var tier0 = LevelCompleteBanner.BuildCelebrationProfile(0, 0);
            var tier1 = LevelCompleteBanner.BuildCelebrationProfile(2, 0);
            var tier2 = LevelCompleteBanner.BuildCelebrationProfile(4, 0);
            var tier3 = LevelCompleteBanner.BuildCelebrationProfile(5, 0);

            Assert.Less(tier0.EmissionScale, tier1.EmissionScale);
            Assert.Less(tier1.EmissionScale, tier2.EmissionScale);
            Assert.Less(tier2.EmissionScale, tier3.EmissionScale);
        }

        // ── MultiBurst only for tier 3 ──

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        public void MultiBurst_OnlyForTier3(int stars, bool expected)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.AreEqual(expected, profile.MultiPhaseBurst);
        }

        // ── Shimmer not at tier 0 ──

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        public void Shimmer_NotAtTier0(int stars, bool expected)
        {
            var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, 0);
            Assert.AreEqual(expected, profile.Shimmer);
        }

        // ── Sparkle density respects caps ──

        [Test]
        public void SparkleDensity_NeverExceedsCap()
        {
            for (int stars = 0; stars <= 5; stars++)
            {
                for (int milestone = 0; milestone <= 10; milestone += 5)
                {
                    var profile = LevelCompleteBanner.BuildCelebrationProfile(stars, milestone);
                    Assert.LessOrEqual(profile.SparkleDensity, 2f,
                        $"SparkleDensity exceeds cap for stars={stars} milestone={milestone}");
                }
            }
        }
    }
}
