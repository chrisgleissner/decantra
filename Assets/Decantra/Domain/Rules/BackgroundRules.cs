/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
    /// <summary>
    /// Design language parameters for background generation.
    /// Each language family (every 10 levels) produces a distinct visual style.
    /// </summary>
    public struct DesignLanguage
    {
        public int LanguageId;
        public float BaseHue;
        public float BaseSaturation;
        public float BaseValue;
        public float HueRange;
        public float GradientShift;
        public MotifFamily PrimaryMotif;
        public MotifFamily SecondaryMotif;
        public float MotifDensity;
        public float MotifScale;
        public float ParallaxIntensity;
        public float LayerCount;
        public float MicroParticleDensity;
        public float MacroShapeScale;
        public float NoiseIntensity;
        public float DriftSpeed;
        public float DriftAngle;

        /// <summary>
        /// Returns the motif family as a string for validation/testing.
        /// </summary>
        public string MotifFamily => PrimaryMotif switch
        {
            Rules.MotifFamily.Bubbles => "Bubbles",
            Rules.MotifFamily.CrystallineShards => "Crystalline",
            Rules.MotifFamily.Leaves => "Leaves",
            Rules.MotifFamily.Mist => "Mist",
            Rules.MotifFamily.Waves => "Waves",
            Rules.MotifFamily.ParticulateDust => "Particles",
            Rules.MotifFamily.GeometricShapes => "Geometric",
            Rules.MotifFamily.OrganicBlobs => "Organic",
            Rules.MotifFamily.StarField => "Celestial",
            Rules.MotifFamily.RibbonStreams => "Abstract",
            Rules.MotifFamily.CellularPatterns => "Organic",
            Rules.MotifFamily.LightRays => "Celestial",
            Rules.MotifFamily.CloudWisps => "Mist",
            Rules.MotifFamily.FlowerPetals => "Leaves",
            Rules.MotifFamily.FeatherDrift => "Abstract",
            Rules.MotifFamily.AquaticFlow => "Waves",
            _ => "Abstract"
        };
    }

    /// <summary>
    /// Motif families define the primary visual elements in backgrounds.
    /// </summary>
    public enum MotifFamily
    {
        Bubbles = 0,
        CrystallineShards = 1,
        Leaves = 2,
        Mist = 3,
        Waves = 4,
        ParticulateDust = 5,
        GeometricShapes = 6,
        OrganicBlobs = 7,
        StarField = 8,
        RibbonStreams = 9,
        CellularPatterns = 10,
        LightRays = 11,
        CloudWisps = 12,
        FlowerPetals = 13,
        FeatherDrift = 14,
        AquaticFlow = 15
    }

    /// <summary>
    /// Level variation within a design language.
    /// </summary>
    public struct LevelVariation
    {
        public float HueJitter;
        public float SaturationJitter;
        public float ValueJitter;
        public float DetailOffsetX;
        public float DetailOffsetY;
        public float FlowRotation;
        public float ShapeRotation;

        public (float x, float y) DetailOffset => (DetailOffsetX, DetailOffsetY);
    }

    /// <summary>
    /// Background signature for collision detection.
    /// Two levels with identical signatures would produce identical backgrounds.
    /// </summary>
    public struct BackgroundSignature
    {
        public int LanguageId;
        public long ParameterHash;

        public override bool Equals(object obj)
        {
            if (obj is BackgroundSignature other)
            {
                return LanguageId == other.LanguageId && ParameterHash == other.ParameterHash;
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (LanguageId * 397) ^ ParameterHash.GetHashCode();
            }
        }
    }

    public static class BackgroundRules
    {
        private const int DesignLanguageSalt = 0x1F3D5B79;
        private const int LevelsPerLanguage = 10;
        private const int FirstLanguageSize = 9;

        /// <summary>
        /// Computes the language family index for a given level.
        /// Language 0 = levels 1-9, Language 1 = levels 10-19, etc.
        /// </summary>
        public static int GetLanguageId(int levelIndex)
        {
            if (levelIndex <= 0) return 0;
            if (levelIndex <= FirstLanguageSize) return 0;
            int remaining = levelIndex - FirstLanguageSize - 1;
            return 1 + Math.Max(0, remaining / LevelsPerLanguage);
        }

        /// <summary>
        /// Computes the position within the current language.
        /// </summary>
        private static int GetPositionInLanguage(int levelIndex)
        {
            if (levelIndex <= FirstLanguageSize)
            {
                return Math.Max(0, levelIndex - 1);
            }

            int remaining = levelIndex - FirstLanguageSize - 1;
            return Math.Max(0, remaining % LevelsPerLanguage);
        }

        /// <summary>
        /// Computes the palette index within a language family.
        /// </summary>
        public static int ComputePaletteIndex(int levelIndex, int seed, BackgroundThemeId themeId, int paletteCount)
        {
            if (paletteCount <= 0) throw new ArgumentOutOfRangeException(nameof(paletteCount));
            unchecked
            {
                int familyIndex = GetLanguageId(levelIndex);
                int mix = familyIndex * 73856093;
                mix ^= ((int)themeId + 1) * 83492791;
                int value = Math.Abs(mix);
                return value % paletteCount;
            }
        }

        /// <summary>
        /// Generates a deterministic design language for a given language family.
        /// Each of the 100+ languages has unique visual characteristics.
        /// </summary>
        public static DesignLanguage GetDesignLanguage(int languageId)
        {
            var rng = new SeededRng(languageId, DesignLanguageSalt);

            // Base hue distributed across the spectrum with variety
            float hueBase = rng.NextFloat();
            // Ensure distinct hue sectors for adjacent languages
            float hueOffset = (languageId % 12) / 12f;
            float baseHue = (hueBase * 0.4f + hueOffset * 0.6f) % 1f;

            // Saturation and value vary by language family
            float baseSaturation = Lerp(0.15f, 0.38f, rng.NextFloat());
            float baseValue = Lerp(0.42f, 0.68f, rng.NextFloat());

            // Hue range determines color variety within the language
            float hueRange = Lerp(0.04f, 0.12f, rng.NextFloat());

            // Gradient characteristics
            float gradientShift = Lerp(-0.1f, 0.1f, rng.NextFloat());

            // Motif selection - 16 families, no two adjacent languages use same primary
            int primaryMotifIndex = (languageId * 7 + rng.NextInt(0, 4)) % 16;
            int secondaryMotifIndex = (primaryMotifIndex + 4 + rng.NextInt(0, 8)) % 16;

            // Density and scale
            float motifDensity = Lerp(0.2f, 0.8f, rng.NextFloat());
            float motifScale = Lerp(0.6f, 1.4f, rng.NextFloat());

            // Parallax intensity varies by language
            float parallaxIntensity = Lerp(0.5f, 1.2f, rng.NextFloat());

            // Layer count (3-5 visual depth bands)
            float layerCount = Lerp(3f, 5f, rng.NextFloat());

            // Micro particles (fine detail)
            float microDensity = Lerp(0.1f, 0.6f, rng.NextFloat());

            // Macro shapes (large background forms)
            float macroScale = Lerp(0.8f, 1.6f, rng.NextFloat());

            // Noise/texture intensity
            float noiseIntensity = Lerp(0.05f, 0.25f, rng.NextFloat());

            // Animation parameters
            float driftSpeed = Lerp(0.3f, 1.2f, rng.NextFloat());
            float driftAngle = rng.NextFloat() * 360f;

            return new DesignLanguage
            {
                LanguageId = languageId,
                BaseHue = baseHue,
                BaseSaturation = baseSaturation,
                BaseValue = baseValue,
                HueRange = hueRange,
                GradientShift = gradientShift,
                PrimaryMotif = (MotifFamily)primaryMotifIndex,
                SecondaryMotif = (MotifFamily)secondaryMotifIndex,
                MotifDensity = motifDensity,
                MotifScale = motifScale,
                ParallaxIntensity = parallaxIntensity,
                LayerCount = layerCount,
                MicroParticleDensity = microDensity,
                MacroShapeScale = macroScale,
                NoiseIntensity = noiseIntensity,
                DriftSpeed = driftSpeed,
                DriftAngle = driftAngle
            };
        }

        /// <summary>
        /// Generates per-level variation within a design language.
        /// Returns jitter values (0-1) for visual parameters.
        /// </summary>
        public static void GetLevelVariation(int levelIndex, out float jitter1, out float jitter2, out float jitter3)
        {
            int languageId = GetLanguageId(levelIndex);
            int positionInLanguage = GetPositionInLanguage(levelIndex);

            unchecked
            {
                int hash1 = levelIndex * 73856093 ^ languageId * 19349663 ^ unchecked((int)0xA5A5A5A5);
                int hash2 = levelIndex * 83492791 ^ languageId * 73856093 ^ 0x5A5A5A5A;
                int hash3 = levelIndex * 19349663 ^ positionInLanguage * 83492791 ^ 0x3C3C3C3C;

                jitter1 = Math.Abs(hash1 % 10000) / 10000f;
                jitter2 = Math.Abs(hash2 % 10000) / 10000f;
                jitter3 = Math.Abs(hash3 % 10000) / 10000f;
            }
        }

        /// <summary>
        /// Returns a LevelVariation struct for a given level.
        /// </summary>
        public static LevelVariation GetLevelVariation(int levelIndex)
        {
            GetLevelVariation(levelIndex, out float j1, out float j2, out float j3);

            // Derive additional variation parameters
            unchecked
            {
                int hash4 = levelIndex * 12345 ^ 0x7F7F7F7F;
                int hash5 = levelIndex * 67891 ^ unchecked((int)0xABCDEF01);
                float j4 = Math.Abs(hash4 % 10000) / 10000f;
                float j5 = Math.Abs(hash5 % 10000) / 10000f;

                return new LevelVariation
                {
                    HueJitter = j1,
                    SaturationJitter = j2,
                    ValueJitter = j3,
                    DetailOffsetX = (j1 - 0.5f) * 24f,
                    DetailOffsetY = (j2 - 0.5f) * 16f,
                    FlowRotation = (j4 - 0.5f) * 14f,
                    ShapeRotation = (j5 - 0.5f) * 10f
                };
            }
        }

        /// <summary>
        /// Returns a unique string signature for a level's background configuration.
        /// No two level indices should produce the same signature.
        /// </summary>
        public static string GetBackgroundSignature(int levelIndex)
        {
            var sig = ComputeSignature(levelIndex);
            return $"{sig.LanguageId}:{sig.ParameterHash:X16}";
        }

        /// <summary>
        /// Computes a unique signature for a level's background.
        /// No two level indices should produce the same signature.
        /// </summary>
        public static BackgroundSignature ComputeSignature(int levelIndex)
        {
            int languageId = GetLanguageId(levelIndex);
            var language = GetDesignLanguage(languageId);
            GetLevelVariation(levelIndex, out float j1, out float j2, out float j3);

            unchecked
            {
                // Combine language parameters with level-specific jitter
                long hash = levelIndex;
                hash = hash * 31 + languageId;
                hash = hash * 31 + (int)(language.BaseHue * 10000);
                hash = hash * 31 + (int)(language.BaseSaturation * 10000);
                hash = hash * 31 + (int)(language.BaseValue * 10000);
                hash = hash * 31 + (int)language.PrimaryMotif;
                hash = hash * 31 + (int)language.SecondaryMotif;
                hash = hash * 31 + (int)(j1 * 10000);
                hash = hash * 31 + (int)(j2 * 10000);
                hash = hash * 31 + (int)(j3 * 10000);

                return new BackgroundSignature
                {
                    LanguageId = languageId,
                    ParameterHash = hash
                };
            }
        }

        /// <summary>
        /// Verifies that two level indices produce different backgrounds.
        /// </summary>
        public static bool AreBackgroundsDistinct(int levelIndex1, int levelIndex2)
        {
            if (levelIndex1 == levelIndex2) return false;
            var sig1 = ComputeSignature(levelIndex1);
            var sig2 = ComputeSignature(levelIndex2);
            return !sig1.Equals(sig2);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Simple seeded RNG for deterministic design language generation.
        /// </summary>
        private sealed class SeededRng
        {
            private int _state;

            public SeededRng(int seed, int salt)
            {
                unchecked
                {
                    _state = seed * 1103515245 + salt;
                    if (_state == 0) _state = salt ^ 0x12345678;
                }
            }

            public float NextFloat()
            {
                unchecked
                {
                    _state = _state * 1103515245 + 12345;
                    return Math.Abs(_state % 10000) / 10000f;
                }
            }

            public int NextInt(int min, int max)
            {
                if (max <= min) return min;
                unchecked
                {
                    _state = _state * 1103515245 + 12345;
                    return min + Math.Abs(_state) % (max - min);
                }
            }
        }
    }
}
