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
    /// Generates nebula-like cosmic glow patterns.
    /// Produces ethereal, space-like backgrounds with soft glowing regions.
    /// </summary>
    public sealed class NebulaGlowGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.NebulaGlow;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets
            float offsetX = rng.NextFloat() * 500f;
            float offsetY = rng.NextFloat() * 500f;

            // Multi-layer nebula parameters
            float baseScale = parameters.Scale * 2f;
            int octaves = parameters.Octaves;

            // Generate primary nebula clouds
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Large-scale nebula structure
                    float largeCloud = rng.FBm(nx * baseScale * 0.5f + offsetX, ny * baseScale * 0.5f + offsetY, octaves, 2.2f, 0.55f);

                    // Medium detail
                    float mediumCloud = rng.FBm(nx * baseScale + offsetX + 50f, ny * baseScale + offsetY + 50f, octaves - 1, 2f, 0.5f);

                    // Fine detail wisps
                    float fineCloud = rng.FBm(nx * baseScale * 2f + offsetX + 100f, ny * baseScale * 2f + offsetY + 100f, 2, 2.5f, 0.4f);

                    // Create wispy tendrils using ridged noise
                    float tendril = rng.FBm(nx * baseScale * 1.5f + offsetX + 150f, ny * baseScale * 1.5f + offsetY + 150f, 3, 2f, 0.5f);
                    tendril = 1f - Math.Abs(tendril * 2f - 1f);
                    tendril = tendril * tendril * tendril; // Sharp ridges for tendrils

                    // Combine layers with nebula-like weighting
                    float value = largeCloud * 0.4f + mediumCloud * 0.25f + fineCloud * 0.15f + tendril * 0.2f;

                    // Apply glow-like transformation
                    value = (float)Math.Pow(value, 0.7f);

                    // Add bright spots (stars/hotspots)
                    float spots = rng.FBm(nx * 8f + offsetX + 200f, ny * 8f + offsetY + 200f, 2, 3f, 0.3f);
                    spots = SmoothStep(0.7f, 0.9f, spots);
                    value = Math.Max(value, spots * 0.6f);

                    // Density-based adjustment
                    value = SmoothRemap(value, (1f - parameters.Density) * 0.15f, 1f - (1f - parameters.Density) * 0.1f);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply glow blur
            int blurPasses = parameters.IsMacroLayer ? 4 : 3;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Add bloom-like glow enhancement
            AddBloomGlow(field, width, height);

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
        }

        private static void AddBloomGlow(float[] field, int width, int height)
        {
            // Create a blurred copy for bloom
            var bloom = new float[field.Length];
            Array.Copy(field, bloom, field.Length);

            // Heavy blur for bloom
            for (int i = 0; i < 3; i++)
            {
                BoxBlur(bloom, width, height);
            }

            // Add bloom back to original
            for (int i = 0; i < field.Length; i++)
            {
                float bloomVal = bloom[i];
                // Screen blend approximation
                field[i] = Clamp01(field[i] + bloomVal * 0.3f * (1f - field[i]));
            }
        }

        private static float SmoothRemap(float value, float inMin, float inMax)
        {
            if (inMax <= inMin) return value;
            float t = Clamp01((value - inMin) / (inMax - inMin));
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
