/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

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
