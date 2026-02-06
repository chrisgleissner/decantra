/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.Domain.Model;
using UnityEngine;

namespace Decantra.App.Services
{
    public sealed class SettingsStore
    {
        private const string SfxEnabledKey = "decantra.sfx.enabled";
        private const string StarfieldEnabledKey = "decantra.starfield.enabled";
        private const string StarfieldDensityKey = "decantra.starfield.density";
        private const string StarfieldSpeedKey = "decantra.starfield.speed";
        private const string StarfieldBrightnessKey = "decantra.starfield.brightness";

        public bool LoadSfxEnabled()
        {
            return PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
        }

        public void SaveSfxEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public StarfieldConfig LoadStarfieldConfig()
        {
            bool enabled = PlayerPrefs.GetInt(StarfieldEnabledKey, StarfieldConfig.EnabledDefault ? 1 : 0) == 1;
            float density = PlayerPrefs.GetFloat(StarfieldDensityKey, StarfieldConfig.DensityDefault);
            float speed = PlayerPrefs.GetFloat(StarfieldSpeedKey, StarfieldConfig.SpeedDefault);
            float brightness = PlayerPrefs.GetFloat(StarfieldBrightnessKey, StarfieldConfig.BrightnessDefault);
            return new StarfieldConfig(enabled, density, speed, brightness);
        }

        public void SaveStarfieldConfig(StarfieldConfig config)
        {
            if (config == null) return;
            PlayerPrefs.SetInt(StarfieldEnabledKey, config.Enabled ? 1 : 0);
            PlayerPrefs.SetFloat(StarfieldDensityKey, config.Density);
            PlayerPrefs.SetFloat(StarfieldSpeedKey, config.Speed);
            PlayerPrefs.SetFloat(StarfieldBrightnessKey, config.Brightness);
            PlayerPrefs.Save();
        }
    }
}
