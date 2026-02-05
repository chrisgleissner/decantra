/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Background
{
    /// <summary>
    /// Generates organic botanical patterns using Iterated Function Systems.
    /// Produces fern-like, branching, and floral density structures.
    /// </summary>
    public sealed class BotanicalIFSGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.BotanicalIFS;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Choose IFS type
            int ifsType = rng.NextInt(0, 4);

            // IFS iteration parameters
            int iterations = parameters.IsMacroLayer ? 80000 : 120000;
            iterations = (int)(iterations * (0.8f + parameters.Density * 0.4f));

            // Starting point
            float px = 0f;
            float py = 0f;

            // Get IFS coefficients
            var coeffs = GetIFSCoefficients(ifsType, rng);

            // Run IFS chaos game
            for (int i = 0; i < iterations; i++)
            {
                // Select transformation based on probabilities
                float r = rng.NextFloat();
                int transformIdx = SelectTransform(r, coeffs);
                var t = coeffs[transformIdx];

                // Apply affine transformation
                float newX = t.a * px + t.b * py + t.e;
                float newY = t.c * px + t.d * py + t.f;
                px = newX;
                py = newY;

                // Skip first iterations (let attractor stabilize)
                if (i < 100) continue;

                // Map to image coordinates
                int ix = (int)((px + 3f) / 6f * (width - 1));
                int iy = (int)((py + 0.5f) / 11f * (height - 1));

                if (ix >= 0 && ix < width && iy >= 0 && iy < height)
                {
                    int idx = iy * width + ix;
                    field[idx] += 0.02f;

                    // Add to neighbors for softer result
                    if (ix > 0) field[idx - 1] += 0.005f;
                    if (ix < width - 1) field[idx + 1] += 0.005f;
                    if (iy > 0) field[idx - width] += 0.005f;
                    if (iy < height - 1) field[idx + width] += 0.005f;
                }
            }

            // Normalize
            float maxVal = 0.0001f;
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] > maxVal) maxVal = field[i];
            }
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = Clamp01(field[i] / maxVal);
            }

            // Add organic noise overlay
            AddOrganicNoise(rng, field, width, height);

            // Light blur to preserve fine IFS structure while softening
            int blurPasses = parameters.IsMacroLayer ? 2 : 1;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Ensure coverage
            EnsureMinimumCoverage(field, rng, width, height);

            return field;
        }

        private struct IFSTransform
        {
            public float a, b, c, d, e, f;
            public float prob;
        }

        private static IFSTransform[] GetIFSCoefficients(int type, DeterministicRng rng)
        {
            // Add small random variations for uniqueness
            float vary() => rng.NextSignedFloat(0.02f);

            switch (type)
            {
                case 0: // Barnsley Fern
                    return new IFSTransform[]
                    {
                        new IFSTransform { a = 0f + vary(), b = 0f, c = 0f, d = 0.16f + vary(), e = 0f, f = 0f, prob = 0.01f },
                        new IFSTransform { a = 0.85f + vary(), b = 0.04f + vary(), c = -0.04f + vary(), d = 0.85f + vary(), e = 0f, f = 1.6f, prob = 0.85f },
                        new IFSTransform { a = 0.2f + vary(), b = -0.26f + vary(), c = 0.23f + vary(), d = 0.22f + vary(), e = 0f, f = 1.6f, prob = 0.07f },
                        new IFSTransform { a = -0.15f + vary(), b = 0.28f + vary(), c = 0.26f + vary(), d = 0.24f + vary(), e = 0f, f = 0.44f, prob = 0.07f },
                    };

                case 1: // Maple Leaf variant
                    return new IFSTransform[]
                    {
                        new IFSTransform { a = 0.14f + vary(), b = 0.01f, c = 0f, d = 0.51f + vary(), e = -0.08f, f = -1.31f, prob = 0.25f },
                        new IFSTransform { a = 0.43f + vary(), b = 0.52f + vary(), c = -0.45f + vary(), d = 0.5f + vary(), e = 1.49f, f = -0.75f, prob = 0.25f },
                        new IFSTransform { a = 0.45f + vary(), b = -0.49f + vary(), c = 0.47f + vary(), d = 0.47f + vary(), e = -1.62f, f = -0.74f, prob = 0.25f },
                        new IFSTransform { a = 0.49f + vary(), b = 0f, c = 0f, d = 0.51f + vary(), e = 0.02f, f = 1.62f, prob = 0.25f },
                    };

                case 2: // Tree-like
                    return new IFSTransform[]
                    {
                        new IFSTransform { a = 0.05f, b = 0f, c = 0f, d = 0.6f + vary(), e = 0f, f = 0f, prob = 0.1f },
                        new IFSTransform { a = 0.45f + vary(), b = -0.45f + vary(), c = 0.45f + vary(), d = 0.45f + vary(), e = 0f, f = 0.4f, prob = 0.45f },
                        new IFSTransform { a = 0.49f + vary(), b = 0.49f + vary(), c = -0.49f + vary(), d = 0.49f + vary(), e = 0f, f = 1.1f, prob = 0.45f },
                    };

                case 3: // Spiral fern
                default:
                    return new IFSTransform[]
                    {
                        new IFSTransform { a = 0f, b = 0f, c = 0f, d = 0.25f + vary(), e = 0f, f = -0.4f, prob = 0.02f },
                        new IFSTransform { a = 0.95f + vary(), b = 0.005f + vary(), c = -0.005f + vary(), d = 0.93f + vary(), e = -0.002f, f = 0.5f, prob = 0.84f },
                        new IFSTransform { a = 0.035f + vary(), b = -0.2f + vary(), c = 0.16f + vary(), d = 0.04f + vary(), e = -0.09f, f = 0.02f, prob = 0.07f },
                        new IFSTransform { a = -0.04f + vary(), b = 0.2f + vary(), c = 0.16f + vary(), d = 0.04f + vary(), e = 0.083f, f = 0.12f, prob = 0.07f },
                    };
            }
        }

        private static int SelectTransform(float r, IFSTransform[] coeffs)
        {
            float cumulative = 0f;
            for (int i = 0; i < coeffs.Length; i++)
            {
                cumulative += coeffs[i].prob;
                if (r < cumulative) return i;
            }
            return coeffs.Length - 1;
        }

        private static void AddOrganicNoise(DeterministicRng rng, float[] field, int width, int height)
        {
            float offsetX = rng.NextFloat() * 100f;
            float offsetY = rng.NextFloat() * 100f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float noise = rng.FBm(nx * 3f + offsetX, ny * 3f + offsetY, 3, 2f, 0.5f);
                    int idx = y * width + x;
                    // Blend noise with existing pattern
                    field[idx] = Clamp01(field[idx] * 0.7f + noise * 0.3f);
                }
            }
        }

        private static void EnsureMinimumCoverage(float[] field, DeterministicRng rng, int width, int height)
        {
            float offsetX = rng.NextFloat() * 50f;
            float offsetY = rng.NextFloat() * 50f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float baseNoise = rng.FBm(nx * 2f + offsetX, ny * 2f + offsetY, 2, 2f, 0.5f);
                    int idx = y * width + x;
                    field[idx] = Math.Max(field[idx], baseNoise * 0.25f);
                }
            }
        }

        private static void BoxBlur(float[] field, int width, int height)
        {
            var temp = new float[field.Length];
            Array.Copy(field, temp, field.Length);

            for (int y = 1; y < height - 1; y++)
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int idx = row + x;
                    float sum = temp[idx - width - 1] + temp[idx - width] + temp[idx - width + 1]
                              + temp[idx - 1] + temp[idx] + temp[idx + 1]
                              + temp[idx + width - 1] + temp[idx + width] + temp[idx + width + 1];
                    field[idx] = sum / 9f;
                }
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
