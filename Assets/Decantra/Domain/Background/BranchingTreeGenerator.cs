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
    /// Generates recursive branching tree silhouettes.
    /// Uses space colonization-inspired algorithm with soft rendering.
    /// </summary>
    public sealed class BranchingTreeGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.BranchingTree;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var field = new float[width * height];
            var rng = new DeterministicRng(seed);

            // Multiple trees at different positions
            // NOTE: Image is 512x256 (landscape) but displayed in PORTRAIT mode
            // In portrait: x=1 is bottom of screen, x=0 is top of screen
            // Trees grow from right side (x≈0.85-0.95) toward left side (x≈0)
            int numTrees = 2 + rng.NextInt(0, 3);

            for (int t = 0; t < numTrees; t++)
            {
                float treeX = rng.NextFloat() * 0.1f + 0.88f; // Start near right edge (bottom in portrait)
                float treeY = rng.NextFloat() * 0.6f + 0.2f;  // Spread along the "ground" line
                float scale = rng.NextFloat() * 0.3f + 0.4f;  // Larger trees
                float angle = rng.NextFloat() * 0.3f - 0.15f; // Slight tilt
                int treeDepth = 7 + rng.NextInt(0, 3);
                ulong branchSeed = seed ^ (ulong)(t * 12345);

                // Angle -PI means growing left (which is UP in portrait mode)
                DrawBranch(field, width, height, treeX, treeY,
                    MathF.PI + angle, scale * 0.35f, treeDepth,
                    new DeterministicRng(branchSeed));
            }

            // Soft blur pass
            BoxBlur(field, width, height, 3);
            BoxBlur(field, width, height, 2);

            // Add soft atmospheric haze using fBm
            var hazeRng = new DeterministicRng(seed + 999);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;

                    float haze = hazeRng.FBm(nx * 3f, ny * 2f, 3) * 0.3f;
                    field[y * width + x] = Math.Min(1f, field[y * width + x] + haze * 0.15f);
                }
            }

            // Final density adjustment
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = SmoothRemap(field[i], 0.1f, 0.85f);
            }

            EnforceNoCenterBias(field, width, height);
            return field;
        }

        private void DrawBranch(float[] field, int width, int height,
            float x, float y, float angle, float length, int depth, DeterministicRng rng)
        {
            if (depth <= 0 || length < 0.005f) return;

            // Calculate end point
            float endX = x + MathF.Cos(angle) * length;
            float endY = y + MathF.Sin(angle) * length;

            // Draw the branch as a soft line
            float thickness = 0.008f + depth * 0.004f;
            DrawSoftLine(field, width, height, x, y, endX, endY, thickness);

            // Add some leaves/density near tips
            if (depth <= 2)
            {
                float leafSize = rng.NextFloat() * 0.02f + 0.02f;
                DrawSoftCircle(field, width, height, endX, endY, leafSize, 0.6f);
            }

            // Branching factor increases as we go higher
            int branches = 2;
            if (rng.NextFloat() < 0.4f) branches = 3;

            float spreadBase = 0.4f + rng.NextFloat() * 0.3f;

            for (int b = 0; b < branches; b++)
            {
                float branchAngle;
                if (branches == 2)
                {
                    float spread = rng.NextFloat() * 0.6f + 0.6f;
                    branchAngle = angle + (b == 0 ? -spreadBase : spreadBase) * spread;
                }
                else
                {
                    float spread = rng.NextFloat() * 0.4f + 0.7f;
                    branchAngle = angle + (b - 1) * spreadBase * spread;
                }

                // Add organic variation
                branchAngle += rng.NextFloat() * 0.3f - 0.15f;

                float branchLength = length * (rng.NextFloat() * 0.2f + 0.6f);
                ulong childSeed = (ulong)(depth * 1000 + b * 100) ^ (ulong)(x * 10000) ^ (ulong)(y * 10000);

                DrawBranch(field, width, height, endX, endY, branchAngle,
                    branchLength, depth - 1, new DeterministicRng(childSeed));
            }
        }

        private void DrawSoftLine(float[] field, int width, int height,
            float x1, float y1, float x2, float y2, float thickness)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float lineLength = MathF.Sqrt(dx * dx + dy * dy);
            if (lineLength < 0.001f) return;

            int steps = Math.Max(10, (int)(lineLength * Math.Max(width, height)));

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float px = x1 + dx * t;
                float py = y1 + dy * t;

                // Taper the thickness
                float taper = 1f - t * 0.3f;
                DrawSoftCircle(field, width, height, px, py, thickness * taper, 0.8f);
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
                    falloff = falloff * falloff * (3f - 2f * falloff); // Smoothstep

                    int idx = py * width + px;
                    field[idx] = Math.Min(1f, field[idx] + falloff * intensity);
                }
            }
        }

        private static void BoxBlur(float[] field, int width, int height, int radius)
        {
            var temp = new float[field.Length];
            float kernelSize = (2 * radius + 1);

            // Horizontal pass
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

            // Vertical pass
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
            // Subtle edge-to-center balance
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
