/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections.Generic;

namespace Decantra.Domain.Rules
{
    public enum GeometryVocabulary
    {
        CirclesAndArcs = 0,
        RectanglesAndDiagonals = 1,
        PointsAndLines = 2,
        OrganicBlobs = 3,
        TrianglesOnly = 4,
        HexagonalTiling = 5
    }

    public enum GeneratorFamily
    {
        RadialPolar = 0,
        RegularTiling = 1,
        RecursiveSubdivision = 2,
        NoiseField = 3,
        WaveInterference = 4,
        DistanceFieldShapes = 5,
        FractalLite = 6
    }

    public enum SymmetryClass
    {
        None = 0,
        Axial = 1,
        Radial = 2,
        Grid = 3
    }

    public enum DensityProfile
    {
        Sparse = 0,
        Medium = 1,
        Dense = 2
    }

    public enum ScaleBand
    {
        Macro = 0,
        Meso = 1,
        Micro = 2
    }

    public enum LayerRole
    {
        Base = 0,
        Macro = 1,
        Meso = 2,
        Micro = 3,
        Accent = 4,
        Atmosphere = 5
    }

    public enum BlendMode
    {
        Normal = 0,
        Add = 1,
        Multiply = 2,
        Screen = 3,
        Overlay = 4,
        SoftLight = 5
    }

    public enum Crispness
    {
        Soft = 0,
        Medium = 1,
        Crisp = 2
    }

    public enum PlacementRule
    {
        Grid = 0,
        JitteredGrid = 1,
        Radial = 2,
        NoiseThreshold = 3,
        VoronoiCenters = 4
    }

    public enum MotionType
    {
        None = 0,
        SineDrift = 1,
        SlowRotation = 2,
        PhaseOscillation = 3
    }

    public enum FocalRule
    {
        CenterBias = 0,
        RingFocus = 1,
        GridIntersection = 2,
        DiagonalAxis = 3
    }

    public struct LayerSpec
    {
        public int Id;
        public LayerRole Role;
        public ScaleBand ScaleBand;
        public int GeneratorVariant;
        public float FrequencyOrScale;
        public float Density;
        public Crispness EdgeSoftness;
        public float ShapeSizeMin;
        public float ShapeSizeMax;
        public PlacementRule PlacementRule;
        public BlendMode BlendMode;
        public float Opacity;
        public int DepthIndex;
        public float ContrastMultiplier;
        public Crispness CrispnessModel;
        public float BlurRadiusOrFeather;
        public bool IsGradientOnly;
    }

    public struct MotionSpec
    {
        public int LayerId;
        public MotionType MotionType;
        public float Amplitude;
        public float PeriodSeconds;
        public float AxisX;
        public float AxisY;
    }

    public struct ZoneTheme
    {
        public int ZoneIndex;
        public ulong ZoneSeed;
        public GeometryVocabulary GeometryVocabulary;
        public GeneratorFamily PrimaryGeneratorFamily;
        public GeneratorFamily? SecondaryGeneratorFamily;
        public SymmetryClass SymmetryClass;
        public DensityProfile DensityProfile;
        public FocalRule FocalRule;
        public int LayerCount;
        public int MacroCount;
        public int MesoCount;
        public int MicroCount;
        public LayerSpec[] Layers;
        public float OpacityFalloff;
        public float ContrastFalloff;
        public bool HasParallax;
        public float ParallaxScaleShift;
        public float ParallaxPhaseOffset;
        public MotionSpec[] MotionLayers;
        public ZoneThemeFingerprint Fingerprint;
    }

    public struct ZoneThemeFingerprint
    {
        public GeometryVocabulary GeometryVocabulary;
        public GeneratorFamily PrimaryGeneratorFamily;
        public SymmetryClass SymmetryClass;
        public int LayerCount;
        public bool MotionPresence;
        public int MacroCount;
        public int MesoCount;
        public int MicroCount;
        public int CompositingSignature;
    }

