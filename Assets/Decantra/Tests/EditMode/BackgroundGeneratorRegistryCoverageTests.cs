/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Linq;
using Decantra.Domain.Background;
using NUnit.Framework;

namespace Decantra.Domain.Tests
{
    [TestFixture]
    public sealed class BackgroundGeneratorRegistryCoverageTests
    {
        [Test]
        public void Registry_ReturnsGeneratorsForAllAllowedArchetypes()
        {
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();
            Assert.Greater(allowed.Count, 0, "Allowed archetypes list should not be empty.");

            foreach (var archetype in allowed)
            {
                var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);
                Assert.IsNotNull(generator, $"Generator missing for {archetype}.");
                Assert.AreEqual(archetype, generator.Archetype, $"Generator archetype mismatch for {archetype}.");
            }
        }

        [Test]
        public void Registry_AllowedListContainsAllImplementedArchetypes()
        {
            // Every archetype that has a registered generator must be in AllowedArchetypesOrdered
            var allowed = new System.Collections.Generic.HashSet<GeneratorArchetype>(
                BackgroundGeneratorRegistry.GetAllowedArchetypes());
            var allValues = (GeneratorArchetype[])System.Enum.GetValues(typeof(GeneratorArchetype));

            foreach (var archetype in allValues)
            {
                if (BackgroundGeneratorRegistry.IsImplemented(archetype))
                {
                    Assert.IsTrue(allowed.Contains(archetype),
                        $"Implemented archetype {archetype} is missing from AllowedArchetypesOrdered (dead code).");
                }
            }
        }

        [Test]
        public void Registry_Has16AllowedArchetypes()
        {
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();
            Assert.AreEqual(16, allowed.Count, "Should have exactly 16 allowed archetypes.");
        }

        [Test]
        public void Registry_ImplementedArchetypesMatchAllowedList()
        {
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();
            var implemented = BackgroundGeneratorRegistry.GetImplementedArchetypes().ToArray();

            CollectionAssert.AreEqual(allowed, implemented, "Implemented archetypes should match allowed order.");
        }

        [Test]
        public void Registry_IsImplementedReflectsKnownAndUnknownArchetypes()
        {
            Assert.IsTrue(BackgroundGeneratorRegistry.IsImplemented(GeneratorArchetype.DomainWarpedClouds));
            Assert.IsTrue(BackgroundGeneratorRegistry.IsImplemented(GeneratorArchetype.CrystallineFrost));
            Assert.IsTrue(BackgroundGeneratorRegistry.IsImplemented(GeneratorArchetype.AtmosphericWash));
            Assert.IsTrue(BackgroundGeneratorRegistry.IsImplemented(GeneratorArchetype.OrganicCells));

            var invalid = (GeneratorArchetype)999;
            Assert.IsFalse(BackgroundGeneratorRegistry.IsImplemented(invalid));
        }

        [Test]
        public void Registry_ThrowsForUnknownArchetype()
        {
            var invalid = (GeneratorArchetype)999;
            var ex = Assert.Throws<ArgumentException>(() => BackgroundGeneratorRegistry.GetGenerator(invalid));
            StringAssert.Contains("not yet implemented", ex?.Message);
        }

        [Test]
        public void SelectArchetypeForLevel_SameWithinZone()
        {
            // All levels within a 10-level zone must return the same archetype
            int seed = 12345;
            for (int zone = 0; zone <= 20; zone++)
            {
                int firstLevel = zone == 0 ? 1 : 10 + (zone - 1) * 10;
                int lastLevel = zone == 0 ? 9 : firstLevel + 9;
                var expected = BackgroundGeneratorRegistry.SelectArchetypeForLevel(firstLevel, seed);

                for (int level = firstLevel; level <= lastLevel; level++)
                {
                    var actual = BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed);
                    Assert.AreEqual(expected, actual,
                        $"Level {level} (zone {zone}) should use {expected} but got {actual}");
                }
            }
        }

        [Test]
        public void SelectArchetypeForLevel_ChangesAtZoneBoundaries()
        {
            // No two consecutive zones may have the same archetype,
            // including across cycle boundaries
            int seed = 12345;
            int totalZones = BackgroundGeneratorRegistry.GetAllowedArchetypes().Count * 3;

            for (int zone = 0; zone < totalZones - 1; zone++)
            {
                int lastOfZone = zone == 0 ? 9 : 10 + (zone - 1) * 10 + 9;
                int firstOfNext = lastOfZone + 1;
                var prev = BackgroundGeneratorRegistry.SelectArchetypeForLevel(lastOfZone, seed);
                var next = BackgroundGeneratorRegistry.SelectArchetypeForLevel(firstOfNext, seed);
                Assert.AreNotEqual(prev, next,
                    $"Archetype should change at zone boundary {lastOfZone}->{firstOfNext} (zones {zone}->{zone + 1})");
            }
        }

        [Test]
        public void SelectArchetypeForLevel_AllArchetypesReachable()
        {
            // Over zone 0 (pinned) + one full shuffle cycle, every archetype must appear
            int seed = 12345;
            var seen = new System.Collections.Generic.HashSet<GeneratorArchetype>();
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();

            for (int zone = 0; zone <= allowed.Count; zone++)
            {
                int level = zone == 0 ? 1 : 10 + (zone - 1) * 10;
                seen.Add(BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed));
            }

            Assert.AreEqual(allowed.Count, seen.Count,
                $"All {allowed.Count} archetypes should be reachable within one cycle. Only {seen.Count} found.");
        }

        [Test]
        public void SelectArchetypeForLevel_Zone0AlwaysDomainWarpedClouds()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var archetype = BackgroundGeneratorRegistry.SelectArchetypeForLevel(1, seed);
                Assert.AreEqual(GeneratorArchetype.DomainWarpedClouds, archetype,
                    $"Zone 0 must always be DomainWarpedClouds (seed {seed})");
            }
        }

        [Test]
        public void SelectArchetypeForLevel_DeterministicForSameSeed()
        {
            int seed = 54321;
            for (int level = 1; level <= 200; level++)
            {
                var first = BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed);
                var second = BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed);
                Assert.AreEqual(first, second, $"Level {level} archetype not deterministic");
            }
        }

        [Test]
        public void SelectArchetypeForLevel_DifferentSeedsProduceDifferentOrderings()
        {
            // Zone 0 is pinned, so test zone 1 (level 10) across different seeds
            var seedResults = new System.Collections.Generic.HashSet<GeneratorArchetype>();
            for (int seed = 0; seed < 100; seed++)
            {
                seedResults.Add(BackgroundGeneratorRegistry.SelectArchetypeForLevel(10, seed));
            }
            Assert.GreaterOrEqual(seedResults.Count, 2,
                "Different seeds should yield different archetype orderings");
        }
    }
}
