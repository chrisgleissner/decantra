/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Model;
using NUnit.Framework;

namespace Decantra.Tests.EditMode
{
    public sealed class StarfieldConfigTests
    {
        // --- Defaults ---

        [Test]
        public void Default_HasExpectedValues()
        {
            var config = StarfieldConfig.Default;
            Assert.IsTrue(config.Enabled);
            Assert.AreEqual(StarfieldConfig.DensityDefault, config.Density, 0.0001f);
            Assert.AreEqual(StarfieldConfig.SpeedDefault, config.Speed, 0.0001f);
            Assert.AreEqual(StarfieldConfig.BrightnessDefault, config.Brightness, 0.0001f);
        }

        [Test]
        public void Default_EnabledIsTrue()
        {
            Assert.IsTrue(StarfieldConfig.Default.Enabled);
        }

        // --- Clamping ---

        [Test]
        public void Density_ClampedToMin_WhenBelowRange()
        {
            var config = new StarfieldConfig(true, -5f, 0.5f, 0.5f);
            Assert.AreEqual(StarfieldConfig.DensityMin, config.Density, 0.0001f);
        }

        [Test]
        public void Density_ClampedToMax_WhenAboveRange()
        {
            var config = new StarfieldConfig(true, 99f, 0.5f, 0.5f);
            Assert.AreEqual(StarfieldConfig.DensityMax, config.Density, 0.0001f);
        }

        [Test]
        public void Speed_ClampedToMin_WhenBelowRange()
        {
            var config = new StarfieldConfig(true, 0.5f, -1f, 0.5f);
            Assert.AreEqual(StarfieldConfig.SpeedMin, config.Speed, 0.0001f);
        }

        [Test]
        public void Speed_ClampedToMax_WhenAboveRange()
        {
            var config = new StarfieldConfig(true, 0.5f, 100f, 0.5f);
            Assert.AreEqual(StarfieldConfig.SpeedMax, config.Speed, 0.0001f);
        }

        [Test]
        public void Brightness_ClampedToMin_WhenBelowRange()
        {
            var config = new StarfieldConfig(true, 0.5f, 0.5f, -0.1f);
            Assert.AreEqual(StarfieldConfig.BrightnessMin, config.Brightness, 0.0001f);
        }

        [Test]
        public void Brightness_ClampedToMax_WhenAboveRange()
        {
            var config = new StarfieldConfig(true, 0.5f, 0.5f, 5f);
            Assert.AreEqual(StarfieldConfig.BrightnessMax, config.Brightness, 0.0001f);
        }

        [Test]
        public void AllValues_ClampedSimultaneously()
        {
            var config = new StarfieldConfig(false, -1f, -1f, -1f);
            Assert.IsFalse(config.Enabled);
            Assert.AreEqual(StarfieldConfig.DensityMin, config.Density, 0.0001f);
            Assert.AreEqual(StarfieldConfig.SpeedMin, config.Speed, 0.0001f);
            Assert.AreEqual(StarfieldConfig.BrightnessMin, config.Brightness, 0.0001f);
        }

        // --- With* methods ---

        [Test]
        public void WithEnabled_ReturnsNewInstanceWithUpdatedValue()
        {
            var original = StarfieldConfig.Default;
            var modified = original.WithEnabled(false);
            Assert.IsFalse(modified.Enabled);
            Assert.IsTrue(original.Enabled, "Original must not be mutated");
            Assert.AreEqual(original.Density, modified.Density, 0.0001f);
            Assert.AreEqual(original.Speed, modified.Speed, 0.0001f);
            Assert.AreEqual(original.Brightness, modified.Brightness, 0.0001f);
        }

        [Test]
        public void WithDensity_ClampsAndReturnsNewInstance()
        {
            var config = StarfieldConfig.Default.WithDensity(2.0f);
            Assert.AreEqual(StarfieldConfig.DensityMax, config.Density, 0.0001f);
        }

        [Test]
        public void WithSpeed_ClampsAndReturnsNewInstance()
        {
            var config = StarfieldConfig.Default.WithSpeed(0.0f);
            Assert.AreEqual(StarfieldConfig.SpeedMin, config.Speed, 0.0001f);
        }

        [Test]
        public void WithBrightness_ClampsAndReturnsNewInstance()
        {
            var config = StarfieldConfig.Default.WithBrightness(0.01f);
            Assert.AreEqual(StarfieldConfig.BrightnessMin, config.Brightness, 0.0001f);
        }

        // --- Equality ---

        [Test]
        public void Equals_TrueForIdenticalConfigs()
        {
            var a = new StarfieldConfig(true, 0.5f, 0.5f, 0.5f);
            var b = new StarfieldConfig(true, 0.5f, 0.5f, 0.5f);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equals_FalseWhenEnabledDiffers()
        {
            var a = new StarfieldConfig(true, 0.5f, 0.5f, 0.5f);
            var b = new StarfieldConfig(false, 0.5f, 0.5f, 0.5f);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_FalseWhenDensityDiffers()
        {
            var a = new StarfieldConfig(true, 0.3f, 0.5f, 0.5f);
            var b = new StarfieldConfig(true, 0.7f, 0.5f, 0.5f);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_FalseForNull()
        {
            var a = StarfieldConfig.Default;
            Assert.IsFalse(a.Equals(null));
        }

        [Test]
        public void ToString_ContainsAllValues()
        {
            var config = new StarfieldConfig(true, 0.5f, 0.4f, 0.6f);
            string s = config.ToString();
            Assert.IsTrue(s.Contains("Enabled=True"));
            Assert.IsTrue(s.Contains("Density=0.50"));
            Assert.IsTrue(s.Contains("Speed=0.40"));
            Assert.IsTrue(s.Contains("Brightness=0.60"));
        }

        // --- Boundary values ---

        [Test]
        public void ExactMinValues_Accepted()
        {
            var config = new StarfieldConfig(true, StarfieldConfig.DensityMin, StarfieldConfig.SpeedMin, StarfieldConfig.BrightnessMin);
            Assert.AreEqual(StarfieldConfig.DensityMin, config.Density, 0.0001f);
            Assert.AreEqual(StarfieldConfig.SpeedMin, config.Speed, 0.0001f);
            Assert.AreEqual(StarfieldConfig.BrightnessMin, config.Brightness, 0.0001f);
        }

        [Test]
        public void ExactMaxValues_Accepted()
        {
            var config = new StarfieldConfig(true, StarfieldConfig.DensityMax, StarfieldConfig.SpeedMax, StarfieldConfig.BrightnessMax);
            Assert.AreEqual(StarfieldConfig.DensityMax, config.Density, 0.0001f);
            Assert.AreEqual(StarfieldConfig.SpeedMax, config.Speed, 0.0001f);
            Assert.AreEqual(StarfieldConfig.BrightnessMax, config.Brightness, 0.0001f);
        }

        [Test]
        public void MidRangeValues_PreservedExactly()
        {
            var config = new StarfieldConfig(true, 0.42f, 0.73f, 0.18f);
            Assert.AreEqual(0.42f, config.Density, 0.0001f);
            Assert.AreEqual(0.73f, config.Speed, 0.0001f);
            Assert.AreEqual(0.18f, config.Brightness, 0.0001f);
        }
    }
}
