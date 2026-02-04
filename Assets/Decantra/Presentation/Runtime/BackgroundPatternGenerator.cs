/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;
using Decantra.Domain.Rules;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.Assertions;
#endif

namespace Decantra.Presentation
{
    internal static class BackgroundPatternGenerator
    {
        internal struct PatternSprites
        {
            public Sprite Macro;
            public Sprite Meso;
            public Sprite Accent;
            public Sprite Micro;
            public float GenerationMilliseconds;
        }

        internal struct PatternRequest
        {
            public GeneratorFamily PrimaryFamily;
            public GeneratorFamily? SecondaryFamily;
            public DensityProfile Density;
            public int MacroCount;
            public int MesoCount;
            public int MicroCount;
            public ulong ZoneSeed;
        }

        private struct Resolution
        {
            public int Macro;
            public int Meso;
            public int Accent;
            public int Micro;
        }

        internal static PatternSprites Generate(PatternRequest request)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var rng = new DeterministicRng(request.ZoneSeed ^ 0xA13F2B19u);
            Resolution res = GetResolution(request.PrimaryFamily);

            var macro = GeneratePrimaryField(request.PrimaryFamily, request.Density, request.MacroCount, res.Macro, res.Macro, rng, true);
            var meso = GeneratePrimaryField(request.PrimaryFamily, request.Density, request.MesoCount, res.Meso, res.Meso, rng, false);
            var accent = request.SecondaryFamily.HasValue
                ? GeneratePrimaryField(request.SecondaryFamily.Value, request.Density, Math.Max(1, request.MesoCount / 2), res.Accent, res.Accent, rng, false)
                : GenerateAccentNoise(res.Accent, res.Accent, rng);
            var micro = GenerateMicroNoise(res.Micro, res.Micro, rng);

            EnforceNoCenterBias(macro, res.Macro, res.Macro);
            ValidateNoCenterBias(macro, res.Macro, res.Macro, request.PrimaryFamily, "Macro");

            var macroSprite = CreateSpriteFromField(macro, res.Macro, res.Macro, TextureWrapMode.Clamp, 256f);
            var mesoSprite = CreateSpriteFromField(meso, res.Meso, res.Meso, TextureWrapMode.Clamp, 256f);
            var accentSprite = CreateSpriteFromField(accent, res.Accent, res.Accent, TextureWrapMode.Clamp, 256f);
            var microSprite = CreateSpriteFromField(micro, res.Micro, res.Micro, TextureWrapMode.Repeat, 128f);

            timer.Stop();

            return new PatternSprites
            {
                Macro = macroSprite,
                Meso = mesoSprite,
                Accent = accentSprite,
                Micro = microSprite,
                GenerationMilliseconds = (float)timer.Elapsed.TotalMilliseconds
            };
        }

        private static Resolution GetResolution(GeneratorFamily family)
        {
            switch (family)
            {
                case GeneratorFamily.VoronoiRegions:
                    return new Resolution { Macro = 192, Meso = 192, Accent = 160, Micro = 128 };
                case GeneratorFamily.FractalLite:
                    return new Resolution { Macro = 192, Meso = 192, Accent = 160, Micro = 128 };
                default:
                    return new Resolution { Macro = 256, Meso = 256, Accent = 192, Micro = 128 };
            }
        }

        private static float[] GeneratePrimaryField(GeneratorFamily family, DensityProfile density, int countHint, int width, int height, DeterministicRng rng, bool isMacro)
        {
            switch (family)
            {
                case GeneratorFamily.DirectionalLineFields:
                    return GenerateDirectionalLineField(width, height, density, countHint, rng, isMacro);
                case GeneratorFamily.BandGradients:
                    return GenerateBandGradientField(width, height, density, rng, isMacro);
                case GeneratorFamily.VoronoiRegions:
                    return GenerateVoronoiField(width, height, density, countHint, rng, isMacro);
                case GeneratorFamily.PolygonShards:
                    return GenerateShardField(width, height, density, rng, isMacro);
                case GeneratorFamily.WaveInterference:
                    return GenerateWaveInterferenceField(width, height, density, rng, isMacro);
                case GeneratorFamily.FractalLite:
                    return GenerateFractalField(width, height, density, rng, isMacro);
                default:
                    return GenerateDirectionalLineField(width, height, density, countHint, rng, isMacro);
            }
        }

