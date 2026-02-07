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
        public void Registry_IncludesRegisteredButNonAllowedArchetypes()
        {
            var atmospheric = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.AtmosphericWash);
            var organic = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.OrganicCells);

            Assert.AreEqual(GeneratorArchetype.AtmosphericWash, atmospheric.Archetype);
            Assert.AreEqual(GeneratorArchetype.OrganicCells, organic.Archetype);
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
    }
}
