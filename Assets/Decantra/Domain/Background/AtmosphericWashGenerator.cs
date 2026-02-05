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
    /// Generates soft, painterly atmospheric washes and gradients.
    /// Ideal for gentle, non-distracting backgrounds, especially for early game zones.
    /// </summary>
    public sealed class AtmosphericWashGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.AtmosphericWash;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets
            float offsetX = rng.NextFloat() * 500f;
            float offsetY = rng.NextFloat() * 500f;

            // Choose gradient style
            int gradientStyle = rng.NextInt(0, 4);
            float gradientAngle = rng.NextFloat() * 6.2831853f; // 2π
            float gradientCos = (float)Math.Cos(gradientAngle);
            float gradientSin = (float)Math.Sin(gradientAngle);

            // Fog layer parameters
            int fogLayers = parameters.IsMacroLayer ? 2 : 3;
            float[] fogScales = new float[fogLayers];
            float[] fogOffsetX = new float[fogLayers];
            float[] fogOffsetY = new float[fogLayers];
            float[] fogIntensities = new float[fogLayers];

            for (int i = 0; i < fogLayers; i++)
            {
                fogScales[i] = 2f + rng.NextFloat() * 3f * (i + 1);
                fogOffsetX[i] = rng.NextFloat() * 100f;
                fogOffsetY[i] = rng.NextFloat() * 100f;
                fogIntensities[i] = 0.15f + rng.NextFloat() * 0.2f;
            }

            // Generate base atmospheric field
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Base gradient
                    float gradient = ComputeGradient(nx, ny, gradientStyle, gradientCos, gradientSin);

                    // Warp the gradient slightly with noise
                    float warpX = rng.FBm(nx * 3f + offsetX, ny * 3f + offsetY, 2, 2f, 0.5f) * 2f - 1f;
                    float warpY = rng.FBm(nx * 3f + offsetX + 50f, ny * 3f + offsetY + 50f, 2, 2f, 0.5f) * 2f - 1f;

                    float warpedNx = nx + warpX * 0.08f;
                    float warpedNy = ny + warpY * 0.08f;
                    float warpedGradient = ComputeGradient(warpedNx, warpedNy, gradientStyle, gradientCos, gradientSin);

                    // Blend original and warped gradient
                    gradient = Lerp(gradient, warpedGradient, parameters.WarpAmplitude * 0.8f);

                    // Add fog layers
                    float fogSum = 0f;
                    for (int i = 0; i < fogLayers; i++)
                    {
                        float fogValue = rng.FBm(
                            nx * fogScales[i] + fogOffsetX[i],
                            ny * fogScales[i] + fogOffsetY[i],
                            3, 2f, 0.5f
                        );
                        // Soften the fog
                        fogValue = SmoothStep(0.3f, 0.7f, fogValue);
                        fogSum += fogValue * fogIntensities[i];
                    }

                    // Combine gradient and fog
                    float value;
                    if (parameters.IsMacroLayer)
                    {
                        // Macro: gradient-dominant with subtle fog
                        value = gradient * 0.7f + fogSum * 0.3f;
                    }
                    else
                    {
                        // Meso/Micro: more fog variation
                        value = gradient * 0.5f + fogSum * 0.5f;
                    }

                    // Apply density control - ensure minimum coverage
                    value = SmoothRemap(value, (1f - parameters.Density) * 0.1f, 0.95f);

                    // Ensure strong minimum value to prevent empty images
                    // Base minimum of 0.25 plus gradient contribution ensures visible content
                    float minValue = 0.25f + gradient * 0.25f + fogSum * 0.15f;
                    value = Math.Max(value, minValue);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply softness via blur
            int blurPasses = parameters.IsMacroLayer ? 4 : 3;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Add very subtle noise texture for "painterly" feel
            AddSubtleTexture(rng, field, width, height, offsetX + 200f, offsetY + 200f);

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
        }

        /// <summary>
        /// Computes base gradient value based on style.
        /// </summary>
        private static float ComputeGradient(float nx, float ny, int style, float cos, float sin)
        {
            switch (style)
            {
                case 0: // Linear diagonal
                    return Clamp01((nx * cos + ny * sin + 1f) * 0.5f);

                case 1: // Bilinear (centered)
                    {
                        float dx = (nx - 0.5f) * 2f;
                        float dy = (ny - 0.5f) * 2f;
                        float rotX = dx * cos - dy * sin;
                        float rotY = dx * sin + dy * cos;
                        float dist = (float)Math.Sqrt(rotX * rotX * 0.5f + rotY * rotY);
                        // Invert and smooth for vignette-like effect
                        return SmoothStep(0f, 1.2f, 1f - dist * 0.8f);
                    }

                case 2: // Radial soft
                    {
                        float dx = nx - 0.5f;
                        float dy = ny - 0.5f;
                        // Use cos/sin to offset the center slightly
                        dx -= cos * 0.15f;
                        dy -= sin * 0.15f;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy) * 1.8f;
                        return SmoothStep(0f, 1f, 1f - dist);
                    }

                case 3: // Multi-band
                default:
                    {
                        float proj = nx * cos + ny * sin;
                        float band = (float)Math.Sin(proj * 3.14159f * 2f) * 0.3f + 0.5f;
                        float linear = Clamp01((proj + 0.5f));
                        return Lerp(linear, band, 0.4f);
                    }
            }
        }

        /// <summary>
        /// Adds very subtle noise texture for painterly appearance.
        /// </summary>
        private static void AddSubtleTexture(DeterministicRng rng, float[] field, int width, int height, float offsetX, float offsetY)
        {
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Very fine noise
                    float texture = rng.GradientNoise2D(nx * 8f + offsetX, ny * 8f + offsetY);
                    texture = texture * 2f - 1f; // Center around 0

                    int idx = y * width + x;
                    // Add very subtle texture variation (±3%)
                    field[idx] = Clamp01(field[idx] + texture * 0.03f);
                }
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
