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
    /// Generates elegant marble-like veined patterns.
    /// Produces flowing, stone-like textures with organic veins.
    /// </summary>
    public sealed class MarbledFlowGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.MarbledFlow;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets
            float offsetX = rng.NextFloat() * 500f;
            float offsetY = rng.NextFloat() * 500f;

            // Vein parameters
            float baseScale = parameters.Scale * 3f;
            float turbulence = parameters.WarpAmplitude * 1.5f;
            int octaves = parameters.Octaves;

            // Choose vein direction
            float veinAngle = rng.NextFloat() * 3.14159f;
            float veinDirX = (float)Math.Cos(veinAngle);
            float veinDirY = (float)Math.Sin(veinAngle);

            // Secondary vein set for complexity
            float vein2Angle = veinAngle + 1.2f + rng.NextFloat() * 0.5f;
            float vein2DirX = (float)Math.Cos(vein2Angle);
            float vein2DirY = (float)Math.Sin(vein2Angle);

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Turbulent displacement
                    float turbX = rng.FBm(nx * baseScale + offsetX, ny * baseScale + offsetY, octaves, 2f, 0.5f) * 2f - 1f;
                    float turbY = rng.FBm(nx * baseScale + offsetX + 5.2f, ny * baseScale + offsetY + 3.7f, octaves, 2f, 0.5f) * 2f - 1f;

                    // Warped coordinates
                    float warpedX = nx + turbX * turbulence;
                    float warpedY = ny + turbY * turbulence;

                    // Primary veins using sine waves through turbulent field
                    float veinProjection = warpedX * veinDirX + warpedY * veinDirY;
                    float primaryVein = (float)Math.Sin(veinProjection * 15f + turbX * 3f);
                    primaryVein = Math.Abs(primaryVein);
                    primaryVein = (float)Math.Pow(primaryVein, 0.5f); // Soften

                    // Secondary veins
                    float vein2Projection = warpedX * vein2DirX + warpedY * vein2DirY;
                    float secondaryVein = (float)Math.Sin(vein2Projection * 10f + turbY * 2f);
                    secondaryVein = Math.Abs(secondaryVein);
                    secondaryVein = (float)Math.Pow(secondaryVein, 0.7f);

                    // Fine detail veins
                    float fineVein = rng.FBm(warpedX * baseScale * 2f + offsetX, warpedY * baseScale * 2f + offsetY, 2, 2.5f, 0.6f);
                    fineVein = Math.Abs(fineVein * 2f - 1f);

                    // Combine vein layers
                    float value = primaryVein * 0.5f + secondaryVein * 0.3f + fineVein * 0.2f;

                    // Add base marble color variation
                    float baseColor = rng.FBm(nx * 2f + offsetX + 100f, ny * 2f + offsetY + 100f, 3, 2f, 0.5f);
                    value = Lerp(baseColor * 0.6f + 0.2f, value, parameters.Density);

                    // Apply density threshold
                    value = SmoothRemap(value, 0.1f, 0.9f);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply softness via blur
            int blurPasses = parameters.IsMacroLayer ? 4 : 3;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
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
            const float threshold = 1.15f;
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
