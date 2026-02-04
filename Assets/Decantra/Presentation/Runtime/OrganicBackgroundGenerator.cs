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
            public int ZoneIndex;
            public ulong ZoneSeed;
            public DensityProfile Density;
            public int MacroCount;
            public int MesoCount;
            public int MicroCount;
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

            // Select archetype based on zone
            var archetype = BackgroundGeneratorRegistry.SelectArchetypeForZone(request.ZoneIndex, request.ZoneSeed);
            var generator = BackgroundGeneratorRegistry.GetGenerator(archetype);

            // Get parameters for each scale band
            var macroParams = GetParameters(archetype, ScaleBand.Macro, request.Density);
            var mesoParams = GetParameters(archetype, ScaleBand.Meso, request.Density);
            var accentParams = GetParameters(archetype, ScaleBand.Meso, request.Density);
            var microParams = GetParameters(archetype, ScaleBand.Micro, request.Density);

            // Vary seeds for each layer
            ulong macroSeed = request.ZoneSeed ^ 0xA13F2B19ul;
            ulong mesoSeed = request.ZoneSeed ^ 0xB24E3C28ul;
            ulong accentSeed = request.ZoneSeed ^ 0xC35F4D37ul;
            ulong microSeed = request.ZoneSeed ^ 0xD46A5E46ul;

            // Generate fields
            var macroField = generator.Generate(MacroResolution, MacroResolution, macroParams, macroSeed);
            var mesoField = generator.Generate(MesoResolution, MesoResolution, mesoParams, mesoSeed);
            var accentField = GenerateAccentField(archetype, AccentResolution, AccentResolution, accentSeed);
            var microField = GenerateMicroField(MicroResolution, MicroResolution, microSeed);

            // Create sprites
            var macroSprite = CreateSpriteFromField(macroField, MacroResolution, MacroResolution, TextureWrapMode.Clamp, 256f);
            var mesoSprite = CreateSpriteFromField(mesoField, MesoResolution, MesoResolution, TextureWrapMode.Clamp, 256f);
            var accentSprite = CreateSpriteFromField(accentField, AccentResolution, AccentResolution, TextureWrapMode.Clamp, 256f);
            var microSprite = CreateSpriteFromField(microField, MicroResolution, MicroResolution, TextureWrapMode.Repeat, 128f);

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

        /// <summary>
        /// Generates organic background sprites from a legacy GeneratorFamily request.
        /// Maps the legacy family to the appropriate organic archetype.
        /// </summary>
        internal static OrganicPatternSprites GenerateFromLegacy(BackgroundPatternGenerator.PatternRequest legacyRequest)
        {
            // Compute zone index from seed (approximate - for compatibility)
            int zoneIndex = EstimateZoneFromSeed(legacyRequest.ZoneSeed);

            var request = new OrganicPatternRequest
            {
                ZoneIndex = zoneIndex,
                ZoneSeed = legacyRequest.ZoneSeed,
                Density = legacyRequest.Density,
                MacroCount = legacyRequest.MacroCount,
                MesoCount = legacyRequest.MesoCount,
                MicroCount = legacyRequest.MicroCount
            };

            return Generate(request);
        }

        private static int EstimateZoneFromSeed(ulong seed)
        {
            // Extract zone hint from seed structure if available
            // Otherwise return a default that cycles through archetypes
            return (int)((seed >> 16) % 10);
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
        /// Generates a subtle accent field using atmospheric wash for contrast.
        /// </summary>
        private static float[] GenerateAccentField(GeneratorArchetype mainArchetype, int width, int height, ulong seed)
        {
            // Use AtmosphericWash for accent regardless of main archetype
            // This provides consistent, subtle accent layers
            var generator = BackgroundGeneratorRegistry.GetGenerator(GeneratorArchetype.AtmosphericWash);
            var parameters = new FieldParameters
            {
                Scale = 0.6f,
                Density = 0.4f,
                Octaves = 2,
                WarpAmplitude = 0.15f,
                Softness = 0.8f,
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

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
