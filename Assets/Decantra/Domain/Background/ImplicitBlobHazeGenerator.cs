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
    /// Generates soft, blob-like patterns using implicit metaball surfaces.
    /// Produces organic, amoeba-like shapes with soft edges.
    /// </summary>
    public sealed class ImplicitBlobHazeGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.ImplicitBlobHaze;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            // Generate random metaball positions and sizes
            int blobCount = parameters.IsMacroLayer ? 8 : 12;
            blobCount = (int)(blobCount * (0.7f + parameters.Density * 0.6f));

            var blobs = new (float x, float y, float radius, float strength)[blobCount];

            for (int i = 0; i < blobCount; i++)
            {
                blobs[i] = (
                    rng.NextFloat(),
                    rng.NextFloat(),
                    0.1f + rng.NextFloat() * 0.25f,
                    0.5f + rng.NextFloat() * 0.5f
                );
            }

            // Add some clustered smaller blobs around larger ones
            int clusterCount = blobCount / 2;
            var clusters = new (float x, float y, float radius, float strength)[clusterCount * 3];
            for (int i = 0; i < clusterCount; i++)
            {
                var parent = blobs[i % blobCount];
                for (int j = 0; j < 3; j++)
                {
                    int idx = i * 3 + j;
                    float angle = rng.NextFloat() * 6.2831853f;
                    float dist = parent.radius * (0.5f + rng.NextFloat() * 0.5f);
                    clusters[idx] = (
                        parent.x + (float)Math.Cos(angle) * dist,
                        parent.y + (float)Math.Sin(angle) * dist,
                        parent.radius * (0.3f + rng.NextFloat() * 0.3f),
                        parent.strength * (0.4f + rng.NextFloat() * 0.3f)
                    );
                }
            }

            // Combine all blobs
            var allBlobs = new (float x, float y, float radius, float strength)[blobCount + clusters.Length];
            Array.Copy(blobs, allBlobs, blobCount);
            Array.Copy(clusters, 0, allBlobs, blobCount, clusters.Length);

            // Compute metaball field
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    float totalField = 0f;

                    foreach (var blob in allBlobs)
                    {
                        float dx = nx - blob.x;
                        float dy = ny - blob.y;
                        float distSq = dx * dx + dy * dy;
                        float radiusSq = blob.radius * blob.radius;

                        // Soft falloff function (polynomial for smoothness)
                        if (distSq < radiusSq * 4f)
                        {
                            float t = distSq / (radiusSq * 4f);
                            float falloff = 1f - t * t;
                            falloff = falloff * falloff * falloff;
                            totalField += blob.strength * falloff;
                        }
                    }

                    // Apply threshold with soft edge
                    float value = SmoothStep(0.2f, 0.8f, totalField);

                    field[y * width + x] = Clamp01(value);
                }
            }

            // Add subtle noise for organic texture
            AddOrganicNoise(rng, field, width, height);

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
                    float noise = rng.FBm(nx * 5f + offsetX, ny * 5f + offsetY, 2, 2f, 0.5f);
                    int idx = y * width + x;
                    field[idx] = Clamp01(field[idx] + (noise - 0.5f) * 0.15f);
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