        private static float[] GenerateDirectionalLineField(int width, int height, DensityProfile density, int countHint, DeterministicRng rng, bool isMacro)
        {
            int families = Mathf.Clamp(countHint, 1, 3);
            if (density == DensityProfile.Dense) families = Math.Min(3, families + 1);
            if (density == DensityProfile.Sparse) families = Math.Max(1, families - 1);

            var directions = new Vector2[families];
            var spacing = new float[families];
            var thickness = new float[families];
            var phase = new float[families];

            for (int i = 0; i < families; i++)
            {
                float angle = rng.NextFloat() * Mathf.PI * 2f;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                spacing[i] = Mathf.Lerp(isMacro ? 0.16f : 0.08f, isMacro ? 0.32f : 0.2f, rng.NextFloat());
                thickness[i] = Mathf.Lerp(isMacro ? 0.05f : 0.015f, isMacro ? 0.12f : 0.04f, rng.NextFloat());
                phase[i] = rng.NextFloat();
            }

            float warpAmp = Mathf.Lerp(0.0f, isMacro ? 0.03f : 0.015f, rng.NextFloat());
            float warpFreq = Mathf.Lerp(1.5f, 4.0f, rng.NextFloat());
            float warpPhase = rng.NextFloat() * Mathf.PI * 2f;
            Vector2 warpDir = new Vector2(Mathf.Cos(rng.NextFloat() * Mathf.PI * 2f), Mathf.Sin(rng.NextFloat() * Mathf.PI * 2f));

            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float baseCoord = Vector2.Dot(new Vector2(nx, ny), warpDir);
                    float warp = warpAmp * Mathf.Sin(baseCoord * warpFreq * Mathf.PI * 2f + warpPhase);
                    float value = 0f;

                    for (int i = 0; i < families; i++)
                    {
                        float proj = Vector2.Dot(new Vector2(nx + warp, ny + warp * 0.5f), directions[i]);
                        float t = Mathf.Repeat(proj / spacing[i] + phase[i], 1f);
                        float dist = Mathf.Abs(t - 0.5f);
                        float band = Mathf.Clamp01(1f - dist / thickness[i]);
                        float softened = SmoothCurve(band, isMacro ? 0.35f : 0.65f);
                        value = Mathf.Max(value, softened);
                    }

                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float[] GenerateBandGradientField(int width, int height, DensityProfile density, DeterministicRng rng, bool isMacro)
        {
            Vector2 dir = new Vector2(Mathf.Cos(rng.NextFloat() * Mathf.PI * 2f), Mathf.Sin(rng.NextFloat() * Mathf.PI * 2f));
            bool mirrored = rng.NextFloat() > 0.5f;
            bool tiled = rng.NextFloat() > 0.5f;

            float bandCount = isMacro
                ? Mathf.Lerp(2f, 4f, rng.NextFloat())
                : Mathf.Lerp(4f, density == DensityProfile.Dense ? 8f : 6f, rng.NextFloat());
            float bandSharpness = isMacro ? 0.5f : 0.75f;

            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float t = Vector2.Dot(new Vector2(nx, ny), dir) + 0.5f;
                    t = Mathf.Repeat(t, 1f);
                    if (tiled)
                    {
                        t = Mathf.Repeat(t * 2f, 1f);
                    }
                    if (mirrored)
                    {
                        t = Mathf.PingPong(t * 2f, 1f);
                    }

                    float band = Mathf.Abs(Mathf.Sin((t * bandCount + 0.15f) * Mathf.PI * 2f));
                    float ramp = Mathf.SmoothStep(0f, 1f, t);
                    float value = Mathf.Lerp(ramp, band, bandSharpness);
                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float[] GenerateVoronoiField(int width, int height, DensityProfile density, int countHint, DeterministicRng rng, bool isMacro)
        {
            int pointCount = Mathf.Clamp(countHint + (density == DensityProfile.Dense ? 2 : 0), 4, 9);
            var points = new Vector2[pointCount];
            int grid = Mathf.CeilToInt(Mathf.Sqrt(pointCount));
            float jitter = density == DensityProfile.Dense ? 0.18f : 0.12f;
            int index = 0;
            for (int gy = 0; gy < grid && index < pointCount; gy++)
            {
                for (int gx = 0; gx < grid && index < pointCount; gx++)
                {
                    float nx = (gx + 0.5f) / grid;
                    float ny = (gy + 0.5f) / grid;
                    float jx = (rng.NextFloat() - 0.5f) * jitter;
                    float jy = (rng.NextFloat() - 0.5f) * jitter;
                    points[index++] = new Vector2(Mathf.Clamp01(nx + jx), Mathf.Clamp01(ny + jy));
                }
            }

            var field = new float[width * height];
            float edgeScale = isMacro ? 0.25f : 0.6f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float minA = float.MaxValue;
                    float minB = float.MaxValue;

                    for (int i = 0; i < pointCount; i++)
                    {
                        float dx = nx - points[i].x;
                        float dy = ny - points[i].y;
                        float d = dx * dx + dy * dy;
                        if (d < minA)
                        {
                            minB = minA;
                            minA = d;
                        }
                        else if (d < minB)
                        {
                            minB = d;
                        }
                    }

                    float edge = Mathf.Clamp01((minB - minA) * 8f);
                    float value = isMacro
                        ? Mathf.Clamp01(1f - Mathf.Sqrt(minA) * 1.8f)
                        : Mathf.Pow(edge, edgeScale);
                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float[] GenerateShardField(int width, int height, DensityProfile density, DeterministicRng rng, bool isMacro)
        {
            int grid = density == DensityProfile.Dense ? 5 : 4;
            float jitter = density == DensityProfile.Dense ? 0.18f : 0.12f;
            var points = new Vector2[grid + 1, grid + 1];

            for (int y = 0; y <= grid; y++)
            {
                for (int x = 0; x <= grid; x++)
                {
                    float nx = x / (float)grid;
                    float ny = y / (float)grid;
                    float jx = (rng.NextFloat() - 0.5f) * jitter;
                    float jy = (rng.NextFloat() - 0.5f) * jitter;
                    points[x, y] = new Vector2(Mathf.Clamp01(nx + jx), Mathf.Clamp01(ny + jy));
                }
            }

            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                int cellY = Mathf.Min(grid - 1, Mathf.FloorToInt(ny * grid));
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    int cellX = Mathf.Min(grid - 1, Mathf.FloorToInt(nx * grid));

                    Vector2 p00 = points[cellX, cellY];
                    Vector2 p10 = points[cellX + 1, cellY];
                    Vector2 p01 = points[cellX, cellY + 1];
                    Vector2 p11 = points[cellX + 1, cellY + 1];

                    bool flip = ((cellX + cellY) % 2 == 0);
                    Vector2 a1 = flip ? p00 : p10;
                    Vector2 b1 = flip ? p10 : p11;
                    Vector2 c1 = flip ? p11 : p01;

                    Vector2 a2 = flip ? p00 : p10;
                    Vector2 b2 = flip ? p11 : p01;
                    Vector2 c2 = flip ? p01 : p00;

                    Vector2 p = new Vector2(nx, ny);
                    float value = TriangleValue(p, a1, b1, c1, isMacro);
                    if (value <= 0f)
                    {
                        value = TriangleValue(p, a2, b2, c2, isMacro);
                    }
                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float TriangleValue(Vector2 p, Vector2 a, Vector2 b, Vector2 c, bool isMacro)
        {
            Vector3 bary = ComputeBarycentric(p, a, b, c);
            if (bary.x < 0f || bary.y < 0f || bary.z < 0f) return 0f;

            float edge = Mathf.Min(bary.x, Mathf.Min(bary.y, bary.z));
            float edgeSoft = isMacro ? 0.18f : 0.06f;
            float edgeValue = Mathf.Clamp01(edge / edgeSoft);
            float gradient = 0.6f + 0.4f * bary.x;
            return Mathf.Clamp01(gradient * edgeValue);
        }

        private static Vector3 ComputeBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-5f) return new Vector3(-1f, -1f, -1f);
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            return new Vector3(u, v, w);
        }

        private static float[] GenerateWaveInterferenceField(int width, int height, DensityProfile density, DeterministicRng rng, bool isMacro)
        {
            int waveCount = density == DensityProfile.Dense ? 5 : 3;
            var dirs = new Vector2[waveCount];
            var freqs = new float[waveCount];
            var phases = new float[waveCount];
            var amps = new float[waveCount];

            for (int i = 0; i < waveCount; i++)
            {
                float angle = rng.NextFloat() * Mathf.PI * 2f;
                dirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                freqs[i] = Mathf.Lerp(isMacro ? 1.4f : 2.8f, isMacro ? 2.6f : 4.2f, rng.NextFloat());
                phases[i] = rng.NextFloat() * Mathf.PI * 2f;
                amps[i] = Mathf.Lerp(0.2f, 0.6f, rng.NextFloat());
            }

            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float sum = 0f;
                    for (int i = 0; i < waveCount; i++)
                    {
                        float t = Vector2.Dot(new Vector2(nx, ny), dirs[i]) * freqs[i] * Mathf.PI * 2f + phases[i];
                        sum += Mathf.Sin(t) * amps[i];
                    }
                    float value = Mathf.InverseLerp(-1f, 1f, sum);
                    if (!isMacro)
                    {
                        value = Mathf.SmoothStep(0.25f, 0.85f, value);
                    }
                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float[] GenerateFractalField(int width, int height, DensityProfile density, DeterministicRng rng, bool isMacro)
        {
            int octaves = density == DensityProfile.Dense ? 5 : 4;
            float baseFreq = isMacro ? 1.6f : 2.4f;
            float lacunarity = 2.1f;
            float gain = 0.55f;

            float offsetX = rng.NextFloat() * 10f;
            float offsetY = rng.NextFloat() * 10f;

            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float amp = 1f;
                    float freq = baseFreq;
                    float sum = 0f;
                    float norm = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        float n = Mathf.PerlinNoise(nx * freq + offsetX, ny * freq + offsetY);
                        sum += n * amp;
                        norm += amp;
                        amp *= gain;
                        freq *= lacunarity;
                    }

                    float value = sum / Mathf.Max(0.0001f, norm);
                    value = Mathf.SmoothStep(0.2f, 0.85f, value);
                    if (!isMacro)
                    {
                        value = Mathf.Pow(value, 1.35f);
                    }
                    field[y * width + x] = value;
                }
            }

            if (isMacro)
            {
                BoxBlur(field, width, height, 1);
            }

            return field;
        }

