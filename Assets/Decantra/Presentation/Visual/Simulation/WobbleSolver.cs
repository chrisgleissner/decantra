/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Presentation.Visual.Simulation
{
    /// <summary>
    /// Deterministic damped harmonic oscillator implementing the liquid sloshing illusion.
    ///
    /// Physical model
    /// --------------
    /// The oscillator models a simple spring-damper system driven by external angular
    /// impulses (pours, user taps). The state is (displacement, velocity) and is integrated
    /// over time using a fixed-step semi-implicit Euler method to guarantee determinism
    /// regardless of frame rate.
    ///
    ///   a = -ω² · x  -  2 · ζ · ω · v  +  impulse/mass
    ///   v_new = v + a · dt
    ///   x_new = x + v_new · dt
    ///
    /// where ω = 2π·f₀ (natural frequency), ζ = damping ratio.
    ///
    /// The tilt of the liquid surface visible to the shader is:
    ///   tiltAngleDegrees = Clamp( x · TiltScale, -MaxTiltDegrees, MaxTiltDegrees )
    ///
    /// Determinism guarantees
    /// -----------------------
    /// • Fixed-step integration with a hardcoded dt (FixedDeltaTime) — never uses
    ///   Time.deltaTime or any non-deterministic input.
    /// • Accumulator carries sub-step remainder between frames so total impulse is constant.
    /// • Seeding/reset via Reset() restores the exact zero state.
    ///
    /// Usage
    /// -----
    ///   var solver = new WobbleSolver();
    ///   solver.ApplyImpulse(1.5f);   // angular velocity impulse (radians/s)
    ///   solver.Step(0.016f);          // advance by one frame's worth of time
    ///   float tilt = solver.TiltAngleDegrees;
    /// </summary>
    public sealed class WobbleSolver
    {
        // ── Configuration constants ────────────────────────────────────────────
        /// <summary>Natural frequency in Hz. Tuned for water-like sloshing.</summary>
        public const float NaturalFrequencyHz = 3.5f;

        /// <summary>
        /// Damping ratio ζ. 1.0 = critically damped, 0.3 = lightly damped (bouncy).
        /// 0.45 gives a pleasing single oscillation that decays in ~0.5 s.
        /// </summary>
        public const float DampingRatio = 0.45f;

        /// <summary>Fixed integration step size in seconds (60 Hz).</summary>
        public const float FixedDeltaTime = 1f / 60f;

        /// <summary>
        /// Maps oscillator displacement to visual tilt in the shader.
        /// Tuned so the gameplay-scale impulses used by Bottle3DView produce a clearly
        /// visible slosh instead of sub-degree motion that is effectively invisible.
        /// </summary>
        public const float TiltScale = 100f;

        /// <summary>Maximum tilt clamp (degrees). Prevents extreme visual distortion.</summary>
        public const float MaxTiltDegrees = 18f;

        /// <summary>
        /// Maximum slosh displacement (pre-scale). Hard clamp to prevent overflow.
        /// </summary>
        public const float MaxDisplacement = MaxTiltDegrees / TiltScale;

        // ── Derived constants (computed once) ─────────────────────────────────
        private static readonly float Omega = 2f * MathF.PI * NaturalFrequencyHz;
        private static readonly float OmegaSq = Omega * Omega;
        private static readonly float TwoDampingOmega = 2f * DampingRatio * Omega;

        // ── Oscillator state ───────────────────────────────────────────────────
        private float _displacement;   // x: current displacement in radians
        private float _velocity;       // v: current angular velocity in rad/s
        private float _accumulator;    // sub-step time accumulator

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Current surface tilt in degrees, clamped to ± <see cref="MaxTiltDegrees"/>.
        /// Positive = tilted right; negative = tilted left.
        /// Pass directly to the Liquid3D shader's _SurfaceTiltDegrees property.
        /// </summary>
        public float TiltAngleDegrees =>
            Math.Clamp(_displacement * TiltScale, -MaxTiltDegrees, MaxTiltDegrees);

        /// <summary>
        /// Raw displacement (radians). Use for energy/amplitude assertions in tests.
        /// </summary>
        public float Displacement => _displacement;

        /// <summary>
        /// Current angular velocity in rad/s.
        /// </summary>
        public float Velocity => _velocity;

        /// <summary>
        /// Returns true when the oscillator has effectively settled
        /// (|displacement| &lt; 0.0001 rad, |velocity| &lt; 0.0001 rad/s).
        /// </summary>
        public bool IsSettled =>
            MathF.Abs(_displacement) < 0.0001f && MathF.Abs(_velocity) < 0.0001f;

        /// <summary>
        /// Apply an angular velocity impulse (rad/s) to the oscillator.
        /// Call this when a pour starts, a bottle is selected, or the user taps.
        /// </summary>
        /// <param name="angularVelocityImpulse">Signed angular velocity in rad/s.
        /// Positive = push right, negative = push left.</param>
        public void ApplyImpulse(float angularVelocityImpulse)
        {
            _velocity += angularVelocityImpulse;
        }

        /// <summary>
        /// Advance the simulation by <paramref name="deltaTime"/> seconds using
        /// deterministic fixed-step integration. Call once per visual frame.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            _accumulator += deltaTime;

            while (_accumulator >= FixedDeltaTime)
            {
                Integrate(FixedDeltaTime);
                _accumulator -= FixedDeltaTime;
            }
        }

        /// <summary>
        /// Reset the oscillator to the zero-energy rest state.
        /// Call when switching levels or resetting the board.
        /// </summary>
        public void Reset()
        {
            _displacement = 0f;
            _velocity = 0f;
            _accumulator = 0f;
        }

        // ── Internal integration ───────────────────────────────────────────────

        private void Integrate(float dt)
        {
            // Semi-implicit Euler: compute acceleration, update velocity first, then position.
            float acceleration = -OmegaSq * _displacement - TwoDampingOmega * _velocity;
            _velocity += acceleration * dt;
            _displacement += _velocity * dt;

            // Hard clamp to prevent overflow under extreme impulse stacking
            _displacement = Math.Clamp(_displacement, -MaxDisplacement, MaxDisplacement);
        }
    }
}
