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
    /// Generates organic patterns using Julia set escape-time fractals.
    /// Produces intricate, swirling patterns with smooth density variations.
    /// </summary>
    public sealed class FractalEscapeDensityGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.FractalEscapeDensity;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Choose Julia set constant - select from known aesthetic values
            float cReal, cImag;
            SelectJuliaConstant(rng, out cReal, out cImag);

            // Viewport parameters - randomize for variety
            float centerX = rng.NextSignedFloat(0.3f);
            float centerY = rng.NextSignedFloat(0.3f);
            float zoom = 1.5f + rng.NextFloat() * 1.5f;

            int maxIterations = parameters.IsMacroLayer ? 40 : 60;
            float escapeRadius = 4f;

            // Generate fractal field
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Map to complex plane
                    float zReal = (nx - 0.5f) * 2f * zoom + centerX;
                    float zImag = (ny - 0.5f) * 2f * zoom * (height / (float)width) + centerY;

                    // Julia iteration
                    int iteration = 0;
                    float zReal2 = zReal * zReal;
                    float zImag2 = zImag * zImag;

                    while (zReal2 + zImag2 < escapeRadius && iteration < maxIterations)
                    {
                        float newReal = zReal2 - zImag2 + cReal;
                        float newImag = 2f * zReal * zImag + cImag;
                        zReal = newReal;
                        zImag = newImag;
                        zReal2 = zReal * zReal;
                        zImag2 = zImag * zImag;
                        iteration++;
                    }

                    // Smooth iteration count for anti-aliasing
                    float smoothIter = iteration;
                    if (iteration < maxIterations)
                    {
                        float log_zn = (float)Math.Log(zReal2 + zImag2) * 0.5f;
                        float nu = (float)(Math.Log(log_zn / Math.Log(2)) / Math.Log(2));
                        smoothIter = iteration + 1f - nu;
                    }

                    // Map to density value
                    float value = smoothIter / maxIterations;

                    // Apply organic transformation to avoid harsh bands
                    value = (float)Math.Sin(value * Math.PI * 0.5f);
                    value = value * value;

                    // Invert based on parameter for variety
                    if (parameters.Density > 0.5f)
                    {
                        value = 1f - value;
                    }

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Add subtle noise overlay for organic feel
            AddOrganicNoise(rng, field, width, height);

            // Apply softness via blur
            int blurPasses = parameters.IsMacroLayer ? 5 : 3;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
        }

        private static void SelectJuliaConstant(DeterministicRng rng, out float cReal, out float cImag)
        {
            // Pre-selected aesthetic Julia constants
            var constants = new (float r, float i)[]
            {
                (-0.7f, 0.27015f),       // Classic dendrite
                (-0.8f, 0.156f),         // Spiral
                (-0.4f, 0.6f),           // Douady rabbit
                (0.285f, 0.01f),         // Siegel disk
                (-0.835f, -0.2321f),     // Paisley
                (-0.70176f, -0.3842f),   // Dragon
                (0.37f, 0.1f),           // Soft swirl
                (-0.12f, 0.74f),         // Tendrils
            };

            int idx = rng.NextInt(0, constants.Length);
            var selected = constants[idx];

            // Add small variation
            cReal = selected.r + rng.NextSignedFloat(0.05f);
            cImag = selected.i + rng.NextSignedFloat(0.05f);
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
                    float noise = rng.FBm(nx * 4f + offsetX, ny * 4f + offsetY, 2, 2f, 0.5f);
                    int idx = y * width + x;
                    field[idx] = Clamp01(field[idx] + (noise - 0.5f) * 0.1f);
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

        private static void EnforceNoCenterBias(float[] field, int width, int height)
        {
            const float threshold = 1.2f;
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;

            ComputeCenterEdgeStats(field, width, height, out float center, out float edgeAvg);

            if (edgeAvg <= 0.0001f) return;
            float ratio = center / edgeAvg;
            if (ratio <= threshold) return;

            float scale = Clamp(threshold * edgeAvg / Math.Max(0.0001f, center), 0.6f, 0.95f);

            for (int y = 0; y < height; y++)
            {
                float dy = (y - centerY) / Math.Max(0.0001f, centerY);
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - centerX) / Math.Max(0.0001f, centerX);
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float weight = SmoothStep(0f, 1.5f, dist);
                    float factor = Lerp(scale, 1f, weight);
                    int idx = y * width + x;
                    field[idx] = Clamp01(field[idx] * factor);
                }
            }
        }

        private static void ComputeCenterEdgeStats(float[] field, int width, int height, out float center, out float edgeAvg)
        {
            int regionSize = Math.Max(2, (int)(Math.Min(width, height) * 0.18f));
            center = SampleRegionAverage(field, width, height, (width - regionSize) / 2, (height - regionSize) / 2, regionSize, regionSize);

            float corners = (
                SampleRegionAverage(field, width, height, 0, 0, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, width - regionSize, 0, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, 0, height - regionSize, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, width - regionSize, height - regionSize, regionSize, regionSize)
            ) * 0.25f;

            edgeAvg = corners;
        }

        private static float SampleRegionAverage(float[] field, int width, int height, int startX, int startY, int sizeX, int sizeY)
        {
            float sum = 0f;
            int count = 0;
            int endX = Math.Min(startX + sizeX, width);
            int endY = Math.Min(startY + sizeY, height);

            for (int y = startY; y < endY; y++)
            {
                int row = y * width;
                for (int x = startX; x < endX; x++)
                {
                    sum += field[row + x];
                    count++;
                }
            }
            return count > 0 ? sum / count : 0f;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
        private static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
