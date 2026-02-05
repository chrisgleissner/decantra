/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Background;
using Decantra.Domain.Rules;
using UnityEngine;

namespace Decantra.Presentation
{
    /// <summary>
    /// Organic background generator using the new archetype-based system.
    /// Produces modern, fluid, non-geometric backgrounds.
    /// </summary>
    internal static class OrganicBackgroundGenerator
    {
        internal struct OrganicPatternSprites
        {
            public Sprite Macro;
            public Sprite Meso;
            public Sprite Accent;
            public Sprite Micro;
            public float GenerationMilliseconds;
            public GeneratorArchetype Archetype;
        }

        internal struct OrganicPatternRequest
        {
            public int LevelIndex;
            public int GlobalSeed;
            public ulong LevelSeed;
            public DensityProfile Density;
        }

        private const int MacroResolution = 256;
        private const int MesoResolution = 256;
        private const int AccentResolution = 192;
        private const int MicroResolution = 128;

        /// <summary>
        /// Generates organic background sprites using the new archetype system.
        /// </summary>
        internal static OrganicPatternSprites Generate(OrganicPatternRequest request)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            ulong levelSeed = request.LevelSeed != 0
                ? request.LevelSeed
                : BackgroundRules.GetLevelSeed(request.GlobalSeed, request.LevelIndex);

            if (request.LevelIndex <= 24)
            {
                levelSeed ^= 0xC3D2E1F0A5B49786ul;
                levelSeed = (levelSeed << 7) | (levelSeed >> 57);
            }

            // Select archetype based on level progression
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForLevel(request.LevelIndex, request.GlobalSeed);
            var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);

            // Get parameters for each scale band
            var macroParams = GetParameters(archetype, ScaleBand.Macro, request.Density);
            var mesoParams = GetParameters(archetype, ScaleBand.Meso, request.Density);
            var accentParams = GetParameters(archetype, ScaleBand.Meso, request.Density);
            var microParams = GetParameters(archetype, ScaleBand.Micro, request.Density);

            if (request.LevelIndex > 0 && request.LevelIndex % 10 == 0)
            {
                macroParams.Scale *= 0.75f;
                macroParams.WarpAmplitude = Mathf.Clamp01(macroParams.WarpAmplitude + 0.2f);
                macroParams.Octaves = Mathf.Min(6, macroParams.Octaves + 1);

                mesoParams.Scale *= 0.8f;
                mesoParams.WarpAmplitude = Mathf.Clamp01(mesoParams.WarpAmplitude + 0.15f);
                mesoParams.Octaves = Mathf.Min(6, mesoParams.Octaves + 1);
            }

            if (request.LevelIndex <= 24)
            {
                macroParams.Softness = Mathf.Clamp01(macroParams.Softness * 0.55f);
                mesoParams.Softness = Mathf.Clamp01(mesoParams.Softness * 0.6f);
                accentParams.Softness = Mathf.Clamp01(accentParams.Softness * 0.6f);
                microParams.Softness = Mathf.Clamp01(microParams.Softness * 0.7f);
            }

            // Vary seeds for each layer
            ulong macroSeed = levelSeed ^ 0xA13F2B19ul;
            ulong mesoSeed = levelSeed ^ 0xB24E3C28ul;
            ulong accentSeed = levelSeed ^ 0xC35F4D37ul;
            ulong microSeed = levelSeed ^ 0xD46A5E46ul;

            // Generate fields
            var macroField = generator.Generate(MacroResolution, MacroResolution, macroParams, macroSeed);
            var mesoField = generator.Generate(MesoResolution, MesoResolution, mesoParams, mesoSeed);
            var accentField = GenerateAccentField(archetype, AccentResolution, AccentResolution, accentSeed);
            var microField = GenerateMicroField(MicroResolution, MicroResolution, microSeed);

            // Create sprites
            float edgeFeather = request.LevelIndex <= 24 ? 0.25f : 0.12f;

