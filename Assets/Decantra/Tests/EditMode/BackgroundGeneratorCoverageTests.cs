/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Background;
using NUnit.Framework;

namespace Decantra.Domain.Tests
{
    [TestFixture]
    public sealed class BackgroundGeneratorCoverageTests
    {
        private const int TestWidth = 48;
        private const int TestHeight = 48;
        private const ulong SeedA = 0x12345678ABCDEF01;
        private const ulong SeedB = 0x0FEDCBA987654321;

        public static IEnumerable<GeneratorArchetype> RequiredArchetypes()
        {
            yield return GeneratorArchetype.BotanicalIFS;
            yield return GeneratorArchetype.BranchingTree;
            yield return GeneratorArchetype.CanopyDapple;
            yield return GeneratorArchetype.ConcentricRipples;
            yield return GeneratorArchetype.CrystallineFrost;
            yield return GeneratorArchetype.FloralMandala;
            yield return GeneratorArchetype.FractalEscapeDensity;
            yield return GeneratorArchetype.ImplicitBlobHaze;
            yield return GeneratorArchetype.MarbledFlow;
            yield return GeneratorArchetype.NebulaGlow;
            yield return GeneratorArchetype.OrganicCells;
            yield return GeneratorArchetype.RootNetwork;
            yield return GeneratorArchetype.VineTendrils;
        }

        [TestCaseSource(nameof(RequiredArchetypes))]
        public void Generators_AreDeterministic_ForDefaultAndMacro(GeneratorArchetype archetype)
        {
            var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);

            var defaultParameters = FieldParameters.Default;
            defaultParameters.IsMacroLayer = false;

            var macroParameters = FieldParameters.Macro;
            macroParameters.IsMacroLayer = true;

            var fieldA1 = generator.Generate(TestWidth, TestHeight, defaultParameters, SeedA);
            var fieldA2 = generator.Generate(TestWidth, TestHeight, defaultParameters, SeedA);
            AssertFieldsEqual(fieldA1, fieldA2, archetype, "default");
            AssertFieldValid(fieldA1, archetype, "default");

            var fieldM1 = generator.Generate(TestWidth, TestHeight, macroParameters, SeedA);
            var fieldM2 = generator.Generate(TestWidth, TestHeight, macroParameters, SeedA);
            AssertFieldsEqual(fieldM1, fieldM2, archetype, "macro");
            AssertFieldValid(fieldM1, archetype, "macro");
        }

        [TestCaseSource(nameof(RequiredArchetypes))]
        public void Generators_VaryAcrossSeeds(GeneratorArchetype archetype)
        {
            var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);
            var parameters = FieldParameters.Default;
            parameters.IsMacroLayer = false;

            var fieldA = generator.Generate(TestWidth, TestHeight, parameters, SeedA);
            var fieldB = generator.Generate(TestWidth, TestHeight, parameters, SeedB);

            AssertFieldValid(fieldA, archetype, "seedA");
            AssertFieldValid(fieldB, archetype, "seedB");

            float fingerprintA = ComputeFingerprint(fieldA);
            float fingerprintB = ComputeFingerprint(fieldB);
            float delta = Math.Abs(fingerprintA - fingerprintB);
            Assert.Greater(delta, 1e-4f,
                $"{archetype} should vary across seeds (fingerprint {fingerprintA} vs {fingerprintB}).");
        }

        private static void AssertFieldsEqual(float[] left, float[] right, GeneratorArchetype archetype, string label)
        {
            Assert.AreEqual(left.Length, right.Length, $"{archetype} {label} length mismatch.");
            for (int i = 0; i < left.Length; i++)
            {
                Assert.AreEqual(left[i], right[i], 1e-6f,
                    $"{archetype} {label} mismatch at index {i}.");
            }
        }

        private static void AssertFieldValid(float[] field, GeneratorArchetype archetype, string label)
        {
            Assert.IsNotNull(field, $"{archetype} {label} field is null.");
            Assert.AreEqual(TestWidth * TestHeight, field.Length, $"{archetype} {label} length mismatch.");

            float min = float.MaxValue;
            float max = float.MinValue;
            double sum = 0;

            for (int i = 0; i < field.Length; i++)
            {
                float value = field[i];
                Assert.GreaterOrEqual(value, 0f, $"{archetype} {label} value below 0 at {i}.");
                Assert.LessOrEqual(value, 1f, $"{archetype} {label} value above 1 at {i}.");
                if (value < min) min = value;
                if (value > max) max = value;
                sum += value;
            }

            float mean = (float)(sum / field.Length);
            Assert.Greater(max - min, 0.05f, $"{archetype} {label} lacks variance (min {min}, max {max}).");
            Assert.Greater(mean, 0.05f, $"{archetype} {label} mean too low ({mean}).");
            Assert.Less(mean, 0.95f, $"{archetype} {label} mean too high ({mean}).");
        }

        private static float ComputeFingerprint(float[] field)
        {
            double sum = 0;
            double sumSquares = 0;

            for (int i = 0; i < field.Length; i++)
            {
                float value = field[i];
                sum += value;
                sumSquares += value * value;
            }

            return (float)(sum + sumSquares * 0.13);
        }
    }
}
