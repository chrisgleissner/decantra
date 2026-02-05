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
    /// Generates organic, cloud-like patterns using domain-warped fractal Brownian motion.
    /// Produces soft, billowy shapes without any grid or geometric artifacts.
    /// </summary>
    public sealed class DomainWarpedCloudsGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.DomainWarpedClouds;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets to prevent repetition across levels
            float offsetX = rng.NextFloat() * 1000f;
            float offsetY = rng.NextFloat() * 1000f;

            // Warp field offsets (for domain warping)
            float warpOffsetX1 = rng.NextFloat() * 500f + 100f;
            float warpOffsetY1 = rng.NextFloat() * 500f + 100f;
            float warpOffsetX2 = rng.NextFloat() * 500f + 200f;
            float warpOffsetY2 = rng.NextFloat() * 500f + 200f;

            // Scale factors
            float baseScale = parameters.Scale * 4f;
            float warpScale = baseScale * 0.7f;
            float warpAmplitude = parameters.WarpAmplitude * (parameters.IsMacroLayer ? 0.6f : 0.4f);

            int octaves = parameters.Octaves;
            float lacunarity = 2.1f;
            float gain = 0.52f;

            // First pass: generate with domain warping
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Sample coordinates for warping
                    float wx = nx * warpScale + warpOffsetX1;
                    float wy = ny * warpScale + warpOffsetY1;

                    // First warp layer
                    float warp1X = rng.FBm(wx, wy, 3, lacunarity, gain) * 2f - 1f;
                    float warp1Y = rng.FBm(wx + 5.2f, wy + 1.3f, 3, lacunarity, gain) * 2f - 1f;

                    // Second warp layer (warp the warp for more organic shapes)
                    float wx2 = (nx + warp1X * warpAmplitude) * warpScale + warpOffsetX2;
                    float wy2 = (ny + warp1Y * warpAmplitude) * warpScale + warpOffsetY2;

                    float warp2X = rng.FBm(wx2, wy2, 2, lacunarity, gain) * 2f - 1f;
                    float warp2Y = rng.FBm(wx2 + 3.7f, wy2 + 8.1f, 2, lacunarity, gain) * 2f - 1f;

                    // Final warped coordinates
                    float finalX = (nx + warp1X * warpAmplitude + warp2X * warpAmplitude * 0.5f) * baseScale + offsetX;
                    float finalY = (ny + warp1Y * warpAmplitude + warp2Y * warpAmplitude * 0.5f) * baseScale + offsetY;

                    // Generate the main cloud pattern
                    float value = rng.FBm(finalX, finalY, octaves, lacunarity, gain);

                    // Apply density-based thresholding with smooth falloff
                    float densityThreshold = 1f - parameters.Density;
                    value = SmoothRemap(value, densityThreshold * 0.3f, 1f - densityThreshold * 0.2f);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply softness via simple box blur passes
            if (parameters.Softness > 0.2f)
            {
                int blurPasses = parameters.IsMacroLayer ? 3 : 2;
                for (int i = 0; i < blurPasses; i++)
                {
                    BoxBlur(field, width, height);
                }
            }

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
        }

        /// <summary>
        /// Smooth remapping function that avoids harsh cutoffs.
        /// </summary>
        private static float SmoothRemap(float value, float inMin, float inMax)
        {
            if (inMax <= inMin) return value;
            float t = Clamp01((value - inMin) / (inMax - inMin));
            // Smoothstep for gentle transitions
            return t * t * (3f - 2f * t);
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

        /// <summary>
        /// Ensures no center-biased "egg" artifacts by applying edge-aware normalization.
        /// </summary>
        private static void EnforceNoCenterBias(float[] field, int width, int height)
        {
            const float threshold = 1.15f;
            const int maxIterations = 3;

            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                ComputeCenterEdgeStats(field, width, height, out float center, out float edgeAvg);

                if (edgeAvg <= 0.0001f) return;
                float ratio = center / edgeAvg;
                if (ratio <= threshold) return;

                // Apply radial falloff to reduce center intensity
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

            float edges = (
                SampleRegionAverage(field, width, height, (width - regionSize) / 2, 0, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, (width - regionSize) / 2, height - regionSize, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, 0, (height - regionSize) / 2, regionSize, regionSize) +
                SampleRegionAverage(field, width, height, width - regionSize, (height - regionSize) / 2, regionSize, regionSize)
            ) * 0.25f;

            edgeAvg = (corners + edges) * 0.5f;
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

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
