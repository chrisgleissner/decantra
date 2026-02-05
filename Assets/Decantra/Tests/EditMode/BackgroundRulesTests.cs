/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Rules;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class BackgroundRulesTests
    {
        [Test]
        public void ZoneIndex_ReturnsCorrectBoundaries()
        {
            Assert.AreEqual(0, BackgroundRules.GetZoneIndex(1));
            Assert.AreEqual(0, BackgroundRules.GetZoneIndex(9));
            Assert.AreEqual(1, BackgroundRules.GetZoneIndex(10));
            Assert.AreEqual(1, BackgroundRules.GetZoneIndex(19));
            Assert.AreEqual(2, BackgroundRules.GetZoneIndex(20));
        }

        [Test]
        public void ZoneIndex_ConsecutiveGroupsMatchSpecification()
        {
            for (int zone = 1; zone <= 100; zone++)
            {
                int firstLevel = 10 + (zone - 1) * 10;
                int lastLevel = firstLevel + 9;

                for (int level = firstLevel; level <= lastLevel; level++)
                {
                    Assert.AreEqual(zone, BackgroundRules.GetZoneIndex(level),
                        $"Level {level} should be in Zone {zone}");
                }
            }
        }

        [Test]
        public void LevelVariant_IsDeterministicForSameLevel()
        {
            int seed = 2222;
            for (int level = 1; level <= 100; level++)
            {
                var first = BackgroundRules.GetLevelVariant(level, seed, 6);
                var second = BackgroundRules.GetLevelVariant(level, seed, 6);
                Assert.AreEqual(first.PaletteIndex, second.PaletteIndex, $"Level {level} palette not deterministic");
                Assert.AreEqual(first.HueShift, second.HueShift, $"Level {level} hueShift not deterministic");
                Assert.AreEqual(first.PhaseOffset, second.PhaseOffset, $"Level {level} phase not deterministic");
            }
        }

        [Test]
        public void LevelVariant_VariesWithinZone()
        {
            int seed = 3333;
            var paletteIndices = new HashSet<int>();
            for (int level = 10; level <= 19; level++)
            {
                var variant = BackgroundRules.GetLevelVariant(level, seed, 6);
                paletteIndices.Add(variant.PaletteIndex);
            }
            Assert.GreaterOrEqual(paletteIndices.Count, 2, "Level variants should vary within a Zone");
        }
    }
}
