/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.IO;
using System.Linq;
using Decantra.Domain.Background;
using NUnit.Framework;

namespace Decantra.Domain.Tests
{
    /// <summary>
    /// Tests for the organic background generation system.
    /// </summary>
    [TestFixture]
    public sealed class BackgroundGeneratorTests
    {
        private const int TestWidth = 64;
        private const int TestHeight = 64;
        private const ulong TestSeed = 0xDEADBEEF12345678;

        [Test]
        public void DomainWarpedClouds_GeneratesValidField()
        {
            var generator = new DomainWarpedCloudsGenerator();
            var parameters = FieldParameters.Default;

            var field = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);

            Assert.IsNotNull(field);
            Assert.AreEqual(TestWidth * TestHeight, field.Length);
            AssertFieldInRange(field);
            AssertNotAllSameValue(field);
        }

        [Test]
        public void CurlFlowAdvection_GeneratesValidField()
        {
            var generator = new CurlFlowAdvectionGenerator();
            var parameters = FieldParameters.Default;

            var field = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);

            Assert.IsNotNull(field);
            Assert.AreEqual(TestWidth * TestHeight, field.Length);
            AssertFieldInRange(field);
            AssertNotAllSameValue(field);
        }

        [Test]
        public void AtmosphericWash_GeneratesValidField()
        {
            var generator = new AtmosphericWashGenerator();
            var parameters = FieldParameters.Default;

            var field = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);

            Assert.IsNotNull(field);
            Assert.AreEqual(TestWidth * TestHeight, field.Length);
            AssertFieldInRange(field);
            AssertNotAllSameValue(field);
        }

        [Test]
        public void AllGenerators_AreDeterministic()
        {
            foreach (var archetype in GetNoCenterBiasArchetypes())
            {
                var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);
                var parameters = FieldParameters.Default;

                var field1 = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);
                var field2 = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);

                Assert.AreEqual(field1.Length, field2.Length, $"Field lengths differ for {archetype}");

                for (int i = 0; i < field1.Length; i++)
                {
                    Assert.AreEqual(field1[i], field2[i], 0.0001f,
                        $"Field values differ at index {i} for {archetype}");
                }
            }
        }

        [Test]
        public void Registry_ReturnsCorrectGenerators()
        {
            var clouds = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.DomainWarpedClouds);
            var curl = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.CurlFlowAdvection);
            var wash = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.AtmosphericWash);

            Assert.AreEqual(GeneratorArchetype.DomainWarpedClouds, clouds.Archetype);
            Assert.AreEqual(GeneratorArchetype.CurlFlowAdvection, curl.Archetype);
            Assert.AreEqual(GeneratorArchetype.AtmosphericWash, wash.Archetype);
        }

        [Test]
        public void Registry_SameArchetypeWithinZone()
        {
            int seed = unchecked((int)TestSeed);
            // Zone 0: levels 1-9, Zone 1: 10-19, Zone 2: 20-29 ...
            for (int zone = 0; zone <= 20; zone++)
            {
                int first = zone == 0 ? 1 : 10 + (zone - 1) * 10;
                int last = zone == 0 ? 9 : first + 9;
                var expected = BackgroundGeneratorRegistry.SelectArchetypeForLevel(first, seed);
                for (int level = first; level <= last; level++)
                {
                    var actual = BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed);
                    Assert.AreEqual(expected, actual,
                        $"Level {level} (zone {zone}) should use {expected} but got {actual}");
                }
            }
        }

        [Test]
        public void Registry_ArchetypeChangesAtEveryZoneBoundary()
        {
            int seed = unchecked((int)TestSeed);
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();
            // Check across 3 full cycles to cover cross-cycle boundaries
            int totalZones = allowed.Count * 3;
            for (int zone = 0; zone < totalZones - 1; zone++)
            {
                int lastOfZone = zone == 0 ? 9 : 10 + (zone - 1) * 10 + 9;
                int firstOfNext = lastOfZone + 1;
                var prev = BackgroundGeneratorRegistry.SelectArchetypeForLevel(lastOfZone, seed);
                var next = BackgroundGeneratorRegistry.SelectArchetypeForLevel(firstOfNext, seed);
                Assert.AreNotEqual(prev, next,
                    $"Archetype should change at boundary {lastOfZone}->{firstOfNext} (zones {zone}->{zone + 1})");
            }
        }

        [Test]
        public void Registry_AllArchetypesReachableInOneCycle()
        {
            int seed = unchecked((int)TestSeed);
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes();
            var seen = new System.Collections.Generic.HashSet<GeneratorArchetype>();
            // Zone 0 is pinned; zones 1-16 are the first full shuffle cycle
            for (int zone = 0; zone <= allowed.Count; zone++)
            {
                int level = zone == 0 ? 1 : 10 + (zone - 1) * 10;
                seen.Add(BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed));
            }
            Assert.AreEqual(allowed.Count, seen.Count,
                $"All {allowed.Count} archetypes should be reachable in one cycle");
        }

        [Test]
        public void Registry_Zone0AlwaysDomainWarpedClouds()
        {
            // Zone 0 (levels 1-9) must always be DomainWarpedClouds regardless of seed
            for (int seed = 0; seed < 100; seed++)
            {
                for (int level = 1; level <= 9; level++)
                {
                    var archetype = BackgroundGeneratorRegistry.SelectArchetypeForLevel(level, seed);
                    Assert.AreEqual(GeneratorArchetype.DomainWarpedClouds, archetype,
                        $"Level {level} (seed {seed}) should always be DomainWarpedClouds");
                }
            }
        }

        [Test]
        public void AllowedArchetypes_MatchBackgroundSampleFilenames()
        {
            var allowed = BackgroundGeneratorRegistry.GetAllowedArchetypes()
                .Select(a => a.ToString())
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            string samplesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "doc", "img", "background-samples"));
            Assert.IsTrue(Directory.Exists(samplesPath), $"Background samples directory missing: {samplesPath}");

            var fileNames = Directory.GetFiles(samplesPath, "*_zone*.png")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Split('_')[0])
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            CollectionAssert.IsSubsetOf(allowed, fileNames);
        }

        [Test]
        public void Generators_ProduceNoCenterBias()
        {
            foreach (var archetype in GetNoCenterBiasArchetypes())
            {
                var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);
                var parameters = FieldParameters.Macro;
                parameters.IsMacroLayer = true;

                var field = generator.Generate(TestWidth, TestHeight, parameters, TestSeed);

                float centerAvg = ComputeRegionAverage(field, TestWidth, TestHeight, true);
                float edgeAvg = ComputeRegionAverage(field, TestWidth, TestHeight, false);

                if (edgeAvg > 0.01f)
                {
                    float ratio = centerAvg / edgeAvg;
                    Assert.Less(ratio, 1.25f,
                        $"{archetype} has center bias: center={centerAvg:F3}, edge={edgeAvg:F3}, ratio={ratio:F2}");
                }
            }
        }

        private void AssertFieldInRange(float[] field)
        {
            for (int i = 0; i < field.Length; i++)
            {
                Assert.GreaterOrEqual(field[i], 0f, $"Value at {i} is below 0");
                Assert.LessOrEqual(field[i], 1f, $"Value at {i} is above 1");
            }
        }

        private void AssertNotAllSameValue(float[] field)
        {
            float first = field[0];
            bool allSame = true;

            for (int i = 1; i < field.Length && allSame; i++)
            {
                if (System.Math.Abs(field[i] - first) > 0.001f)
                {
                    allSame = false;
                }
            }

            Assert.IsFalse(allSame, "Field should not have all same values");
        }

        private float ComputeRegionAverage(float[] field, int width, int height, bool isCenter)
        {
            int regionSize = System.Math.Max(2, (int)(System.Math.Min(width, height) * 0.2f));
            float sum = 0f;
            int count = 0;

            if (isCenter)
            {
                int startX = (width - regionSize) / 2;
                int startY = (height - regionSize) / 2;

                for (int y = startY; y < startY + regionSize; y++)
                {
                    for (int x = startX; x < startX + regionSize; x++)
                    {
                        sum += field[y * width + x];
                        count++;
                    }
                }
            }
            else
            {
                // Sample corners
                for (int y = 0; y < regionSize; y++)
                {
                    for (int x = 0; x < regionSize; x++)
                    {
                        sum += field[y * width + x]; // Top-left
                        sum += field[y * width + (width - 1 - x)]; // Top-right
                        sum += field[(height - 1 - y) * width + x]; // Bottom-left
                        sum += field[(height - 1 - y) * width + (width - 1 - x)]; // Bottom-right
                        count += 4;
                    }
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        private static GeneratorArchetype[] GetAllPrimaryArchetypes()
        {
            return new[]
            {
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection,
                GeneratorArchetype.AtmosphericWash,
                GeneratorArchetype.FractalEscapeDensity,
                GeneratorArchetype.BotanicalIFS,
                GeneratorArchetype.ImplicitBlobHaze,
                GeneratorArchetype.MarbledFlow,
                GeneratorArchetype.ConcentricRipples,
                GeneratorArchetype.NebulaGlow,
                GeneratorArchetype.OrganicCells,
                GeneratorArchetype.CrystallineFrost,
                GeneratorArchetype.BranchingTree,
                GeneratorArchetype.VineTendrils,
                GeneratorArchetype.RootNetwork,
                GeneratorArchetype.CanopyDapple,
                GeneratorArchetype.FloralMandala
            };
        }

        private static GeneratorArchetype[] GetNoCenterBiasArchetypes()
        {
            return new[]
            {
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection,
                GeneratorArchetype.AtmosphericWash
            };
        }
    }
}
