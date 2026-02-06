/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using Decantra.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public class OrientationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneBootstrap_DoesNotForceScreenOrientation()
        {
            var initial = Screen.orientation;
            var probe = initial == ScreenOrientation.LandscapeLeft
                ? ScreenOrientation.LandscapeRight
                : ScreenOrientation.LandscapeLeft;

            Screen.orientation = probe;
            yield return null;

            if (Screen.orientation != probe)
            {
                Screen.orientation = initial;
                Assert.Ignore("Screen.orientation is not controllable in this environment.");
            }

            SceneBootstrap.EnsureScene();
            yield return null;

            Assert.AreEqual(probe, Screen.orientation, "Scene bootstrap must not override Screen.orientation.");
            Screen.orientation = initial;
        }
    }
}
