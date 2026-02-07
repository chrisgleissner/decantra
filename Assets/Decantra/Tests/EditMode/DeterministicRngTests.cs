/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Background;
using NUnit.Framework;

namespace Decantra.Domain.Tests
{
    [TestFixture]
    public sealed class DeterministicRngTests
    {
        [Test]
        public void NextInt_ReturnsMinWhenMaxNotGreater()
        {
            var rng = new DeterministicRng(42);
            Assert.AreEqual(5, rng.NextInt(5, 5));
            Assert.AreEqual(5, rng.NextInt(5, 4));
        }

        [Test]
        public void NextFloat_StaysWithinUnitRange()
        {
            var rng = new DeterministicRng(1234);
            for (int i = 0; i < 256; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void NextSignedFloat_RespectsMagnitude()
        {
            var rng = new DeterministicRng(98765);
            for (int i = 0; i < 128; i++)
            {
                float value = rng.NextSignedFloat(0.75f);
                Assert.GreaterOrEqual(value, -0.75f);
                Assert.LessOrEqual(value, 0.75f);
            }

            Assert.AreEqual(0f, rng.NextSignedFloat(0f), 1e-6f);
        }

        [Test]
        public void NextFloat_SequenceIsReproducible()
        {
            var rngA = new DeterministicRng(123456);
            var rngB = new DeterministicRng(123456);

            for (int i = 0; i < 256; i++)
            {
                Assert.AreEqual(rngA.NextFloat(), rngB.NextFloat(), 1e-7f, $"Mismatch at index {i}.");
            }
        }

        [Test]
        public void Noise2D_IsDeterministicAndBounded()
        {
            var rngA = new DeterministicRng(0xABCDEF01);
            var rngB = new DeterministicRng(0xABCDEF01);

            float valueA = rngA.Noise2D(1.25f, -0.75f);
            float valueB = rngB.Noise2D(1.25f, -0.75f);

            Assert.AreEqual(valueA, valueB, 1e-6f);
            Assert.GreaterOrEqual(valueA, 0f);
            Assert.LessOrEqual(valueA, 1f);
        }

        [Test]
        public void GradientNoise2D_IsDeterministicAndBounded()
        {
            var rngA = new DeterministicRng(0x0BADF00D);
            var rngB = new DeterministicRng(0x0BADF00D);

            float valueA = rngA.GradientNoise2D(2.5f, 3.25f);
            float valueB = rngB.GradientNoise2D(2.5f, 3.25f);

            Assert.AreEqual(valueA, valueB, 1e-6f);
            Assert.GreaterOrEqual(valueA, 0f);
            Assert.LessOrEqual(valueA, 1f);
        }

        [Test]
        public void FBm_IsDeterministicAndBounded()
        {
            var rngA = new DeterministicRng(0xCAFEBABE);
            var rngB = new DeterministicRng(0xCAFEBABE);

            float valueA = rngA.FBm(1.1f, 2.2f, 4, 2f, 0.5f);
            float valueB = rngB.FBm(1.1f, 2.2f, 4, 2f, 0.5f);

            Assert.AreEqual(valueA, valueB, 1e-6f);
            Assert.GreaterOrEqual(valueA, 0f);
            Assert.LessOrEqual(valueA, 1f);
        }
    }
}
