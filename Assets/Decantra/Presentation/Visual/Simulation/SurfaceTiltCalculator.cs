/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Presentation.Visual.Simulation
{
    /// <summary>
    /// Deterministically computes liquid surface tilt from bottle Z rotation.
    /// The equivalent bottle-local gravity vector is:
    ///   gLocalX = -sin(z)
    ///   gLocalY = -cos(z)
    /// and tilt is atan2(gLocalX, -gLocalY), then clamped.
    /// </summary>
    public static class SurfaceTiltCalculator
    {
        /// <summary>
        /// Returns the tilt angle (degrees) of the liquid surface in bottle-local coordinates.
        /// A positive value tilts toward +X; a negative value tilts toward -X.
        /// </summary>
        public static float ComputeTiltDegrees(float bottleZDegrees, float maxTiltDegrees)
        {
            float signedZ = NormalizeSignedDegrees(bottleZDegrees);
            float radians = signedZ * DegreesToRadians;

            // Gravity in bottle-local coordinates (assuming world gravity points -Y):
            // gLocalX = -sin(z), gLocalY = -cos(z)
            float gLocalX = -Sin(radians);
            float gLocalY = -Cos(radians);

            // Surface normal aligns opposite local gravity. The surface angle relative to
            // bottle-local horizontal is therefore atan2(gLocalX, -gLocalY).
            float tilt = Atan2(gLocalX, -gLocalY) * RadiansToDegrees;
            float max = Abs(maxTiltDegrees);
            if (tilt > max) return max;
            if (tilt < -max) return -max;
            return tilt;
        }

        private const float DegreesToRadians = 0.017453292519943295f;
        private const float RadiansToDegrees = 57.29577951308232f;

        private static float NormalizeSignedDegrees(float degrees)
        {
            float wrapped = degrees % 360f;
            if (wrapped > 180f)
            {
                wrapped -= 360f;
            }
            else if (wrapped < -180f)
            {
                wrapped += 360f;
            }
            return wrapped;
        }

        private static float Abs(float value)
        {
            return value >= 0f ? value : -value;
        }

        private static float Sin(float radians)
        {
            return (float)System.Math.Sin(radians);
        }

        private static float Cos(float radians)
        {
            return (float)System.Math.Cos(radians);
        }

        private static float Atan2(float y, float x)
        {
            return (float)System.Math.Atan2(y, x);
        }
    }
}
