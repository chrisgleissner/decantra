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
        private const string SfxVolumeKey = "decantra.sfx.volume";
        private const string TutorialCompletedKey = "decantra.tutorial.completed";
        private const string HighContrastEnabledKey = "decantra.accessibility.highcontrast";
        private const string AccessibleColorsEnabledKey = "decantra.accessibility.colorblind";
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

        public float LoadSfxVolume01()
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        }

        public void SaveSfxVolume01(float volume)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(volume));
            PlayerPrefs.Save();
        }

        public bool LoadTutorialCompleted()
        {
            return PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1;
        }

        public void SaveTutorialCompleted(bool completed)
        {
            PlayerPrefs.SetInt(TutorialCompletedKey, completed ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool LoadHighContrastEnabled()
        {
            return PlayerPrefs.GetInt(HighContrastEnabledKey, 0) == 1;
        }

        public void SaveHighContrastEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(HighContrastEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool LoadAccessibleColorsEnabled()
        {
            return PlayerPrefs.GetInt(AccessibleColorsEnabledKey, 0) == 1;
        }

        public void SaveAccessibleColorsEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(AccessibleColorsEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool LoadColorBlindAssistEnabled()
        {
            return LoadAccessibleColorsEnabled();
        }

        public void SaveColorBlindAssistEnabled(bool enabled)
        {
            SaveAccessibleColorsEnabled(enabled);
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
