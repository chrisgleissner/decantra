/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Background;
using Decantra.Domain.Rules;
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
            var archetypes = new[]
            {
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection,
                GeneratorArchetype.AtmosphericWash
            };

            foreach (var archetype in archetypes)
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
        public void Registry_MapsLegacyFamiliesCorrectly()
        {
            // Test legacy mapping
            var fromLines = BackgroundGeneratorRegistry.MapLegacyToArchetype(GeneratorFamily.DirectionalLineFields);
            var fromVoronoi = BackgroundGeneratorRegistry.MapLegacyToArchetype(GeneratorFamily.VoronoiRegions);
            var fromWave = BackgroundGeneratorRegistry.MapLegacyToArchetype(GeneratorFamily.WaveInterference);

            Assert.AreEqual(GeneratorArchetype.AtmosphericWash, fromLines);
            Assert.AreEqual(GeneratorArchetype.DomainWarpedClouds, fromVoronoi);
            Assert.AreEqual(GeneratorArchetype.CurlFlowAdvection, fromWave);
        }

        [Test]
        public void Registry_SelectsZone0AsAtmosphericWash()
        {
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForZone(0, TestSeed);
            Assert.AreEqual(GeneratorArchetype.AtmosphericWash, archetype);
        }

        [Test]
        public void Registry_SelectsZone1AsDomainWarpedClouds()
        {
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForZone(1, TestSeed);
            Assert.AreEqual(GeneratorArchetype.DomainWarpedClouds, archetype);
        }

        [Test]
        public void Registry_SelectsZone2AsOrganicCells()
        {
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForZone(2, TestSeed);
            Assert.AreEqual(GeneratorArchetype.OrganicCells, archetype);
        }

        [Test]
        public void Registry_AdjacentZonesAreDifferent()
        {
            ulong seed = 0x123456789ABCDEF0;

            for (int zone = 1; zone < 10; zone++)
            {
                var prev = BackgroundGeneratorRegistry.SelectArchetypeForZone(zone - 1, seed);
                var curr = BackgroundGeneratorRegistry.SelectArchetypeForZone(zone, seed);

                Assert.AreNotEqual(prev, curr,
                    $"Zone {zone} should be different from zone {zone - 1}");
            }
        }

        [Test]
        public void Generators_ProduceNoCenterBias()
        {
            var archetypes = new[]
            {
                GeneratorArchetype.DomainWarpedClouds,
                GeneratorArchetype.CurlFlowAdvection,
                GeneratorArchetype.AtmosphericWash
            };

            foreach (var archetype in archetypes)
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
    }
}
