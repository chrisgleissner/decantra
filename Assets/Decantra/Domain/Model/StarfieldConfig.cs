/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;

namespace Decantra.Domain.Model
{
    /// <summary>
    /// Immutable configuration for the animated starfield background effect.
    /// All slider values are normalized to [Min..Max] and clamped on construction.
    /// </summary>
    public sealed class StarfieldConfig : IEquatable<StarfieldConfig>
    {
        public const float DensityMin = 0.01f;
        public const float DensityMax = 0.34f;
        public const float DensityDefault = 0.175f;

        public const float SpeedMin = 0.01f;
        public const float SpeedMax = 0.39f;
        public const float SpeedDefault = 0.20f;

        public const float BrightnessMin = 0.05f;
        public const float BrightnessMax = 1.0f;
        public const float BrightnessDefault = 0.50f;

        public const bool EnabledDefault = true;

        public bool Enabled { get; }
        public float Density { get; }
        public float Speed { get; }
        public float Brightness { get; }

        public StarfieldConfig(bool enabled, float density, float speed, float brightness)
        {
            Enabled = enabled;
            Density = Clamp(density, DensityMin, DensityMax);
            Speed = Clamp(speed, SpeedMin, SpeedMax);
            Brightness = Clamp(brightness, BrightnessMin, BrightnessMax);
        }

        /// <summary>Returns the default configuration matching the original hardcoded shader values.</summary>
        public static StarfieldConfig Default => new StarfieldConfig(EnabledDefault, DensityDefault, SpeedDefault, BrightnessDefault);

        public StarfieldConfig WithEnabled(bool enabled)
        {
            return new StarfieldConfig(enabled, Density, Speed, Brightness);
        }

        public StarfieldConfig WithDensity(float density)
        {
            return new StarfieldConfig(Enabled, density, Speed, Brightness);
        }

        public StarfieldConfig WithSpeed(float speed)
        {
            return new StarfieldConfig(Enabled, Density, speed, Brightness);
        }

        public StarfieldConfig WithBrightness(float brightness)
        {
            return new StarfieldConfig(Enabled, Density, Speed, brightness);
        }

        public bool Equals(StarfieldConfig other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Enabled == other.Enabled
                && ApproxEqual(Density, other.Density)
                && ApproxEqual(Speed, other.Speed)
                && ApproxEqual(Brightness, other.Brightness);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StarfieldConfig);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Enabled ? 1 : 0;
                hash = (hash * 397) ^ Density.GetHashCode();
                hash = (hash * 397) ^ Speed.GetHashCode();
                hash = (hash * 397) ^ Brightness.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"StarfieldConfig(Enabled={Enabled}, Density={Density:F2}, Speed={Speed:F2}, Brightness={Brightness:F2})";
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static bool ApproxEqual(float a, float b)
        {
            float diff = a - b;
            if (diff < 0f) diff = -diff;
            return diff < 0.0001f;
        }
    }
}
