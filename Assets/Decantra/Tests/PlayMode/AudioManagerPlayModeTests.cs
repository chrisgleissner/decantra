/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Reflection;
using Decantra.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.PlayMode.Tests
{
    public sealed class AudioManagerPlayModeTests
    {
        [UnityTest]
        public IEnumerator AssetClips_AreLoaded()
        {
            var host = new GameObject("AudioManagerTestHost");
            var manager = host.AddComponent<AudioManager>();
            yield return null;

            var buttonClip = (AudioClip)GetPrivateField(manager, "_buttonClickClip");
            var levelCompleteClip = (AudioClip)GetPrivateField(manager, "_levelCompleteClip");
            var bottleFullClip = (AudioClip)GetPrivateField(manager, "_bottleFullClip");
            var stageUnlockedClip = (AudioClip)GetPrivateField(manager, "_stageUnlockedClip");
            var pourClips = (AudioClip[])GetPrivateField(manager, "_pourClips");

            Assert.NotNull(buttonClip, "Expected button-click clip to load.");
            Assert.NotNull(levelCompleteClip, "Expected level-complete clip to load.");
            Assert.NotNull(bottleFullClip, "Expected bottle-full clip to load.");
            Assert.NotNull(stageUnlockedClip, "Expected stage-unlocked clip to load.");
            Assert.NotNull(pourClips);
            Assert.AreEqual(2, pourClips.Length, "Expected exactly two pour clip variants.");
            Assert.NotNull(pourClips[0]);
            Assert.NotNull(pourClips[1]);

            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator PourSelection_IsDeterministicPerLevelSeed()
        {
            var host = new GameObject("AudioManagerSelectionHost");
            var manager = host.AddComponent<AudioManager>();
            yield return null;

            manager.SelectPourClipForLevel(12, 34567);
            var first = (AudioClip)GetPrivateField(manager, "_selectedPourClip");

            manager.SelectPourClipForLevel(12, 34567);
            var second = (AudioClip)GetPrivateField(manager, "_selectedPourClip");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.AreEqual(first, second, "Pour clip selection must be stable for the same level/seed.");

            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator Playback_UsesFixedAudioSourcePool()
        {
            var host = new GameObject("AudioManagerPoolHost");
            var manager = host.AddComponent<AudioManager>();
            yield return null;

            int initialSources = host.GetComponents<AudioSource>().Length;
            Assert.Greater(initialSources, 0);

            manager.SelectPourClipForLevel(3, 1001);

            for (int i = 0; i < 24; i++)
            {
                manager.PlayButtonClick();
                manager.PlayPourSegment(i / 24f, (i + 1) / 24f);
                manager.PlayLevelComplete();
                manager.PlayBottleFull();
                manager.PlayStageUnlocked();
            }

            int finalSources = host.GetComponents<AudioSource>().Length;
            Assert.AreEqual(initialSources, finalSources, "Audio source pool size changed during repeated playback.");

            Object.Destroy(host);
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            Assert.NotNull(instance);
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field '{fieldName}'.");
            return field.GetValue(instance);
        }

    }
}
