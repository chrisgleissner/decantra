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
        public IEnumerator GeneratedClips_AreHardenedAgainstClicksAndBias()
        {
            var host = new GameObject("AudioManagerTestHost");
            var manager = host.AddComponent<AudioManager>();
            yield return null;

            var buttonClip = (AudioClip)GetPrivateField(manager, "_buttonClickClip");
            var levelCompleteClip = (AudioClip)GetPrivateField(manager, "_levelCompleteClip");
            var pourClips = (AudioClip[])GetPrivateField(manager, "_pourClips");

            AssertClipIsSafe(buttonClip);
            AssertClipIsSafe(levelCompleteClip);
            Assert.NotNull(pourClips);
            Assert.Greater(pourClips.Length, 0);
            AssertClipIsSafe(pourClips[0]);
            AssertClipIsSafe(pourClips[pourClips.Length / 2]);
            AssertClipIsSafe(pourClips[pourClips.Length - 1]);

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

            for (int i = 0; i < 24; i++)
            {
                manager.PlayButtonClick();
                manager.PlayPour(i / 23f);
                manager.PlayLevelComplete();
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

        private static void AssertClipIsSafe(AudioClip clip)
        {
            Assert.NotNull(clip, "Expected clip to be generated.");
            int totalSamples = clip.samples * clip.channels;
            Assert.Greater(totalSamples, 2);
            var data = new float[totalSamples];
            Assert.IsTrue(clip.GetData(data, 0), $"Failed reading clip data for '{clip.name}'.");

            Assert.AreEqual(0f, data[0], 1e-6f, $"Clip '{clip.name}' must start at zero.");
            Assert.AreEqual(0f, data[data.Length - 1], 1e-6f, $"Clip '{clip.name}' must end at zero.");

            float mean = 0f;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                mean += data[i];
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }

            mean /= data.Length;
            Assert.Less(Mathf.Abs(mean), 0.002f, $"Clip '{clip.name}' has DC offset {mean}.");
            Assert.LessOrEqual(peak, 1f, $"Clip '{clip.name}' clips at peak {peak}.");
        }
    }
}
