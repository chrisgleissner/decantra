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
    /// Generates curving vine and tendril patterns.
    /// Uses curl noise guided curves with leaf clusters.
    /// </summary>
    public sealed class VineTendrilsGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.VineTendrils;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var field = new float[width * height];
            var rng = new DeterministicRng(seed);

            // Generate multiple vine strands
            // NOTE: Image is 512x256 (landscape) but displayed in PORTRAIT mode
            // In portrait: x=1 is bottom of screen, x=0 is top of screen
            // Vines should primarily grow from right edge toward left
            int numVines = 5 + rng.NextInt(0, 4);

            for (int v = 0; v < numVines; v++)
            {
                float startX, startY;
                float initialAngle;

                // Bias toward starting from right edge (bottom in portrait)
                int edge = rng.NextInt(0, 6); // 0-3 = right, 4 = top/bottom
                if (edge <= 3)
                {
                    // Right edge (bottom of portrait screen) - most common
                    startX = rng.NextFloat() * 0.1f + 0.9f;
                    startY = rng.NextFloat() * 0.8f + 0.1f;
                    initialAngle = MathF.PI + rng.NextFloat() * 0.8f - 0.4f; // Grow left (up in portrait)
                }
                else if (edge == 4)
                {
                    // Top or bottom edge (sides in portrait)
                    startX = rng.NextFloat() * 0.5f + 0.25f;
                    startY = rng.NextFloat() < 0.5f ? 0f : 1f;
                    initialAngle = startY == 0f ? MathF.PI / 2 : -MathF.PI / 2;
                    initialAngle += rng.NextFloat() * 0.6f - 0.3f;
                }
                else
                {
                    // Left edge (top of portrait screen)
                    startX = rng.NextFloat() * 0.1f;
                    startY = rng.NextFloat() * 0.8f + 0.1f;
                    initialAngle = rng.NextFloat() * 0.8f - 0.4f; // Grow right (down in portrait)
                }

                ulong vineSeed = seed ^ (ulong)(v * 54321);
                DrawVine(field, width, height, startX, startY, initialAngle,
                    rng.NextFloat() * 0.4f + 0.4f, new DeterministicRng(vineSeed));
            }

            // Add background organic texture
            var bgRng = new DeterministicRng(seed + 7777);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;

                    float bg = bgRng.FBm(nx * 4f, ny * 3f, 4) * 0.2f + 0.1f;
                    field[y * width + x] = Math.Max(field[y * width + x], bg * 0.3f);
                }
            }

            // Soft blur
            BoxBlur(field, width, height, 2);

            // Density adjustment
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = SmoothRemap(field[i], 0.08f, 0.88f);
            }

            EnforceNoCenterBias(field, width, height);
            return field;
        }

        private void DrawVine(float[] field, int width, int height,
            float startX, float startY, float angle, float length, DeterministicRng rng)
        {
            float x = startX;
            float y = startY;
            float currentAngle = angle;
            float step = 0.003f;
            int steps = (int)(length / step);

            float thickness = rng.NextFloat() * 0.006f + 0.006f;
            var noiseRng = new DeterministicRng((ulong)(startX * 10000) ^ (ulong)(startY * 10000));

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;

                // Curl noise for organic movement
                float noiseScale = 3f;
                float noise = noiseRng.FBm(x * noiseScale, y * noiseScale, 2);
                float angleChange = (noise - 0.5f) * 2f;

                currentAngle += angleChange * 0.08f;

                // Move along vine
                x += MathF.Cos(currentAngle) * step;
                y += MathF.Sin(currentAngle) * step;

                // Stop if out of bounds
                if (x < -0.1f || x > 1.1f || y < -0.1f || y > 1.1f) break;

                // Draw vine segment
                float taper = 1f - t * 0.4f;
                DrawSoftCircle(field, width, height, x, y, thickness * taper, 0.7f);

                // Occasionally add leaves
                if (rng.NextFloat() < 0.03f)
                {
                    float leafAngle = currentAngle + (rng.NextFloat() < 0.5f ? MathF.PI / 2 : -MathF.PI / 2);
                    leafAngle += rng.NextFloat() * 0.6f - 0.3f;
                    DrawLeaf(field, width, height, x, y, leafAngle, rng.NextFloat() * 0.015f + 0.015f);
                }

                // Occasionally spawn tendril curls
                if (rng.NextFloat() < 0.015f)
                {
                    float tendrilAngle = currentAngle + rng.NextFloat() * 1.6f - 0.8f;
                    ulong tendrilSeed = (ulong)(s * 1000) ^ (ulong)(x * 10000);
                    DrawTendril(field, width, height, x, y, tendrilAngle, rng.NextFloat() * 0.03f + 0.03f,
                        new DeterministicRng(tendrilSeed));
                }

                // Occasionally branch
                if (t > 0.3f && rng.NextFloat() < 0.01f)
                {
                    float branchAngle = currentAngle + rng.NextFloat() * 1.2f - 0.6f;
                    ulong branchSeed = (ulong)(s * 2000) ^ (ulong)(y * 10000);
                    DrawVine(field, width, height, x, y, branchAngle,
                        length * (1f - t) * 0.6f, new DeterministicRng(branchSeed));
                }
            }
        }

        private void DrawTendril(float[] field, int width, int height,
            float startX, float startY, float angle, float length, DeterministicRng rng)
        {
            float x = startX;
            float y = startY;
            float currentAngle = angle;
            int steps = 30;
            float spiralRate = rng.NextFloat() * 0.1f + 0.15f;

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                float stepLen = length / steps * (1f - t * 0.5f);

                currentAngle += spiralRate;
                x += MathF.Cos(currentAngle) * stepLen;
                y += MathF.Sin(currentAngle) * stepLen;

                float tendrilThickness = 0.003f * (1f - t);
                DrawSoftCircle(field, width, height, x, y, tendrilThickness, 0.5f);
            }
        }

        private void DrawLeaf(float[] field, int w, int h,
            float x, float y, float angle, float size)
        {
            int leafSteps = 20;
            for (int i = 0; i < leafSteps; i++)
            {
                float t = i / (float)leafSteps;
                float leafX = x + MathF.Cos(angle) * size * t;
                float leafY = y + MathF.Sin(angle) * size * t;

                float widthT = MathF.Sin(t * MathF.PI) * size * 0.4f;
                DrawSoftCircle(field, w, h, leafX, leafY, widthT, 0.6f);
            }
        }

        private void DrawSoftCircle(float[] field, int width, int height,
            float cx, float cy, float radius, float intensity)
        {
            int pixelRadius = (int)(radius * width) + 2;
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
