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
    /// Generates organic cellular patterns with soft Voronoi-like structures.
    /// Produces natural, biology-inspired backgrounds without hard edges.
    /// </summary>
    public sealed class OrganicCellsGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.OrganicCells;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Generate cell center points
            int cellCount = parameters.IsMacroLayer ? 15 : 25;
            cellCount = (int)(cellCount * (0.6f + parameters.Density * 0.8f));

            var cells = new (float x, float y)[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                cells[i] = (rng.NextFloat(), rng.NextFloat());
            }

            // Noise for organic warping
            float offsetX = rng.NextFloat() * 100f;
            float offsetY = rng.NextFloat() * 100f;
            float warpStrength = parameters.WarpAmplitude * 0.15f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Apply organic warping
                    float warpX = rng.FBm(nx * 4f + offsetX, ny * 4f + offsetY, 3, 2f, 0.5f) * 2f - 1f;
                    float warpY = rng.FBm(nx * 4f + offsetX + 10f, ny * 4f + offsetY + 10f, 3, 2f, 0.5f) * 2f - 1f;
                    float warpedNx = nx + warpX * warpStrength;
                    float warpedNy = ny + warpY * warpStrength;

                    // Find two nearest cell centers for soft Voronoi
                    float minDist1 = float.MaxValue;
                    float minDist2 = float.MaxValue;

                    foreach (var cell in cells)
                    {
                        float dx = warpedNx - cell.x;
                        float dy = warpedNy - cell.y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (dist < minDist1)
                        {
                            minDist2 = minDist1;
                            minDist1 = dist;
                        }
                        else if (dist < minDist2)
                        {
                            minDist2 = dist;
                        }
                    }

                    // Soft cell edge using difference between two nearest
                    float edgeDist = minDist2 - minDist1;
                    float cellEdge = SmoothStep(0f, 0.08f, edgeDist);

                    // Cell interior gradient
                    float cellInterior = SmoothStep(0f, 0.15f, minDist1);

                    // Combine for organic cell look
                    float value = Lerp(cellEdge, 1f - cellInterior, 0.5f);

                    // Add internal cell texture
                    float cellTexture = rng.FBm(warpedNx * 8f + offsetX + 50f, warpedNy * 8f + offsetY + 50f, 2, 2f, 0.5f);
                    value = Lerp(value, cellTexture, 0.2f);

                    // Apply density
                    value = SmoothRemap(value, 0.15f, 0.85f);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Apply softness via blur
            int blurPasses = parameters.IsMacroLayer ? 5 : 4;
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
