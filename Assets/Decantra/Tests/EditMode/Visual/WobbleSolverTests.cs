/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using NUnit.Framework;
using Decantra.Presentation.Visual.Simulation;

namespace Decantra.Presentation.Visual.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="WobbleSolver"/>.
    ///
    /// Validates:
    ///   1. Zero-energy state at construction.
    ///   2. Impulse increases displacement over time.
    ///   3. Oscillator decays to near-zero within expected time.
    ///   4. Tilt angle is always clamped to ± MaxTiltDegrees.
    ///   5. Determinism: identical inputs produce identical outputs.
    ///   6. Negative impulse produces negative displacement.
    ///   7. IsSettled gates correctly.
    ///   8. Reset restores zero state exactly.
    /// </summary>
    [TestFixture]
    public sealed class WobbleSolverTests
    {
        private WobbleSolver _solver;

        [SetUp]
        public void SetUp() => _solver = new WobbleSolver();

        // ── 1. Zero state at construction ────────────────────────────────────
        [Test]
        public void AtConstruction_DisplacementAndVelocityAreZero()
        {
            Assert.AreEqual(0f, _solver.Displacement, 1e-6f, "Initial displacement must be 0");
            Assert.AreEqual(0f, _solver.Velocity, 1e-6f, "Initial velocity must be 0");
        }

        [Test]
        public void AtConstruction_TiltAngleIsZero()
        {
            Assert.AreEqual(0f, _solver.TiltAngleDegrees, 1e-6f, "Initial tilt must be 0°");
        }

        [Test]
        public void AtConstruction_IsSettled()
        {
            Assert.IsTrue(_solver.IsSettled, "Solver must start settled");
        }

        // ── 2. Impulse + step produce non-zero displacement ───────────────────
        [Test]
        public void AfterImpulseAndStep_DisplacementIsNonZero()
        {
            _solver.ApplyImpulse(2f);
            _solver.Step(WobbleSolver.FixedDeltaTime);

            Assert.Greater(Math.Abs(_solver.Displacement), 1e-4f,
                "Displacement should be non-zero after impulse + step");
        }

        // ── 3. Energy decays to near-zero ─────────────────────────────────────
        [Test]
        public void AfterSufficientTime_OscillatorDecays()
        {
            _solver.ApplyImpulse(2f);

            // Advance 4 seconds at 60 fps — well past decay envelope for ζ=0.45, f=3.5 Hz
            const float decayTime = 4f;
            const float dt = WobbleSolver.FixedDeltaTime;
            int steps = (int)(decayTime / dt);
            for (int i = 0; i < steps; i++)
                _solver.Step(dt);

            Assert.Less(Math.Abs(_solver.Displacement), 0.01f,
                $"Displacement {_solver.Displacement:F5} should be < 0.01 after {decayTime}s decay");
            Assert.Less(Math.Abs(_solver.Velocity), 0.01f,
                $"Velocity {_solver.Velocity:F5} should be < 0.01 after {decayTime}s decay");
        }

        // ── 4. Tilt is always clamped ─────────────────────────────────────────
        [Test]
        public void TiltAngle_NeverExceedsMaxTiltDegrees()
        {
            // Massive impulse that would exceed clamp
            _solver.ApplyImpulse(1000f);
            _solver.Step(WobbleSolver.FixedDeltaTime);

            float tilt = _solver.TiltAngleDegrees;
            Assert.LessOrEqual(tilt, WobbleSolver.MaxTiltDegrees + 1e-4f,
                $"Tilt {tilt:F2}° should not exceed {WobbleSolver.MaxTiltDegrees}°");
            Assert.GreaterOrEqual(tilt, -WobbleSolver.MaxTiltDegrees - 1e-4f,
                $"Tilt {tilt:F2}° should not go below -{WobbleSolver.MaxTiltDegrees}°");
        }

        [Test]
        public void RepeatedImpulses_DisplacementRemainsWithinMaxBound()
        {
            for (int i = 0; i < 120; i++)
            {
                _solver.ApplyImpulse(8f);
                _solver.Step(WobbleSolver.FixedDeltaTime);
            }

            Assert.LessOrEqual(Math.Abs(_solver.Displacement), WobbleSolver.MaxDisplacement + 1e-6f,
                "Displacement must remain clamped to MaxDisplacement under repeated impulses");
        }

        [Test]
        public void NegativeImpulse_ProducesNegativeTilt()
        {
            _solver.ApplyImpulse(-2f);
            _solver.Step(WobbleSolver.FixedDeltaTime * 3);

            Assert.Less(_solver.Displacement, 0f,
                "Negative impulse should produce negative displacement");
            Assert.Less(_solver.TiltAngleDegrees, 0f,
                "Negative impulse should produce negative tilt");
        }

        // ── 5. Determinism ─────────────────────────────────────────────────────
        [Test]
        public void Determinism_IdenticalInputsProduceIdenticalOutputs()
        {
            const float impulse = 1.7f;
            const int frames = 30;
            const float frameTime = 0.016f;

            var solverA = new WobbleSolver();
            var solverB = new WobbleSolver();

            for (int f = 0; f < frames; f++)
            {
                if (f == 5)
                {
                    solverA.ApplyImpulse(impulse);
                    solverB.ApplyImpulse(impulse);
                }
                float dt = frameTime;
                solverA.Step(dt);
                solverB.Step(dt);
            }

            Assert.AreEqual(solverA.Displacement, solverB.Displacement, 1e-7f,
                "Displacement must be identical for identical sequences");
            Assert.AreEqual(solverA.Velocity, solverB.Velocity, 1e-7f,
                "Velocity must be identical for identical sequences");
        }

        // ── 6. IsSettled ──────────────────────────────────────────────────────
        [Test]
        public void IsSettled_FalseAfterImpulse()
        {
            _solver.ApplyImpulse(2f);
            _solver.Step(WobbleSolver.FixedDeltaTime);

            Assert.IsFalse(_solver.IsSettled,
                "Solver should not be settled immediately after impulse");
        }

        // ── 7. Reset ──────────────────────────────────────────────────────────
        [Test]
        public void Reset_RestoresExactZeroState()
        {
            _solver.ApplyImpulse(3f);
            for (int i = 0; i < 10; i++) _solver.Step(WobbleSolver.FixedDeltaTime);

            _solver.Reset();

            Assert.AreEqual(0f, _solver.Displacement, 0f, "Displacement must be exactly 0 after Reset");
            Assert.AreEqual(0f, _solver.Velocity, 0f, "Velocity must be exactly 0 after Reset");
            Assert.IsTrue(_solver.IsSettled, "Solver must be settled after Reset");
        }

        // ── 8. Step(0) is a no-op ─────────────────────────────────────────────
        [Test]
        public void Step_ZeroDeltaTime_IsNoOp()
        {
            _solver.ApplyImpulse(1f);
            float dispBefore = _solver.Displacement;
            float velBefore = _solver.Velocity;

            _solver.Step(0f);

            Assert.AreEqual(dispBefore, _solver.Displacement, 1e-9f, "Step(0) must not change displacement");
            Assert.AreEqual(velBefore, _solver.Velocity, 1e-9f, "Step(0) must not change velocity");
        }
    }
}
