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
    }
}