        private static float[] GenerateAccentNoise(int width, int height, DeterministicRng rng)
        {
            float offsetX = rng.NextFloat() * 5f;
            float offsetY = rng.NextFloat() * 5f;
            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float n = Mathf.PerlinNoise(nx * 4f + offsetX, ny * 4f + offsetY);
                    field[y * width + x] = Mathf.SmoothStep(0.35f, 0.85f, n);
                }
            }

            return field;
        }

        private static float[] GenerateMicroNoise(int width, int height, DeterministicRng rng)
        {
            float offsetX = rng.NextFloat() * 8f;
            float offsetY = rng.NextFloat() * 8f;
            var field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float n1 = Mathf.PerlinNoise(nx * 6f + offsetX, ny * 6f + offsetY);
                    float n2 = Mathf.PerlinNoise(nx * 12f + offsetX * 1.3f, ny * 12f + offsetY * 1.1f);
                    float n = Mathf.Lerp(n1, n2, 0.5f);
                    field[y * width + x] = Mathf.SmoothStep(0.2f, 0.8f, n);
                }
            }

            return field;
        }

        private static void ValidateNoCenterBias(float[] field, int width, int height, GeneratorFamily family, string label)
        {
            const float threshold = 1.15f;
            int regionSize = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) * 0.18f));
            float center = SampleRegionAverage(field, width, height, (width - regionSize) / 2, (height - regionSize) / 2, regionSize, regionSize);
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

            float edgeAverage = (corners + edges) * 0.5f;
            if (edgeAverage <= 0.0001f) return;

            float ratio = center / edgeAverage;
            if (ratio > threshold)
            {
                string message = $"Center bias detected in {family} {label} (center={center:0.000}, edge={edgeAverage:0.000}, ratio={ratio:0.00})";
                Debug.LogError(message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Assert.IsTrue(false, message);
#endif
            }
        }

        private static void EnforceNoCenterBias(float[] field, int width, int height)
        {
            const float threshold = 1.15f;
            const int maxIterations = 4;

            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float maxDist = Mathf.Sqrt(centerX * centerX + centerY * centerY);

            for (int i = 0; i < maxIterations; i++)
            {
                ComputeCenterEdgeStats(field, width, height, out float center, out float edgeAverage);
                if (edgeAverage <= 0.0001f)
                {
                    return;
                }

                float ratio = center / edgeAverage;
                if (ratio <= threshold)
                {
                    return;
                }

                float t = Mathf.Clamp01((ratio - threshold) / 0.6f);
                float gamma = Mathf.Lerp(1.1f, 1.55f, t);

                float min = float.MaxValue;
                float max = float.MinValue;
                for (int j = 0; j < field.Length; j++)
                {
                    float v = Mathf.Pow(field[j], gamma);
                    field[j] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                float range = Mathf.Max(0.0001f, max - min);
                for (int j = 0; j < field.Length; j++)
                {
                    field[j] = Mathf.Clamp01((field[j] - min) / range);
                }

                ComputeCenterEdgeStats(field, width, height, out center, out edgeAverage);
                if (edgeAverage <= 0.0001f)
                {
                    return;
                }

                ratio = center / edgeAverage;
                if (ratio <= threshold)
                {
                    return;
                }

                float targetRatio = threshold * 0.98f;
                float scale = Mathf.Clamp(targetRatio * edgeAverage / Mathf.Max(0.0001f, center), 0.5f, 0.98f);

                for (int y = 0; y < height; y++)
                {
                    float dy = (y - centerY) / Mathf.Max(0.0001f, centerY);
                    for (int x = 0; x < width; x++)
                    {
                        float dx = (x - centerX) / Mathf.Max(0.0001f, centerX);
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Min(1f, maxDist / Mathf.Max(0.0001f, maxDist));
                        float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dist));
                        float factor = Mathf.Lerp(scale, 1f, weight);
                        int idx = y * width + x;
                        field[idx] = Mathf.Clamp01(field[idx] * factor);
                    }
                }
            }
        }

        private static float ComputeCenterEdgeRatio(float[] field, int width, int height)
        {
            ComputeCenterEdgeStats(field, width, height, out float center, out float edgeAverage);
            if (edgeAverage <= 0.0001f) return 1f;
            return center / edgeAverage;
        }

        private static void ComputeCenterEdgeStats(float[] field, int width, int height, out float center, out float edgeAverage)
        {
            int regionSize = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(width, height) * 0.18f));
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

            edgeAverage = (corners + edges) * 0.5f;
        }

        private static float SampleRegionAverage(float[] field, int width, int height, int startX, int startY, int sizeX, int sizeY)
        {
            float sum = 0f;
            int count = 0;
            for (int y = startY; y < startY + sizeY; y++)
            {
                int row = y * width;
                for (int x = startX; x < startX + sizeX; x++)
                {
                    sum += field[row + x];
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        private static void BoxBlur(float[] field, int width, int height, int iterations)
        {
            if (iterations <= 0) return;
            var temp = new float[field.Length];
            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Copy(field, temp, field.Length);
                for (int y = 1; y < height - 1; y++)
                {
                    int row = y * width;
                    for (int x = 1; x < width - 1; x++)
                    {
                        int idx = row + x;
                        float sum = temp[idx] + temp[idx - 1] + temp[idx + 1] + temp[idx - width] + temp[idx + width];
                        field[idx] = sum * 0.2f;
                    }
                }
            }
        }

        private static float SmoothCurve(float value, float softness)
        {
            return Mathf.Lerp(value, value * value, softness);
        }

        private static Sprite CreateSpriteFromField(float[] field, int width, int height, TextureWrapMode wrapMode, float pixelsPerUnit)
        {
            var colors = new Color32[field.Length];
            for (int i = 0; i < field.Length; i++)
            {
                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(field[i] * 255f), 0, 255);
                colors[i] = new Color32(255, 255, 255, alpha);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrapMode,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private sealed class DeterministicRng
        {
            private uint _state;

            public DeterministicRng(ulong seed)
            {
                _state = (uint)(seed ^ (seed >> 32));
                if (_state == 0)
                {
                    _state = 0x6E624EB7u;
                }
            }

            public float NextFloat()
            {
                _state = 1664525u * _state + 1013904223u;
                return (_state & 0x00FFFFFF) / 16777216f;
            }
        }
    }
}
