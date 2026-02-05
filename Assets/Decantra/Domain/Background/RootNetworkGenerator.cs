/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;

namespace Decantra.Domain.Background
{
    /// <summary>
    /// Generates underground root-like spreading network patterns.
    /// Uses diffusion-limited aggregation inspired approach.
    /// </summary>
    public sealed class RootNetworkGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.RootNetwork;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var field = new float[width * height];
            var rng = new DeterministicRng(seed);

            // Multiple root systems from soil surface
            // NOTE: Image is 512x256 (landscape) but displayed in PORTRAIT mode
            // In portrait: x=0 is top of screen (soil surface), x=1 is bottom (underground)
            // Roots grow from left edge (x≈0) toward right edge (x≈1)
            int numRoots = 3 + rng.NextInt(0, 3);

            for (int r = 0; r < numRoots; r++)
            {
                float rootX = rng.NextFloat() * 0.1f + 0.02f; // Start near left edge (top in portrait)
                float rootY = rng.NextFloat() * 0.6f + 0.2f;  // Spread along the soil line

                ulong rootSeed = seed ^ (ulong)(r * 98765);
                // Angle 0 means growing right (which is DOWN in portrait mode = into ground)
                DrawRootSystem(field, width, height, rootX, rootY,
                    rng.NextFloat() * 0.4f - 0.2f, // Slight angle variation around 0 (rightward)
                    rng.NextFloat() * 0.3f + 0.45f, // Longer roots
                    8 + rng.NextInt(0, 3),
                    new DeterministicRng(rootSeed));
            }

            // Add soil texture using layered noise
            var soilRng = new DeterministicRng(seed + 3333);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;

                    float depthGradient = ny * 0.15f;
                    float soil = soilRng.FBm(nx * 6f, ny * 4f, 3) * 0.15f;

                    field[y * width + x] = Math.Max(field[y * width + x],
                        (depthGradient + soil) * 0.4f);
                }
            }

            BoxBlur(field, width, height, 3);
            BoxBlur(field, width, height, 2);

            for (int i = 0; i < field.Length; i++)
            {
                field[i] = SmoothRemap(field[i], 0.1f, 0.85f);
            }

            EnforceNoCenterBias(field, width, height);
            return field;
        }

        private void DrawRootSystem(float[] field, int width, int height,
            float x, float y, float angle, float length, int depth, DeterministicRng rng)
        {
            if (depth <= 0 || length < 0.01f) return;

            // Gravity pulls roots rightward (which is DOWN in portrait mode)
            float gravityBias = 0.03f;

            float currentX = x;
            float currentY = y;
            float currentAngle = angle;
            float segmentLength = length * 0.35f;
            int segments = 10 + rng.NextInt(0, 5);

            float baseThickness = 0.005f + depth * 0.004f;
            List<(float x, float y, float angle, float length, int depth, ulong seed)> branches = new();

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;

                float noise = rng.FBm(currentX * 5f, currentY * 5f, 2);
                currentAngle += (noise - 0.5f) * 0.4f;

                // Gravity toward angle 0 (rightward = down in portrait)
                float angleToRight = 0f;
                float angleDiff = angleToRight - currentAngle;
                // Normalize angle diff to -PI to PI
                while (angleDiff > MathF.PI) angleDiff -= 2f * MathF.PI;
                while (angleDiff < -MathF.PI) angleDiff += 2f * MathF.PI;
                currentAngle += angleDiff * gravityBias;

                float stepLen = segmentLength / segments;
                float endX = currentX + MathF.Cos(currentAngle) * stepLen;
                float endY = currentY + MathF.Sin(currentAngle) * stepLen;

                float thickness = baseThickness * (1f - t * 0.5f);
                DrawSoftLine(field, width, height, currentX, currentY, endX, endY, thickness);

                if (rng.NextFloat() < 0.08f)
                {
                    DrawSoftCircle(field, width, height, endX, endY,
                        rng.NextFloat() * 0.007f + 0.008f, 0.5f);
                }

                if (depth > 1 && rng.NextFloat() < 0.25f)
                {
                    float branchAngle = currentAngle + rng.NextFloat() * 1.6f - 0.8f;

                    ulong branchSeed = (ulong)(s * 1000 + depth * 100) ^ (ulong)(endX * 10000);
                    branches.Add((endX, endY, branchAngle, length * 0.5f, depth - 1, branchSeed));
                }

                currentX = endX;
                currentY = endY;

                if (currentY > 1.05f || currentX < -0.05f || currentX > 1.05f) break;
            }

            if (depth > 1 && currentY < 0.95f)
            {
                ulong contSeed = (ulong)(depth * 5000) ^ (ulong)(currentX * 10000);
                DrawRootSystem(field, width, height, currentX, currentY,
                    currentAngle + rng.NextFloat() * 0.4f - 0.2f,
                    length * 0.7f, depth - 1, new DeterministicRng(contSeed));
            }

            foreach (var branch in branches)
            {
                DrawRootSystem(field, width, height, branch.x, branch.y,
                    branch.angle, branch.length, branch.depth,
                    new DeterministicRng(branch.seed));
            }

            if (depth <= 2)
            {
                DrawRootHairs(field, width, height, currentX, currentY, currentAngle, rng);
            }
        }

        private void DrawRootHairs(float[] field, int width, int height,
            float x, float y, float baseAngle, DeterministicRng rng)
        {
            int numHairs = 5 + rng.NextInt(0, 8);

            for (int h = 0; h < numHairs; h++)
            {
                float hairAngle = baseAngle + rng.NextFloat() * 2.4f - 1.2f;
                float hairLength = rng.NextFloat() * 0.015f + 0.01f;

                float endX = x + MathF.Cos(hairAngle) * hairLength;
                float endY = y + MathF.Sin(hairAngle) * hairLength;

                DrawSoftLine(field, width, height, x, y, endX, endY, 0.002f);
            }
        }

        private void DrawSoftLine(float[] field, int width, int height,
            float x1, float y1, float x2, float y2, float thickness)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float lineLength = MathF.Sqrt(dx * dx + dy * dy);
            if (lineLength < 0.001f) return;

            int steps = Math.Max(8, (int)(lineLength * Math.Max(width, height)));

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float px = x1 + dx * t;
                float py = y1 + dy * t;

                DrawSoftCircle(field, width, height, px, py, thickness, 0.7f);
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
