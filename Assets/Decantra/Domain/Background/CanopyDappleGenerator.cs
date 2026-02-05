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
    /// Generates dappled light patterns as seen through a leaf canopy.
    /// Simulates overlapping leaf shadows with light filtering through gaps.
    /// </summary>
    public sealed class CanopyDappleGenerator : IBackgroundFieldGenerator
    {
        public GeneratorArchetype Archetype => GeneratorArchetype.CanopyDapple;

        public float[] Generate(int width, int height, FieldParameters parameters, ulong seed)
        {
            var field = new float[width * height];
            var rng = new DeterministicRng(seed);

            // Start with bright base (sunlight)
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = 0.9f;
            }

            // Layer multiple leaf shadows at different depths
            int numLayers = 3 + rng.NextInt(0, 2);

            for (int layer = 0; layer < numLayers; layer++)
            {
                float layerOpacity = 0.3f + layer * 0.1f;
                float layerScale = 1f - layer * 0.15f;
                ulong layerSeed = seed ^ (ulong)(layer * 11111);

                int leavesInLayer = 15 + rng.NextInt(0, 10);
                var layerRng = new DeterministicRng(layerSeed);

                for (int leaf = 0; leaf < leavesInLayer; leaf++)
                {
                    float leafX = layerRng.NextFloat() * 1.2f - 0.1f;
                    float leafY = layerRng.NextFloat() * 1.2f - 0.1f;
                    float leafAngle = layerRng.NextFloat() * MathF.PI * 2f;
                    float leafSize = (layerRng.NextFloat() * 0.1f + 0.08f) * layerScale;

                    int leafType = layerRng.NextInt(0, 4);

                    DrawLeafShadow(field, width, height, leafX, leafY, leafAngle,
                        leafSize, layerOpacity, leafType, layerRng);
                }
            }

            // Add soft light filtering through gaps
            var lightRng = new DeterministicRng(seed + 1111);
            var lightRng2 = new DeterministicRng(seed + 2222);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;

                    float warpX = nx + lightRng.FBm(nx * 2f, ny * 4f, 2) * 0.15f;
                    float ray = MathF.Sin(warpX * MathF.PI * 8f) * 0.5f + 0.5f;
                    ray = MathF.Pow(ray, 3f);

                    float lightNoise = lightRng2.FBm(nx * 5f, ny * 5f, 3);

                    float lightBoost = ray * lightNoise * 0.2f;
                    field[y * width + x] = Math.Min(1f, field[y * width + x] + lightBoost);
                }
            }

            BoxBlur(field, width, height, 3);
            BoxBlur(field, width, height, 2);

            // Invert and adjust
            for (int i = 0; i < field.Length; i++)
            {
                float shadow = 1f - field[i];
                field[i] = SmoothRemap(shadow, 0.15f, 0.75f);
            }

            EnforceNoCenterBias(field, width, height);
            return field;
        }

        private void DrawLeafShadow(float[] field, int width, int height,
            float cx, float cy, float angle, float size, float opacity, int leafType,
            DeterministicRng rng)
        {
            switch (leafType)
            {
                case 0:
                    DrawOvalLeaf(field, width, height, cx, cy, angle, size, opacity);
                    break;
                case 1:
                    DrawPointedLeaf(field, width, height, cx, cy, angle, size, opacity);
                    break;
                case 2:
                    DrawCompoundLeaf(field, width, height, cx, cy, angle, size, opacity, rng);
                    break;
                default:
                    DrawRoundedLeaf(field, width, height, cx, cy, size, opacity);
                    break;
            }
        }

        private void DrawOvalLeaf(float[] field, int width, int height,
            float cx, float cy, float angle, float size, float opacity)
        {
            float aspectRatio = 2.5f;
            int samples = 40;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float leafT = t * 2f - 1f;

                float localX = leafT * size;
                float localWidth = MathF.Sqrt(Math.Max(0f, 1f - leafT * leafT)) * size / aspectRatio;

                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);

                float worldX = cx + localX * cosA;
                float worldY = cy + localX * sinA;

                float perpX = -sinA * localWidth;
                float perpY = cosA * localWidth;

                DrawSoftLine(field, width, height,
                    worldX - perpX, worldY - perpY,
                    worldX + perpX, worldY + perpY,
                    0.008f, opacity);
            }
        }

        private void DrawPointedLeaf(float[] field, int width, int height,
            float cx, float cy, float angle, float size, float opacity)
        {
            int samples = 35;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                float bulge = MathF.Sin(t * MathF.PI);
                float taper = 1f - t * t * 0.7f;
                float widthProfile = bulge * taper;

                float dist = t * size;

                float petalX = cx + MathF.Cos(angle) * dist;
                float petalY = cy + MathF.Sin(angle) * dist;

                float leafWidth = size * 0.35f * widthProfile;

                float cosA = MathF.Cos(angle);
                float sinA = MathF.Sin(angle);
                float perpX = -sinA * leafWidth;
                float perpY = cosA * leafWidth;

                DrawSoftLine(field, width, height,
                    petalX - perpX, petalY - perpY,
                    petalX + perpX, petalY + perpY,
                    0.006f, opacity);
            }
        }

        private void DrawCompoundLeaf(float[] field, int width, int height,
            float cx, float cy, float angle, float size, float opacity, DeterministicRng rng)
        {
            int leaflets = 3 + rng.NextInt(0, 3);
            float leafletSize = size * 0.4f;

            for (int i = 0; i < leaflets; i++)
            {
                float t = (i + 0.5f) / leaflets;
                float stemX = cx + MathF.Cos(angle) * size * (t - 0.5f);
                float stemY = cy + MathF.Sin(angle) * size * (t - 0.5f);

                float leftAngle = angle - MathF.PI / 3 + rng.NextFloat() * 0.4f - 0.2f;
                DrawOvalLeaf(field, width, height, stemX, stemY, leftAngle,
                    leafletSize * (0.7f + t * 0.3f), opacity * 0.8f);

                float rightAngle = angle + MathF.PI / 3 + rng.NextFloat() * 0.4f - 0.2f;
                DrawOvalLeaf(field, width, height, stemX, stemY, rightAngle,
                    leafletSize * (0.7f + t * 0.3f), opacity * 0.8f);
            }

            DrawPointedLeaf(field, width, height,
                cx + MathF.Cos(angle) * size * 0.4f,
                cy + MathF.Sin(angle) * size * 0.4f,
                angle, leafletSize, opacity);
        }

        private void DrawRoundedLeaf(float[] field, int width, int height,
            float cx, float cy, float size, float opacity)
        {
            SubtractSoftCircle(field, width, height, cx, cy, size * 0.5f, opacity);
        }

        private void DrawSoftLine(float[] field, int width, int height,
            float x1, float y1, float x2, float y2, float thickness, float intensity)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float lineLength = MathF.Sqrt(dx * dx + dy * dy);
            if (lineLength < 0.001f) return;

            int steps = Math.Max(5, (int)(lineLength * Math.Max(width, height) * 0.5f));

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float px = x1 + dx * t;
                float py = y1 + dy * t;

                SubtractSoftCircle(field, width, height, px, py, thickness, intensity);
            }
        }

        private void SubtractSoftCircle(float[] field, int width, int height,
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
                    field[idx] = Math.Max(0f, field[idx] - falloff * intensity);
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
