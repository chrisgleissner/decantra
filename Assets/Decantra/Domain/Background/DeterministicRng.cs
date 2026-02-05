/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

namespace Decantra.Domain.Background
{
    /// <summary>
    /// Deterministic pseudo-random number generator for background generation.
    /// Uses SplitMix64 algorithm for high-quality, reproducible random sequences.
    /// </summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        /// <summary>
        /// Creates a new RNG with the specified seed.
        /// </summary>
        public DeterministicRng(ulong seed)
        {
            _state = SplitMix64(seed);
            if (_state == 0)
            {
                _state = 0x6E624EB7CAFEBABE;
            }
        }

        /// <summary>
        /// Returns the next float in [0, 1).
        /// </summary>
        public float NextFloat()
        {
            return (NextUInt64() & 0x00FFFFFF) / 16777216f;
        }

        /// <summary>
        /// Returns the next float in [-magnitude, magnitude].
        /// </summary>
        public float NextSignedFloat(float magnitude)
        {
            return (NextFloat() * 2f - 1f) * magnitude;
        }

        /// <summary>
        /// Returns the next integer in [min, max).
        /// </summary>
        public int NextInt(int min, int max)
        {
            if (max <= min) return min;
            ulong span = (ulong)(max - min);
            return min + (int)(NextUInt64() % span);
        }

        /// <summary>
        /// Returns a 2D noise value for the given coordinates.
        /// Uses a simple but deterministic hash-based approach.
        /// </summary>
        public float Noise2D(float x, float y)
        {
            // Integer grid coordinates
            int ix = x >= 0 ? (int)x : (int)x - 1;
            int iy = y >= 0 ? (int)y : (int)y - 1;

            // Fractional parts for interpolation
            float fx = x - ix;
            float fy = y - iy;

            // Smoothstep for smoother interpolation
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            // Hash the four corners
            float n00 = HashToFloat(ix, iy);
            float n10 = HashToFloat(ix + 1, iy);
            float n01 = HashToFloat(ix, iy + 1);
            float n11 = HashToFloat(ix + 1, iy + 1);

            // Bilinear interpolation
            float nx0 = Lerp(n00, n10, sx);
            float nx1 = Lerp(n01, n11, sx);
            return Lerp(nx0, nx1, sy);
        }

        /// <summary>
        /// Returns a gradient noise value for the given coordinates.
        /// Produces smoother results than simple value noise.
        /// </summary>
        public float GradientNoise2D(float x, float y)
        {
            int ix = x >= 0 ? (int)x : (int)x - 1;
            int iy = y >= 0 ? (int)y : (int)y - 1;

            float fx = x - ix;
            float fy = y - iy;

            // Quintic interpolation for C2 continuity
            float sx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float sy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

            // Gradient vectors at corners (simplified 2D gradients)
            float g00 = GradientDot(ix, iy, fx, fy);
            float g10 = GradientDot(ix + 1, iy, fx - 1f, fy);
            float g01 = GradientDot(ix, iy + 1, fx, fy - 1f);
            float g11 = GradientDot(ix + 1, iy + 1, fx - 1f, fy - 1f);

            // Bilinear interpolation
            float nx0 = Lerp(g00, g10, sx);
            float nx1 = Lerp(g01, g11, sx);
            float value = Lerp(nx0, nx1, sy);

            // Normalize to [0, 1]
            return value * 0.5f + 0.5f;
        }

        /// <summary>
        /// Generates multi-octave fractal Brownian motion noise.
        /// </summary>
        public float FBm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += GradientNoise2D(x * frequency, y * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum / maxValue;
        }

        private float GradientDot(int ix, int iy, float dx, float dy)
        {
            // Hash to get a gradient direction
            uint hash = HashCoords(ix, iy);
            int gradIndex = (int)(hash & 7);

            // 8 gradient directions
            float gx, gy;
            switch (gradIndex)
            {
                case 0: gx = 1f; gy = 0f; break;
                case 1: gx = -1f; gy = 0f; break;
                case 2: gx = 0f; gy = 1f; break;
                case 3: gx = 0f; gy = -1f; break;
                case 4: gx = 0.707107f; gy = 0.707107f; break;
                case 5: gx = -0.707107f; gy = 0.707107f; break;
                case 6: gx = 0.707107f; gy = -0.707107f; break;
                default: gx = -0.707107f; gy = -0.707107f; break;
            }

            return dx * gx + dy * gy;
        }

        private float HashToFloat(int x, int y)
        {
            return (HashCoords(x, y) & 0x00FFFFFF) / 16777216f;
        }

        private uint HashCoords(int x, int y)
        {
            // Mix the seed into the hash for reproducibility
            ulong h = (ulong)x * 0x9E3779B97F4A7C15ul + (ulong)y * 0xBF58476D1CE4E5B9ul + _state;
            h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9ul;
            h = (h ^ (h >> 27)) * 0x94D049BB133111EBul;
            return (uint)(h ^ (h >> 31));
        }

        private ulong NextUInt64()
        {
            _state = SplitMix64(_state);
            return _state;
        }

        private static ulong SplitMix64(ulong x)
        {
            x += 0x9E3779B97F4A7C15ul;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9ul;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBul;
            return x ^ (x >> 31);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
