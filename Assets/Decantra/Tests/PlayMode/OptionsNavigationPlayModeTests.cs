/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public sealed class OptionsNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator Options_ProvidesRequiredDestinations()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.ShowOptionsOverlay();
            yield return null;
            Assert.IsTrue(controller.IsOptionsOverlayVisible);

            controller.ShowPrivacyPolicyOverlay();
            yield return null;
            var privacyOverlay = GameObject.Find("PrivacyPolicyOverlay");
            Assert.IsNotNull(privacyOverlay);
            Assert.IsTrue(privacyOverlay.activeSelf);

            controller.HidePrivacyPolicyOverlay();
            yield return null;
            Assert.IsFalse(privacyOverlay.activeSelf);

            controller.ShowTermsOverlay();
            yield return null;
            var termsOverlay = GameObject.Find("TermsOverlay");
            Assert.IsNotNull(termsOverlay);
            Assert.IsTrue(termsOverlay.activeSelf);

            controller.HideTermsOverlay();
            yield return null;
            Assert.IsFalse(termsOverlay.activeSelf);
        }

        [UnityTest]
        public IEnumerator ReplayTutorial_HidesOptionsAndShowsTutorialOverlay()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.ShowOptionsOverlay();
            yield return null;
            controller.ReplayTutorial();
            yield return null;

            Assert.IsFalse(controller.IsOptionsOverlayVisible, "Options should close when replay tutorial starts.");
            var tutorialOverlay = GameObject.Find("TutorialOverlay");
            Assert.IsNotNull(tutorialOverlay);
            Assert.IsTrue(tutorialOverlay.activeSelf, "Tutorial overlay should be visible after replay request.");
        }

        [UnityTest]
        public IEnumerator LegalOverlays_AreScrollable()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var privacyOverlay = FindGameObjectByNameIncludingInactive("PrivacyPolicyOverlay");
            var termsOverlay = FindGameObjectByNameIncludingInactive("TermsOverlay");

            Assert.IsNotNull(privacyOverlay);
            Assert.IsNotNull(termsOverlay);

            var privacyScroll = privacyOverlay.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            var termsScroll = termsOverlay.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);

            Assert.IsNotNull(privacyScroll, "Privacy Policy overlay should contain a ScrollRect.");
            Assert.IsNotNull(termsScroll, "Terms overlay should contain a ScrollRect.");
        }

        private static GameObject FindGameObjectByNameIncludingInactive(string name)
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go == null) continue;
                if (go.name != name) continue;
                if (go.hideFlags != HideFlags.None) continue;
                return go;
            }

            return null;
        }
    }
}