    public struct LevelVariant
    {
        public int LevelIndex;
        public ulong LevelSeed;
        public int ZoneIndex;
        public int PaletteIndex;
        public float HueShift;
        public float SaturationLow;
        public float SaturationHigh;
        public float ValueLow;
        public float ValueHigh;
        public float GradientDirection;
        public float GradientIntensity;
        public float AccentStrength;
        public float PhaseOffset;
        public float MinorAmplitudeMod;
        public float MinorDensityMod;
        public float SmallPositionalJitter;
    }

    public static class BackgroundRules
    {
        private const int ZoneThemeSalt = 0x1F3D5B79;
        private const int LevelsPerZone = 10;
        private const int Zone0Size = 9;
        private const int FingerprintHistoryCount = 3;

        private static readonly Dictionary<ZoneThemeCacheKey, ZoneTheme> ZoneThemeCache = new Dictionary<ZoneThemeCacheKey, ZoneTheme>();

        private readonly struct ZoneThemeCacheKey : IEquatable<ZoneThemeCacheKey>
        {
            private readonly int _globalSeed;
            private readonly int _zoneIndex;

            public ZoneThemeCacheKey(int globalSeed, int zoneIndex)
            {
                _globalSeed = globalSeed;
                _zoneIndex = zoneIndex;
            }

            public bool Equals(ZoneThemeCacheKey other)
            {
                return _globalSeed == other._globalSeed && _zoneIndex == other._zoneIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ZoneThemeCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_globalSeed * 397) ^ _zoneIndex;
                }
            }
        }

        public static int GetZoneIndex(int levelIndex)
        {
            if (levelIndex <= 0) return 0;
            if (levelIndex <= Zone0Size) return 0;
            int remaining = levelIndex - Zone0Size - 1;
            return 1 + Math.Max(0, remaining / LevelsPerZone);
        }

        public static ulong GetZoneSeed(int globalSeed, int zoneIndex)
        {
            return Hash64((ulong)globalSeed, (ulong)zoneIndex);
        }

        public static ulong GetLevelSeed(int globalSeed, int levelIndex)
        {
            return Hash64((ulong)globalSeed, (ulong)levelIndex);
        }

        public static ZoneTheme GetZoneTheme(int zoneIndex, int globalSeed)
        {
            var key = new ZoneThemeCacheKey(globalSeed, zoneIndex);
            if (ZoneThemeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var history = new List<ZoneThemeFingerprint>(FingerprintHistoryCount);
            int start = Math.Max(0, zoneIndex - FingerprintHistoryCount);
            for (int i = start; i < zoneIndex; i++)
            {
                var previous = GetZoneTheme(i, globalSeed);
                history.Add(previous.Fingerprint);
            }

            var theme = GenerateZoneTheme(zoneIndex, globalSeed, history);
            ZoneThemeCache[key] = theme;
            return theme;
        }

        public static LevelVariant GetLevelVariant(int levelIndex, int globalSeed, int paletteCount)
        {
            if (paletteCount <= 0) throw new ArgumentOutOfRangeException(nameof(paletteCount));
            int zoneIndex = GetZoneIndex(levelIndex);
            ulong levelSeed = GetLevelSeed(globalSeed, levelIndex);
            var rng = new SeededRng(levelSeed, ZoneThemeSalt ^ 0x62B9D3E7);

            int paletteIndex = rng.NextInt(0, paletteCount);
            float hueShift = rng.NextSignedFloat(0.08f);
            float saturationLow = Clamp01(0.15f + rng.NextSignedFloat(0.05f));
            float saturationHigh = Clamp01(0.35f + rng.NextSignedFloat(0.07f));
            float valueLow = Clamp01(0.45f + rng.NextSignedFloat(0.08f));
            float valueHigh = Clamp01(0.68f + rng.NextSignedFloat(0.08f));
            float gradientDirection = rng.NextFloat() * 360f;
            float gradientIntensity = Lerp(0.2f, 0.65f, rng.NextFloat());
            float accentStrength = Lerp(0.25f, 0.8f, rng.NextFloat());
            float phaseOffset = rng.NextFloat() * 6.2831853f;
            float minorAmplitudeMod = Lerp(0.92f, 1.08f, rng.NextFloat());
            float minorDensityMod = Lerp(0.9f, 1.1f, rng.NextFloat());
            float smallPositionalJitter = Lerp(0.5f, 6f, rng.NextFloat());

            return new LevelVariant
            {
                LevelIndex = levelIndex,
                LevelSeed = levelSeed,
                ZoneIndex = zoneIndex,
                PaletteIndex = paletteIndex,
                HueShift = hueShift,
                SaturationLow = Math.Min(saturationLow, saturationHigh),
                SaturationHigh = Math.Max(saturationLow, saturationHigh),
                ValueLow = Math.Min(valueLow, valueHigh),
                ValueHigh = Math.Max(valueLow, valueHigh),
                GradientDirection = gradientDirection,
                GradientIntensity = gradientIntensity,
                AccentStrength = accentStrength,
                PhaseOffset = phaseOffset,
                MinorAmplitudeMod = minorAmplitudeMod,
                MinorDensityMod = minorDensityMod,
                SmallPositionalJitter = smallPositionalJitter
            };
        }

        public static string GetBackgroundSignature(int levelIndex, int globalSeed, int paletteCount)
        {
            var sig = ComputeSignature(levelIndex, globalSeed, paletteCount);
            return $"{sig.ZoneIndex}:{sig.ParameterHash:X16}";
        }

        public static BackgroundSignature ComputeSignature(int levelIndex, int globalSeed, int paletteCount)
        {
            int zoneIndex = GetZoneIndex(levelIndex);
            var zoneTheme = GetZoneTheme(zoneIndex, globalSeed);
            var levelVariant = GetLevelVariant(levelIndex, globalSeed, paletteCount);

            unchecked
            {
                long hash = levelIndex;
                hash = hash * 31 + zoneIndex;
                hash = hash * 31 + (int)zoneTheme.GeometryVocabulary;
                hash = hash * 31 + (int)zoneTheme.PrimaryGeneratorFamily;
                hash = hash * 31 + zoneTheme.LayerCount;
                hash = hash * 31 + zoneTheme.MacroCount;
                hash = hash * 31 + zoneTheme.MesoCount;
                hash = hash * 31 + zoneTheme.MicroCount;
                hash = hash * 31 + zoneTheme.Fingerprint.CompositingSignature;
                hash = hash * 31 + levelVariant.PaletteIndex;
                hash = hash * 31 + (int)(levelVariant.HueShift * 10000);
                hash = hash * 31 + (int)(levelVariant.GradientIntensity * 10000);
                hash = hash * 31 + (int)(levelVariant.MinorAmplitudeMod * 10000);
                hash = hash * 31 + (int)(levelVariant.MinorDensityMod * 10000);

                return new BackgroundSignature
                {
                    ZoneIndex = zoneIndex,
                    ParameterHash = hash
                };
            }
        }

        public static bool AreBackgroundsDistinct(int levelIndex1, int levelIndex2, int globalSeed, int paletteCount)
        {
            if (levelIndex1 == levelIndex2) return false;
            var sig1 = ComputeSignature(levelIndex1, globalSeed, paletteCount);
            var sig2 = ComputeSignature(levelIndex2, globalSeed, paletteCount);
            return !sig1.Equals(sig2);
        }

        public static bool IsGrayscaleRecognisable(ZoneTheme theme)
        {
            int softLayers = 0;
            int crispLayers = 0;
            bool hasGradient = false;

            foreach (var layer in theme.Layers)
            {
                if (layer.EdgeSoftness == Crispness.Soft) softLayers++;
                if (layer.EdgeSoftness == Crispness.Crisp) crispLayers++;
                if (layer.IsGradientOnly) hasGradient = true;
            }

            bool hasMacro = theme.MacroCount > 0;
            bool hasMeso = theme.MesoCount > 0;
            bool hasMicro = theme.MicroCount > 0;
            bool hasScaleDiversity = hasMacro && hasMeso && hasMicro;

            if (theme.LayerCount >= 8)
            {
                return hasGradient && hasScaleDiversity && softLayers >= 2 && crispLayers >= 2;
            }

            return hasGradient && hasScaleDiversity && crispLayers >= 1;
        }

        public static int EstimateZoneThemeWorkUnits(int layerCount)
        {
            return layerCount * 1200;
        }

        public static int EstimateLevelVariantWorkUnits()
        {
            return 2000;
        }

        private static ZoneTheme GenerateZoneTheme(int zoneIndex, int globalSeed, IReadOnlyList<ZoneThemeFingerprint> recentFingerprints)
        {
            ulong zoneSeed = GetZoneSeed(globalSeed, zoneIndex);
            int attempt = 0;
            ZoneTheme theme;

            while (true)
            {
                var rng = new SeededRng(zoneSeed + (ulong)attempt * 0x9E3779B97F4A7C15ul, ZoneThemeSalt);
                theme = BuildZoneTheme(zoneIndex, zoneSeed, rng, recentFingerprints);
                var fingerprint = ComputeFingerprint(theme);
                theme.Fingerprint = fingerprint;

                if (!IsTooSimilar(fingerprint, recentFingerprints))
                {
                    return theme;
                }

                attempt++;
                if (attempt > 12)
                {
                    return theme;
                }
            }
        }

        private static ZoneTheme BuildZoneTheme(int zoneIndex, ulong zoneSeed, SeededRng rng, IReadOnlyList<ZoneThemeFingerprint> recentFingerprints)
        {
            var previous = recentFingerprints != null && recentFingerprints.Count > 0
                ? recentFingerprints[recentFingerprints.Count - 1]
                : (ZoneThemeFingerprint?)null;

            GeometryVocabulary geometry = PickGeometryVocabulary(rng, previous?.GeometryVocabulary);
            GeneratorFamily primaryFamily = PickPrimaryGeneratorFamily(rng, previous?.PrimaryGeneratorFamily);
            SymmetryClass symmetry = PickSymmetry(primaryFamily, rng);
            DensityProfile density = (DensityProfile)rng.NextInt(0, 3);
            FocalRule focalRule = (FocalRule)rng.NextInt(0, 4);
            int layerCount = PickLayerCount(zoneIndex, rng, previous?.LayerCount);

            var bandCounts = AllocateScaleBands(layerCount, rng);
            var layers = BuildLayers(layerCount, bandCounts, primaryFamily, density, rng);
            var motion = BuildMotionLayers(layers, rng);

            return new ZoneTheme
            {
                ZoneIndex = zoneIndex,
                ZoneSeed = zoneSeed,
                GeometryVocabulary = geometry,
                PrimaryGeneratorFamily = primaryFamily,
                SecondaryGeneratorFamily = PickSecondaryGeneratorFamily(primaryFamily, rng),
                SymmetryClass = symmetry,
                DensityProfile = density,
                FocalRule = focalRule,
                LayerCount = layerCount,
                MacroCount = bandCounts.macroCount,
                MesoCount = bandCounts.mesoCount,
                MicroCount = bandCounts.microCount,
                Layers = layers,
                OpacityFalloff = Lerp(0.72f, 0.9f, rng.NextFloat()),
                ContrastFalloff = Lerp(0.7f, 0.95f, rng.NextFloat()),
                HasParallax = rng.NextFloat() > 0.4f,
                ParallaxScaleShift = Lerp(0.98f, 1.04f, rng.NextFloat()),
                ParallaxPhaseOffset = Lerp(0.0f, 0.25f, rng.NextFloat()),
                MotionLayers = motion
            };
        }

        private static GeometryVocabulary PickGeometryVocabulary(SeededRng rng, GeometryVocabulary? previous)
        {
            var options = new List<GeometryVocabulary>
            {
                GeometryVocabulary.CirclesAndArcs,
                GeometryVocabulary.RectanglesAndDiagonals,
                GeometryVocabulary.PointsAndLines,
                GeometryVocabulary.OrganicBlobs,
                GeometryVocabulary.TrianglesOnly,
                GeometryVocabulary.HexagonalTiling
            };
            if (previous.HasValue)
            {
                options.Remove(previous.Value);
            }
            return options[rng.NextInt(0, options.Count)];
        }

        private static GeneratorFamily PickPrimaryGeneratorFamily(SeededRng rng, GeneratorFamily? previous)
        {
            var options = new List<GeneratorFamily>
            {
                GeneratorFamily.RadialPolar,
                GeneratorFamily.RegularTiling,
                GeneratorFamily.RecursiveSubdivision,
                GeneratorFamily.NoiseField,
                GeneratorFamily.WaveInterference,
                GeneratorFamily.DistanceFieldShapes,
                GeneratorFamily.FractalLite
            };
            if (previous.HasValue)
            {
                options.Remove(previous.Value);
            }
            return options[rng.NextInt(0, options.Count)];
        }

        private static GeneratorFamily? PickSecondaryGeneratorFamily(GeneratorFamily primary, SeededRng rng)
        {
            if (rng.NextFloat() < 0.35f)
            {
                return null;
            }

            switch (primary)
            {
                case GeneratorFamily.RadialPolar:
                    return GeneratorFamily.WaveInterference;
                case GeneratorFamily.RegularTiling:
                    return GeneratorFamily.DistanceFieldShapes;
                case GeneratorFamily.RecursiveSubdivision:
                    return GeneratorFamily.RegularTiling;
                case GeneratorFamily.NoiseField:
                    return GeneratorFamily.DistanceFieldShapes;
                case GeneratorFamily.WaveInterference:
                    return GeneratorFamily.NoiseField;
                case GeneratorFamily.DistanceFieldShapes:
                    return GeneratorFamily.NoiseField;
                case GeneratorFamily.FractalLite:
                    return GeneratorFamily.RecursiveSubdivision;
                default:
                    return null;
            }
        }

        private static SymmetryClass PickSymmetry(GeneratorFamily family, SeededRng rng)
        {
            switch (family)
            {
                case GeneratorFamily.RadialPolar:
                    return rng.NextFloat() > 0.2f ? SymmetryClass.Radial : SymmetryClass.Axial;
                case GeneratorFamily.RegularTiling:
                    return SymmetryClass.Grid;
                case GeneratorFamily.RecursiveSubdivision:
                    return rng.NextFloat() > 0.4f ? SymmetryClass.Grid : SymmetryClass.Axial;
                case GeneratorFamily.WaveInterference:
                    return rng.NextFloat() > 0.5f ? SymmetryClass.Axial : SymmetryClass.None;
                case GeneratorFamily.DistanceFieldShapes:
                    return rng.NextFloat() > 0.5f ? SymmetryClass.Radial : SymmetryClass.None;
                case GeneratorFamily.NoiseField:
                    return SymmetryClass.None;
                case GeneratorFamily.FractalLite:
                    return rng.NextFloat() > 0.4f ? SymmetryClass.Axial : SymmetryClass.None;
                default:
                    return SymmetryClass.None;
            }
        }

        private static int PickLayerCount(int zoneIndex, SeededRng rng, int? previousLayerCount)
        {
            if (zoneIndex == 0)
            {
                return rng.NextInt(4, 8);
            }

            float progress = Math.Min(1f, zoneIndex / 8f);
            int min = (int)Math.Round(Lerp(5f, 10f, progress));
            int max = (int)Math.Round(Lerp(12f, 20f, progress));
            int count = rng.NextInt(min, max + 1);

            if (previousLayerCount.HasValue && previousLayerCount.Value == count)
            {
                count = Math.Min(20, count + 1);
                if (count == previousLayerCount.Value)
                {
                    count = Math.Max(4, count - 2);
                }
            }

            return ClampInt(count, 4, 20);
        }

        private static (int macroCount, int mesoCount, int microCount) AllocateScaleBands(int layerCount, SeededRng rng)
        {
            int macroMin = layerCount >= 8 ? 2 : 1;
            int mesoMin = layerCount >= 8 ? 2 : 1;
            int microMin = 1;

            int macroMax = layerCount >= 8 ? 6 : layerCount;
            int mesoMax = layerCount >= 8 ? 10 : layerCount;
            int microMax = layerCount >= 8 ? 8 : layerCount;

            int macro = macroMin;
            int meso = mesoMin;
            int micro = microMin;
            int remaining = layerCount - (macro + meso + micro);

            while (remaining > 0)
            {
                int pick = rng.NextInt(0, 3);
                if (pick == 0 && macro < macroMax)
                {
                    macro++;
                    remaining--;
                }
                else if (pick == 1 && meso < mesoMax)
                {
                    meso++;
                    remaining--;
                }
                else if (pick == 2 && micro < microMax)
                {
                    micro++;
                    remaining--;
                }
                else
                {
                    if (macro < macroMax)
                    {
                        macro++;
                    }
                    else if (meso < mesoMax)
                    {
                        meso++;
                    }
                    else if (micro < microMax)
                    {
                        micro++;
                    }
                    remaining--;
                }
            }

            return (macro, meso, micro);
        }

        private static LayerSpec[] BuildLayers(int layerCount, (int macroCount, int mesoCount, int microCount) bandCounts, GeneratorFamily family, DensityProfile density, SeededRng rng)
        {
            var layers = new List<LayerSpec>(layerCount);
            int id = 0;

            int gradientLayerIndex = rng.NextInt(0, Math.Max(1, bandCounts.macroCount));

            for (int i = 0; i < bandCounts.macroCount; i++)
            {
                layers.Add(BuildLayer(id++, ScaleBand.Macro, LayerRole.Macro, family, density, rng, i == gradientLayerIndex));
            }

            for (int i = 0; i < bandCounts.mesoCount; i++)
            {
                layers.Add(BuildLayer(id++, ScaleBand.Meso, LayerRole.Meso, family, density, rng, false));
            }

            for (int i = 0; i < bandCounts.microCount; i++)
            {
                layers.Add(BuildLayer(id++, ScaleBand.Micro, LayerRole.Micro, family, density, rng, false));
            }

            var result = layers.ToArray();
            EnsureCrispLayerMinimum(result, layerCount);
            return result;
        }

        private static LayerSpec BuildLayer(int id, ScaleBand scaleBand, LayerRole role, GeneratorFamily family, DensityProfile density, SeededRng rng, bool gradientOnly)
        {
            float densityBase = density switch
            {
                DensityProfile.Sparse => 0.35f,
                DensityProfile.Dense => 0.85f,
                _ => 0.6f
            };

            float scale = scaleBand switch
            {
                ScaleBand.Macro => Lerp(0.18f, 0.45f, rng.NextFloat()),
                ScaleBand.Meso => Lerp(0.6f, 1.4f, rng.NextFloat()),
                _ => Lerp(2.0f, 5.0f, rng.NextFloat())
            };

            float densityAdjusted = scaleBand switch
            {
                ScaleBand.Macro => densityBase * 0.7f,
                ScaleBand.Meso => densityBase,
                _ => densityBase * 1.15f
            };

            Crispness edgeSoftness = scaleBand switch
            {
                ScaleBand.Macro => Crispness.Soft,
                ScaleBand.Meso => rng.NextFloat() > 0.6f ? Crispness.Crisp : Crispness.Medium,
                _ => Crispness.Crisp
            };

            float shapeMin = scaleBand switch
            {
                ScaleBand.Macro => Lerp(120f, 260f, rng.NextFloat()),
                ScaleBand.Meso => Lerp(40f, 120f, rng.NextFloat()),
                _ => Lerp(6f, 24f, rng.NextFloat())
            };
            float shapeMax = scaleBand switch
            {
                ScaleBand.Macro => shapeMin + Lerp(80f, 160f, rng.NextFloat()),
                ScaleBand.Meso => shapeMin + Lerp(30f, 80f, rng.NextFloat()),
                _ => shapeMin + Lerp(6f, 18f, rng.NextFloat())
            };

            BlendMode blendMode = PickBlendMode(scaleBand, rng);
            float opacity = scaleBand switch
            {
                ScaleBand.Macro => Lerp(0.08f, 0.2f, rng.NextFloat()),
                ScaleBand.Meso => Lerp(0.1f, 0.28f, rng.NextFloat()),
                _ => Lerp(0.1f, 0.24f, rng.NextFloat())
            };

            float blurRadius = edgeSoftness switch
            {
                Crispness.Soft => Lerp(10f, 22f, rng.NextFloat()),
                Crispness.Medium => Lerp(4f, 10f, rng.NextFloat()),
                _ => Lerp(0.5f, 3f, rng.NextFloat())
            };

            return new LayerSpec
            {
                Id = id,
                Role = role,
                ScaleBand = scaleBand,
                GeneratorVariant = PickGeneratorVariant(family, rng),
                FrequencyOrScale = scale,
                Density = Clamp01(densityAdjusted),
                EdgeSoftness = edgeSoftness,
                ShapeSizeMin = shapeMin,
                ShapeSizeMax = shapeMax,
                PlacementRule = PickPlacementRule(family, rng),
                BlendMode = blendMode,
                Opacity = opacity,
                DepthIndex = id,
                ContrastMultiplier = Lerp(0.7f, 1.25f, id / (float)Math.Max(1, 20)),
                CrispnessModel = edgeSoftness,
                BlurRadiusOrFeather = blurRadius,
                IsGradientOnly = gradientOnly
            };
        }

        private static void EnsureCrispLayerMinimum(LayerSpec[] layers, int layerCount)
        {
            if (layerCount < 8) return;
            int crispCount = 0;
            foreach (var layer in layers)
            {
                if (layer.EdgeSoftness == Crispness.Crisp) crispCount++;
            }

            if (crispCount >= 2) return;

            for (int i = 0; i < layers.Length && crispCount < 2; i++)
            {
                if (layers[i].ScaleBand == ScaleBand.Meso && layers[i].EdgeSoftness != Crispness.Crisp)
                {
                    layers[i].EdgeSoftness = Crispness.Crisp;
                    layers[i].CrispnessModel = Crispness.Crisp;
                    layers[i].BlurRadiusOrFeather = Math.Min(layers[i].BlurRadiusOrFeather, 2f);
                    crispCount++;
                }
            }
        }

        private static MotionSpec[] BuildMotionLayers(LayerSpec[] layers, SeededRng rng)
        {
            if (layers.Length < 5 || rng.NextFloat() < 0.45f)
            {
                return Array.Empty<MotionSpec>();
            }

            int motionCount = rng.NextFloat() > 0.6f ? 2 : 1;
            var motionLayers = new List<MotionSpec>(motionCount);

            for (int i = 0; i < motionCount; i++)
            {
                int index = rng.NextInt(0, layers.Length);
                var layer = layers[index];
                MotionType motionType = (MotionType)rng.NextInt(1, 4);
                motionLayers.Add(new MotionSpec
                {
                    LayerId = layer.Id,
                    MotionType = motionType,
                    Amplitude = Lerp(2f, 18f, rng.NextFloat()),
                    PeriodSeconds = Lerp(6f, 22f, rng.NextFloat()),
                    AxisX = Lerp(-1f, 1f, rng.NextFloat()),
                    AxisY = Lerp(-1f, 1f, rng.NextFloat())
                });
            }

            return motionLayers.ToArray();
        }

        private static int PickGeneratorVariant(GeneratorFamily family, SeededRng rng)
        {
            int variantCount = family switch
            {
                GeneratorFamily.RadialPolar => 5,
                GeneratorFamily.RegularTiling => 4,
                GeneratorFamily.RecursiveSubdivision => 4,
                GeneratorFamily.NoiseField => 5,
                GeneratorFamily.WaveInterference => 4,
                GeneratorFamily.DistanceFieldShapes => 4,
                GeneratorFamily.FractalLite => 3,
                _ => 3
            };
            return rng.NextInt(0, variantCount);
        }

        private static PlacementRule PickPlacementRule(GeneratorFamily family, SeededRng rng)
        {
            return family switch
            {
                GeneratorFamily.RadialPolar => PlacementRule.Radial,
                GeneratorFamily.RegularTiling => rng.NextFloat() > 0.5f ? PlacementRule.Grid : PlacementRule.JitteredGrid,
                GeneratorFamily.RecursiveSubdivision => PlacementRule.Grid,
                GeneratorFamily.NoiseField => PlacementRule.NoiseThreshold,
                GeneratorFamily.WaveInterference => PlacementRule.NoiseThreshold,
                GeneratorFamily.DistanceFieldShapes => PlacementRule.VoronoiCenters,
                GeneratorFamily.FractalLite => PlacementRule.Grid,
                _ => PlacementRule.Grid
            };
        }

        private static BlendMode PickBlendMode(ScaleBand scaleBand, SeededRng rng)
        {
            return scaleBand switch
            {
                ScaleBand.Macro => rng.NextFloat() > 0.5f ? BlendMode.Multiply : BlendMode.SoftLight,
                ScaleBand.Meso => rng.NextFloat() > 0.5f ? BlendMode.Overlay : BlendMode.Screen,
                _ => rng.NextFloat() > 0.4f ? BlendMode.Add : BlendMode.Screen
            };
        }

        private static ZoneThemeFingerprint ComputeFingerprint(ZoneTheme theme)
        {
            int compositingMask = 0;
            foreach (var layer in theme.Layers)
            {
                compositingMask |= 1 << (int)layer.BlendMode;
            }

            return new ZoneThemeFingerprint
            {
                GeometryVocabulary = theme.GeometryVocabulary,
                PrimaryGeneratorFamily = theme.PrimaryGeneratorFamily,
                SymmetryClass = theme.SymmetryClass,
                LayerCount = theme.LayerCount,
                MotionPresence = theme.MotionLayers != null && theme.MotionLayers.Length > 0,
                MacroCount = theme.MacroCount,
                MesoCount = theme.MesoCount,
                MicroCount = theme.MicroCount,
                CompositingSignature = compositingMask
            };
        }

        private static bool IsTooSimilar(ZoneThemeFingerprint candidate, IReadOnlyList<ZoneThemeFingerprint> recent)
        {
            if (recent == null || recent.Count == 0) return false;

            foreach (var previous in recent)
            {
                int matches = 0;
                if (candidate.GeometryVocabulary == previous.GeometryVocabulary) matches++;
                if (candidate.PrimaryGeneratorFamily == previous.PrimaryGeneratorFamily) matches++;
                if (candidate.SymmetryClass == previous.SymmetryClass) matches++;
                if (candidate.LayerCount == previous.LayerCount) matches++;
                if (candidate.MotionPresence == previous.MotionPresence) matches++;
                if (candidate.MacroCount == previous.MacroCount && candidate.MesoCount == previous.MesoCount && candidate.MicroCount == previous.MicroCount) matches++;
                if (candidate.CompositingSignature == previous.CompositingSignature) matches++;

                if (matches >= 4)
                {
                    return true;
                }

                bool comboMatch = candidate.PrimaryGeneratorFamily == previous.PrimaryGeneratorFamily
                                  && candidate.SymmetryClass == previous.SymmetryClass
                                  && candidate.LayerCount == previous.LayerCount
                                  && candidate.MacroCount == previous.MacroCount
                                  && candidate.MesoCount == previous.MesoCount
                                  && candidate.MicroCount == previous.MicroCount;
                if (comboMatch)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static ulong Hash64(ulong a, ulong b)
        {
            ulong x = a + 0x9E3779B97F4A7C15ul;
            x ^= b + 0xBF58476D1CE4E5B9ul + (x << 6) + (x >> 2);
            return SplitMix64(x);
        }

        private static ulong SplitMix64(ulong x)
        {
            x += 0x9E3779B97F4A7C15ul;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9ul;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBul;
            return x ^ (x >> 31);
        }

        public struct BackgroundSignature
        {
            public int ZoneIndex;
            public long ParameterHash;

            public override bool Equals(object obj)
            {
                if (obj is BackgroundSignature other)
                {
                    return ZoneIndex == other.ZoneIndex && ParameterHash == other.ParameterHash;
                }
                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ZoneIndex * 397) ^ ParameterHash.GetHashCode();
                }
            }
        }

        private sealed class SeededRng
        {
            private ulong _state;
            private readonly int _salt;

            public SeededRng(ulong seed, int salt)
            {
                _salt = salt;
                _state = SplitMix64(seed + (ulong)salt);
                if (_state == 0)
                {
                    _state = 0xDEADBEEFCAFEBABEu;
                }
            }

            public float NextFloat()
            {
                return (NextUInt64() & 0xFFFFFFul) / 16777216f;
            }

            public float NextSignedFloat(float magnitude)
            {
                return (NextFloat() * 2f - 1f) * magnitude;
            }

            public int NextInt(int min, int max)
            {
                if (max <= min) return min;
                ulong span = (ulong)(max - min);
                return min + (int)(NextUInt64() % span);
            }

            private ulong NextUInt64()
            {
                _state = SplitMix64(_state + (ulong)_salt);
                return _state;
            }
        }
    }
}
