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
    /// Generates crystalline frost-like branching patterns.
    /// Produces delicate, ice-crystal and snowflake-inspired backgrounds.
    /// </summary>
    public sealed class CrystallineFrostGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.CrystallineFrost;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Randomize offsets
            float offsetX = rng.NextFloat() * 500f;
            float offsetY = rng.NextFloat() * 500f;

            // Crystal parameters
            float baseScale = parameters.Scale * 3f;
            int branchCount = parameters.IsMacroLayer ? 4 : 6;

            // Generate branching frost patterns using multiple radial components
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Multiple frost crystal centers
                    float totalFrost = 0f;

                    // Primary crystal pattern
                    float frost1 = GenerateFrostPattern(rng, nx, ny, 0.5f, 0.5f, branchCount, baseScale, offsetX, offsetY);
                    totalFrost += frost1 * 0.5f;

                    // Secondary offset crystals
                    float frost2 = GenerateFrostPattern(rng, nx, ny, 0.2f, 0.3f, branchCount - 1, baseScale * 0.8f, offsetX + 100f, offsetY + 100f);
                    totalFrost += frost2 * 0.25f;

                    float frost3 = GenerateFrostPattern(rng, nx, ny, 0.8f, 0.7f, branchCount - 1, baseScale * 0.7f, offsetX + 200f, offsetY + 200f);
                    totalFrost += frost3 * 0.25f;

                    // Add delicate noise texture
                    float texture = rng.FBm(nx * baseScale * 2f + offsetX + 300f, ny * baseScale * 2f + offsetY + 300f, 3, 2.5f, 0.45f);
                    totalFrost = Lerp(totalFrost, texture, 0.3f);

                    // Apply feathering
                    float value = SmoothRemap(totalFrost, 0.1f, 0.9f);

                    // Ensure minimum coverage
                    float baseNoise = rng.FBm(nx * 2f + offsetX, ny * 2f + offsetY, 2, 2f, 0.5f);
                    value = Math.Max(value, baseNoise * 0.3f);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply softness via blur for crystalline glow
            int blurPasses = parameters.IsMacroLayer ? 4 : 3;
            for (int i = 0; i < blurPasses; i++)
            {
                BoxBlur(field, width, height);
            }

            // Ensure no center bias
            EnforceNoCenterBias(field, width, height);

            return field;
        }

        private static float GenerateFrostPattern(DeterministicRng rng, float nx, float ny, float centerX, float centerY, int branches, float scale, float offsetX, float offsetY)
        {
            float dx = nx - centerX;
            float dy = ny - centerY;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)Math.Atan2(dy, dx);

            // Radial symmetry for crystal arms
            float armAngle = angle * branches;
            float armPattern = (float)Math.Cos(armAngle);
            armPattern = Math.Abs(armPattern);

            // Distance-based branching with noise
            float branchNoise = rng.FBm(dist * scale * 3f + offsetX, angle * 2f + offsetY, 3, 2f, 0.5f);

            // Create dendritic branching effect
            float dendritic = armPattern * (1f - dist * 1.5f);
            dendritic = Math.Max(0f, dendritic);
            dendritic = dendritic + branchNoise * 0.3f * (1f - dist);

            // Secondary smaller branches
            float subArms = (float)Math.Cos(armAngle * 3f + dist * 20f);
            subArms = Math.Abs(subArms);
            float subBranches = subArms * (1f - dist * 2f) * 0.5f;
            subBranches = Math.Max(0f, subBranches);

            // Combine main and sub branches
            float frost = dendritic + subBranches * 0.4f;

            // Radial falloff
            float falloff = 1f - SmoothStep(0f, 0.6f, dist);
            frost *= falloff;

            return Clamp01(frost);
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
            const float threshold = 1.05f;
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;

            ComputeCenterEdgeStats(field, width, height, out float center, out float edgeAvg);

            if (edgeAvg <= 0.0001f) return;
            float ratio = center / edgeAvg;
            if (ratio <= threshold) return;

            float scale = Clamp(threshold * edgeAvg / Math.Max(0.0001f, center), 0.2f, 0.95f);

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
