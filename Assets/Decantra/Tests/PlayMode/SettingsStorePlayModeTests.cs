/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using Decantra.App.Services;
using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.PlayMode
{
    public sealed class SettingsStorePlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        [Test]
        public void SfxSettings_RoundTripAndClamp()
        {
            var store = new SettingsStore();

            store.SaveSfxEnabled(false);
            store.SaveSfxVolume01(1.25f);

            Assert.IsFalse(store.LoadSfxEnabled());
            Assert.AreEqual(1f, store.LoadSfxVolume01(), 0.0001f);

            store.SaveSfxVolume01(-0.25f);
            Assert.AreEqual(0f, store.LoadSfxVolume01(), 0.0001f);
        }

        [Test]
        public void TutorialAndAccessibilitySettings_RoundTrip()
        {
            var store = new SettingsStore();

            store.SaveTutorialCompleted(true);
            store.SaveHighContrastEnabled(true);
            store.SaveColorBlindAssistEnabled(true);

            Assert.IsTrue(store.LoadTutorialCompleted());
            Assert.IsTrue(store.LoadHighContrastEnabled());
            Assert.IsTrue(store.LoadColorBlindAssistEnabled());
        }
    }
}
