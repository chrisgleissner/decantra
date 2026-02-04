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
    /// Generates organic flow patterns using multi-layered warped noise with ridged variations.
    /// Produces smooth, flowing patterns reminiscent of fluid dynamics without particle artifacts.
    /// </summary>
    public sealed class CurlFlowAdvectionGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.CurlFlowAdvection;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets
            float offsetX = rng.NextFloat() * 500f;
            float offsetY = rng.NextFloat() * 500f;

            // Flow field parameters
            float baseScale = parameters.Scale * 2.5f;
            float warpStrength = parameters.WarpAmplitude * 0.8f;
            int octaves = parameters.Octaves;

            // Secondary offset for flow direction variation
            float flowOffsetX = rng.NextFloat() * 300f + 100f;
            float flowOffsetY = rng.NextFloat() * 300f + 100f;

            // Tertiary offset for detail layer
            float detailOffsetX = rng.NextFloat() * 200f + 200f;
            float detailOffsetY = rng.NextFloat() * 200f + 200f;

            // Choose a flow direction bias
            float flowAngle = rng.NextFloat() * 6.2831853f;
            float flowDirX = (float)Math.Cos(flowAngle);
            float flowDirY = (float)Math.Sin(flowAngle);

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Create flow-like warping using directional bias
                    float flowWarpX = rng.FBm(
                        nx * baseScale * 0.6f + flowOffsetX,
                        ny * baseScale * 0.6f + flowOffsetY,
                        3, 2.2f, 0.45f
                    ) * 2f - 1f;

                    float flowWarpY = rng.FBm(
                        nx * baseScale * 0.6f + flowOffsetX + 7.3f,
                        ny * baseScale * 0.6f + flowOffsetY + 4.1f,
                        3, 2.2f, 0.45f
                    ) * 2f - 1f;

                    // Add directional flow bias to warping
                    flowWarpX += flowDirX * 0.3f;
                    flowWarpY += flowDirY * 0.3f;

                    // Apply first warp layer
                    float warpedX = nx + flowWarpX * warpStrength;
                    float warpedY = ny + flowWarpY * warpStrength;

                    // Second warp layer for more organic feel
                    float warp2X = rng.FBm(
                        warpedX * baseScale * 0.8f + offsetX,
                        warpedY * baseScale * 0.8f + offsetY,
                        2, 2f, 0.5f
                    ) * 2f - 1f;

                    float warp2Y = rng.FBm(
                        warpedX * baseScale * 0.8f + offsetX + 3.2f,
                        warpedY * baseScale * 0.8f + offsetY + 5.7f,
                        2, 2f, 0.5f
                    ) * 2f - 1f;

                    float finalX = warpedX + warp2X * warpStrength * 0.5f;
                    float finalY = warpedY + warp2Y * warpStrength * 0.5f;

                    // Main pattern: multi-octave noise with flow characteristics
                    float mainValue = rng.FBm(
                        finalX * baseScale + offsetX,
                        finalY * baseScale + offsetY,
                        octaves, 2.1f, 0.48f
                    );

                    // Add ridged variation for more interesting flow lines
                    float ridgeValue = rng.FBm(
                        finalX * baseScale * 1.3f + detailOffsetX,
                        finalY * baseScale * 1.3f + detailOffsetY,
                        3, 2f, 0.5f
                    );
                    // Create ridges by folding the noise
                    ridgeValue = 1f - Math.Abs(ridgeValue * 2f - 1f);
                    ridgeValue = ridgeValue * ridgeValue; // Sharpen ridges slightly

                    // Blend main and ridge patterns
                    float blendFactor = parameters.IsMacroLayer ? 0.25f : 0.35f;
                    float value = Lerp(mainValue, ridgeValue, blendFactor);

                    // Add subtle detail layer
                    float detail = rng.FBm(
                        nx * baseScale * 2f + detailOffsetX,
                        ny * baseScale * 2f + detailOffsetY,
                        2, 2f, 0.5f
                    );
                    value = Lerp(value, detail, 0.15f);

                    // Apply density-based thresholding with smooth falloff
                    float densityThreshold = 1f - parameters.Density;
                    value = SmoothRemap(value, densityThreshold * 0.25f, 1f - densityThreshold * 0.15f);

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
            const int maxIterations = 3;

            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;

            for (int iter = 0; iter < maxIterations; iter++)
            {
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
