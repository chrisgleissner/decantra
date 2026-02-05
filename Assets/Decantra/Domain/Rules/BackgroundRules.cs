/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Rules
{
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
        private const int LevelVariantSalt = 0x1F3D5B79;
        private const int LevelsPerZone = 10;
        private const int Zone0Size = 9;

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

        public static LevelVariant GetLevelVariant(int levelIndex, int globalSeed, int paletteCount)
        {
            if (paletteCount <= 0) throw new ArgumentOutOfRangeException(nameof(paletteCount));
            int zoneIndex = GetZoneIndex(levelIndex);
            ulong levelSeed = GetLevelSeed(globalSeed, levelIndex);
            var rng = new SeededRng(levelSeed, LevelVariantSalt ^ 0x62B9D3E7);

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