            var macroSprite = CreateSpriteFromField(macroField, MacroResolution, MacroResolution, TextureWrapMode.Clamp, 256f, edgeFeather);
            var mesoSprite = CreateSpriteFromField(mesoField, MesoResolution, MesoResolution, TextureWrapMode.Clamp, 256f, edgeFeather);
            var accentSprite = CreateSpriteFromField(accentField, AccentResolution, AccentResolution, TextureWrapMode.Clamp, 256f, edgeFeather);
            var microSprite = CreateSpriteFromField(microField, MicroResolution, MicroResolution, TextureWrapMode.Repeat, 128f, 0f);

            timer.Stop();

            return new OrganicPatternSprites
            {
                Macro = macroSprite,
                Meso = mesoSprite,
                Accent = accentSprite,
                Micro = microSprite,
                GenerationMilliseconds = (float)timer.Elapsed.TotalMilliseconds,
                Archetype = archetype
            };
        }

        private static FieldParameters GetParameters(GeneratorArchetype archetype, ScaleBand scaleBand, DensityProfile density)
        {
            var baseParams = BackgroundGeneratorRegistry.GetDefaultParameters(archetype, scaleBand);

            // Adjust for density profile
            switch (density)
            {
                case DensityProfile.Sparse:
                    baseParams.Density = Mathf.Clamp01(baseParams.Density * 0.7f);
                    baseParams.Octaves = Mathf.Max(2, baseParams.Octaves - 1);
                    break;
                case DensityProfile.Dense:
                    baseParams.Density = Mathf.Clamp01(baseParams.Density * 1.3f);
                    baseParams.Octaves = Mathf.Min(6, baseParams.Octaves + 1);
                    break;
            }

            return baseParams;
        }

        /// <summary>
        /// Generates a subtle accent field using domain-warped clouds for contrast.
        /// </summary>
        private static float[] GenerateAccentField(GeneratorArchetype mainArchetype, int width, int height, ulong seed)
        {
            _ = mainArchetype;
            // Use DomainWarpedClouds for accent to avoid linear or Voronoi artifacts
            var generator = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.DomainWarpedClouds);
            var parameters = new FieldParameters
            {
                Scale = 0.55f,
                Density = 0.35f,
                Octaves = 3,
                WarpAmplitude = 0.35f,
                Softness = 0.85f,
                IsMacroLayer = false
            };

            return generator.Generate(width, height, parameters, seed);
        }

        /// <summary>
        /// Generates fine micro-scale texture using gradient noise.
        /// </summary>
        private static float[] GenerateMicroField(int width, int height, ulong seed)
        {
            var rng = new DeterministicRng(seed);
            var field = new float[width * height];

            float offsetX = rng.NextFloat() * 8f;
            float offsetY = rng.NextFloat() * 8f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float n1 = rng.FBm(nx * 6f + offsetX, ny * 6f + offsetY, 2, 2f, 0.5f);
                    float n2 = rng.FBm(nx * 12f + offsetX * 1.3f, ny * 12f + offsetY * 1.1f, 2, 2f, 0.5f);
                    float n = Mathf.Lerp(n1, n2, 0.5f);
                    field[y * width + x] = SmoothStep(0.2f, 0.8f, n);
                }
            }

            return field;
        }

        private static Sprite CreateSpriteFromField(float[] field, int width, int height, TextureWrapMode wrapMode, float pixelsPerUnit, float edgeFeather)
        {
            var colors = new Color32[field.Length];
            int maxX = width - 1;
            int maxY = height - 1;
            float feather = Mathf.Clamp01(edgeFeather);
            for (int i = 0; i < field.Length; i++)
            {
                float alphaFloat = Mathf.Clamp01(field[i]);
                alphaFloat = SmoothStep(0.2f, 0.8f, alphaFloat);
                alphaFloat = Mathf.Pow(alphaFloat, 0.85f);
                if (feather > 0f && wrapMode == TextureWrapMode.Clamp)
                {
                    int x = i % width;
                    int y = i / width;
                    float dx = Mathf.Min(x / (float)maxX, (maxX - x) / (float)maxX);
                    float dy = Mathf.Min(y / (float)maxY, (maxY - y) / (float)maxY);
                    float edge = Mathf.Min(dx, dy);
                    float edgeFactor = SmoothStep(0f, feather, edge);
                    alphaFloat *= edgeFactor;
                }
                byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaFloat * 255f), 0, 255);
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

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
