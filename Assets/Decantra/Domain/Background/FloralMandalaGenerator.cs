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
    /// Generates radial floral petal arrangements.
    /// Creates soft, flower-like mandala patterns with organic variation.
    /// </summary>
    public sealed class FloralMandalaGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.FloralMandala;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var field = new float[width * height];
            var rng = new DeterministicRng(seed);

            // Multiple floral arrangements at different positions
            int numFlowers = 2 + rng.NextInt(0, 3);

            for (int f = 0; f < numFlowers; f++)
            {
                float flowerX = rng.NextFloat() * 0.6f + 0.2f;
                float flowerY = rng.NextFloat() * 0.5f + 0.25f;
                float flowerSize = rng.NextFloat() * 0.2f + 0.2f;
                int petalCount = 5 + rng.NextInt(0, 8);
                float petalRoundness = rng.NextFloat() * 0.4f + 0.4f;

                ulong flowerSeed = seed ^ (ulong)(f * 77777);
                DrawFlower(field, width, height, flowerX, flowerY, flowerSize,
                    petalCount, petalRoundness, new DeterministicRng(flowerSeed));
            }

            // Add scattered smaller flowers/buds
            int numBuds = 5 + rng.NextInt(0, 5);
            for (int b = 0; b < numBuds; b++)
            {
                float budX = rng.NextFloat() * 0.9f + 0.05f;
                float budY = rng.NextFloat() * 0.9f + 0.05f;
                float budSize = rng.NextFloat() * 0.06f + 0.04f;
                int budPetals = 4 + rng.NextInt(0, 4);

                ulong budSeed = seed ^ (ulong)(b * 88888 + 1000);
                DrawFlower(field, width, height, budX, budY, budSize,
                    budPetals, 0.6f, new DeterministicRng(budSeed));
            }

            // Add organic background texture
            var bgRng = new DeterministicRng(seed + 5555);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;

                    float bg = bgRng.FBm(nx * 4f, ny * 3f, 3) * 0.15f;
                    field[y * width + x] = Math.Max(field[y * width + x], bg * 0.3f);
                }
            }

            BoxBlur(field, width, height, 3);
            BoxBlur(field, width, height, 2);

            for (int i = 0; i < field.Length; i++)
            {
                float value = field[i];
                if (!float.IsFinite(value)) value = 0f;
                field[i] = SmoothRemap(value, 0.1f, 0.85f);
            }

            EnforceNoCenterBias(field, width, height);
            return field;
        }

        private void DrawFlower(float[] field, int width, int height,
            float cx, float cy, float size, int petalCount, float roundness, DeterministicRng rng)
        {
            float angleStep = MathF.PI * 2f / petalCount;
            float startAngle = rng.NextFloat() * angleStep;

            int layers = 2 + rng.NextInt(0, 2);

            for (int layer = 0; layer < layers; layer++)
            {
                float layerScale = 1f - layer * 0.25f;
                float layerRotation = layer * angleStep * 0.3f;
                float layerIntensity = 0.5f + layer * 0.15f;

                for (int p = 0; p < petalCount; p++)
                {
                    float angle = startAngle + p * angleStep + layerRotation;
                    angle += rng.NextFloat() * 0.2f - 0.1f;

                    float petalSize = size * layerScale * (rng.NextFloat() * 0.3f + 0.85f);

                    DrawPetal(field, width, height, cx, cy, angle,
                        petalSize, roundness, layerIntensity);
                }
            }

            float centerSize = size * 0.15f;
            DrawSoftCircle(field, width, height, cx, cy, centerSize, 0.8f);

            int dots = 6 + rng.NextInt(0, 6);
            for (int d = 0; d < dots; d++)
            {
                float dotAngle = d * MathF.PI * 2f / dots + rng.NextFloat() * 0.4f - 0.2f;
                float dotDist = centerSize * (rng.NextFloat() * 0.7f + 0.8f);
                float dotX = cx + MathF.Cos(dotAngle) * dotDist;
                float dotY = cy + MathF.Sin(dotAngle) * dotDist * (width / (float)height);
                DrawSoftCircle(field, width, height, dotX, dotY, size * 0.02f, 0.6f);
            }
        }

        private void DrawPetal(float[] field, int width, int height,
            float cx, float cy, float angle, float size, float roundness, float intensity)
        {
            float aspectRatio = width / (float)height;

            int samples = 30;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                float bulge = MathF.Sin(t * MathF.PI);
                float taper = 1f - MathF.Pow(t, 2f - roundness);
                float widthProfile = bulge * taper;

                float dist = t * size;

                float petalX = cx + MathF.Cos(angle) * dist;
                float petalY = cy + MathF.Sin(angle) * dist / aspectRatio;

                float petalWidth = size * 0.35f * widthProfile;
                if (petalWidth <= 0.0001f) continue;

                DrawSoftCircle(field, width, height, petalX, petalY, petalWidth,
                    intensity * (0.6f + widthProfile * 0.4f));
            }
        }

        private void DrawSoftCircle(float[] field, int width, int height,
            float cx, float cy, float radius, float intensity)
        {
            if (radius <= 0.0001f || intensity <= 0f) return;

            int pixelRadius = (int)(radius * width) + 3;
            int centerX = (int)(cx * width);
            int centerY = (int)(cy * height);

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            {
                for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
                {
                    int px = centerX + dx;
                    int py = centerY + dy;

                    if (px < 0 || px >= width || py < 0 || py >= height) continue;

                    float dist = MathF.Sqrt(dx * dx / (float)(width * width) +
                                           dy * dy / (float)(height * height));
                    float falloff = 1f - Math.Min(1f, dist / radius);
                    falloff = falloff * falloff * (3f - 2f * falloff);

                    int idx = py * width + px;
                    field[idx] = Math.Min(1f, field[idx] + falloff * intensity);
                }
            }
        }

        private static void BoxBlur(float[] field, int width, int height, int radius)
        {
            var temp = new float[field.Length];
            float kernelSize = (2 * radius + 1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = Math.Clamp(x + dx, 0, width - 1);
                        sum += field[y * width + sx];
                    }
                    temp[y * width + x] = sum / kernelSize;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int sy = Math.Clamp(y + dy, 0, height - 1);
                        sum += temp[sy * width + x];
                    }
                    field[y * width + x] = sum / kernelSize;
                }
            }
        }

        private static float SmoothRemap(float value, float targetMin, float targetMax)
        {
            value = Math.Clamp(value, 0f, 1f);
            return targetMin + value * (targetMax - targetMin);
        }

        private static void EnforceNoCenterBias(float[] field, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1) - 0.5f;
                    float ny = y / (float)(height - 1) - 0.5f;
                    float distFromCenter = MathF.Sqrt(nx * nx + ny * ny) * 2f;
                    float edgeFactor = 1f - distFromCenter * 0.1f;
                    field[y * width + x] *= Math.Clamp(edgeFactor, 0.85f, 1f);
                }
            }
        }
    }
}
