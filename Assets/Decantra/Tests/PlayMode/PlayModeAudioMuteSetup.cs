/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using NUnit.Framework;
using UnityEngine;

namespace Decantra.Tests.PlayMode
{
    [SetUpFixture]
    public sealed class PlayModeAudioMuteSetup
    {
        private float _previousVolume;
        private bool _previousPause;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _previousVolume = AudioListener.volume;
            _previousPause = AudioListener.pause;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AudioListener.pause = _previousPause;
            AudioListener.volume = _previousVolume;
        }
    }
}
