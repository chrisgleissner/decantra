/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Presentation.Visual.Simulation;
using NUnit.Framework;

namespace Decantra.Presentation.Visual.Tests
{
    [TestFixture]
    public sealed class SurfaceTiltCalculatorTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void UprightBottle_ZeroTilt()
        {
            float tilt = SurfaceTiltCalculator.ComputeTiltDegrees(0f, 18f);
            Assert.AreEqual(0f, tilt, Tol);
        }

        [Test]
        public void PositiveBottleRotation_TiltOpposesRotation()
        {
            float tilt = SurfaceTiltCalculator.ComputeTiltDegrees(30f, 18f);
            Assert.AreEqual(-18f, tilt, Tol, "Tilt is clamped to max and should oppose bottle rotation.");
        }

        [Test]
        public void NegativeBottleRotation_TiltOpposesRotation()
        {
            float tilt = SurfaceTiltCalculator.ComputeTiltDegrees(-12f, 18f);
            Assert.AreEqual(12f, tilt, Tol);
        }

        [Test]
        public void WrappedDegrees_AreNormalized()
        {
            float tilt = SurfaceTiltCalculator.ComputeTiltDegrees(390f, 18f);
            Assert.AreEqual(-18f, tilt, Tol);
        }

        [Test]
        public void ClampHonored_ForLargeRotation()
        {
            float tilt = SurfaceTiltCalculator.ComputeTiltDegrees(80f, 10f);
            Assert.AreEqual(-10f, tilt, Tol);
        }

        [Test]
        public void SmallAngles_AreAntiSymmetric()
        {
            float plus = SurfaceTiltCalculator.ComputeTiltDegrees(7f, 18f);
            float minus = SurfaceTiltCalculator.ComputeTiltDegrees(-7f, 18f);
            Assert.AreEqual(-plus, minus, Tol);
            Assert.AreEqual(-7f, plus, Tol);
        }

        [Test]
        public void NearFullTurn_WrapAndClampRemainDeterministic()
        {
            float tiltA = SurfaceTiltCalculator.ComputeTiltDegrees(725f, 12f);
            float tiltB = SurfaceTiltCalculator.ComputeTiltDegrees(5f, 12f);
            Assert.AreEqual(tiltB, tiltA, Tol);
            Assert.AreEqual(-5f, tiltA, Tol);
        }
    }
}
