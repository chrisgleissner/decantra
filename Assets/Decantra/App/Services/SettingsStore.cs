using UnityEngine;

namespace Decantra.App.Services
{
    public sealed class SettingsStore
    {
        private const string SfxEnabledKey = "decantra.sfx.enabled";

        public bool LoadSfxEnabled()
        {
            return PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
        }

        public void SaveSfxEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(SfxEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
