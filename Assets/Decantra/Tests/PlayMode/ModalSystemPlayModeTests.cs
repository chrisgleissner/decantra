/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    public sealed class ModalSystemPlayModeTests
    {
        [UnityTest]
        public IEnumerator Modals_HiddenByDefault_AndNonBlockingAtStartup()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            Assert.IsFalse(controller.IsOptionsOverlayVisible, "Options modal should be hidden on startup.");
            Assert.IsFalse(controller.IsHowToPlayOverlayVisible, "How To Play modal should be hidden on startup.");

            var tutorialOverlay = FindGameObjectByNameIncludingInactive("TutorialOverlay");
            Assert.IsNotNull(tutorialOverlay);
            Assert.IsFalse(tutorialOverlay.activeSelf, "Tutorial overlay should be hidden by default.");

            var privacyOverlay = FindGameObjectByNameIncludingInactive("PrivacyPolicyOverlay");
            Assert.IsNotNull(privacyOverlay);
            Assert.IsFalse(privacyOverlay.activeSelf, "Privacy overlay should be hidden by default.");

            var termsOverlay = FindGameObjectByNameIncludingInactive("TermsOverlay");
            Assert.IsNotNull(termsOverlay);
            Assert.IsFalse(termsOverlay.activeSelf, "Terms overlay should be hidden by default.");

            var starDialog = GetPrivateField<StarTradeInDialog>(controller, "starTradeInDialog");
            Assert.IsNotNull(starDialog);
            Assert.IsFalse(starDialog.IsVisible, "Star Trade-In should be hidden by default.");

            var starCanvasGroup = GetPrivateField<CanvasGroup>(starDialog, "canvasGroup");
            Assert.IsNotNull(starCanvasGroup);
            Assert.IsFalse(starCanvasGroup.blocksRaycasts, "Hidden Star Trade-In should not block input.");
        }

        [UnityTest]
        public IEnumerator OptionsAndLegalModals_UseSharedBaseModalAndScrollLayouts()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var optionsOverlay = FindGameObjectByNameIncludingInactive("OptionsOverlay");
            Assert.IsNotNull(optionsOverlay);
            Assert.IsNotNull(optionsOverlay.GetComponent<BaseModal>(), "Options should use BaseModal.");

            var optionsPanel = optionsOverlay.transform.Find("Panel");
            Assert.IsNotNull(optionsPanel);
            Assert.IsNotNull(optionsPanel.GetComponent<ResponsiveModalPanel>(), "Options panel should be responsive.");

            var listContainer = optionsOverlay.transform.Find("Panel/ListContainer");
            Assert.IsNotNull(listContainer);
            Assert.IsNotNull(listContainer.GetComponent<ScrollRect>(), "Options content should be vertically scrollable.");
            Assert.IsNotNull(optionsOverlay.transform.Find("Panel/ListContainer/Viewport/Content/GameplaySection"));
            Assert.IsNotNull(optionsOverlay.transform.Find("Panel/ListContainer/Viewport/Content/LegalSection"));

            var privacyOverlay = FindGameObjectByNameIncludingInactive("PrivacyPolicyOverlay");
            var termsOverlay = FindGameObjectByNameIncludingInactive("TermsOverlay");
            Assert.IsNotNull(privacyOverlay);
            Assert.IsNotNull(termsOverlay);

            Assert.IsNotNull(privacyOverlay.GetComponent<BaseModal>(), "Privacy should use BaseModal.");
            Assert.IsNotNull(termsOverlay.GetComponent<BaseModal>(), "Terms should use BaseModal.");

            Assert.IsNotNull(privacyOverlay.GetComponentInChildren<ScrollRect>(true), "Privacy should contain ScrollRect.");
            Assert.IsNotNull(termsOverlay.GetComponentInChildren<ScrollRect>(true), "Terms should contain ScrollRect.");
        }

        [UnityTest]
        public IEnumerator TutorialAndStarModals_UseResponsiveAndScrollableStructures()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var tutorialOverlay = FindGameObjectByNameIncludingInactive("TutorialOverlay");
            Assert.IsNotNull(tutorialOverlay);
            Assert.IsNotNull(tutorialOverlay.GetComponent<BaseModal>(), "Tutorial should use BaseModal.");

            var instructionPanel = tutorialOverlay.transform.Find("InstructionPanel");
            Assert.IsNotNull(instructionPanel);
            Assert.IsNotNull(instructionPanel.GetComponent<ResponsiveModalPanel>(), "Tutorial panel should be responsive.");
            Assert.IsNotNull(instructionPanel.transform.Find("ButtonsRow/SkipButton"));
            Assert.IsNotNull(instructionPanel.transform.Find("ButtonsRow/NextButton"));

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);
            var starDialog = GetPrivateField<StarTradeInDialog>(controller, "starTradeInDialog");
            Assert.IsNotNull(starDialog);

            var selectionRoot = GetPrivateField<GameObject>(starDialog, "selectionRoot");
            Assert.IsNotNull(selectionRoot);
            var selectionScroll = selectionRoot.transform.Find("SelectionScrollView");
            Assert.IsNotNull(selectionScroll);
            Assert.IsNotNull(selectionScroll.GetComponent<ScrollRect>(), "Star selection should scroll when needed.");

            var confirmButton = GetPrivateField<Button>(starDialog, "confirmButton");
            var cancelButton = GetPrivateField<Button>(starDialog, "cancelButton");
            Assert.IsNotNull(confirmButton);
            Assert.IsNotNull(cancelButton);
            Assert.AreNotEqual(
                confirmButton.GetComponent<Image>()?.color,
                cancelButton.GetComponent<Image>()?.color,
                "Confirm and cancel actions should be visually distinct.");
        }

        [UnityTest]
        public IEnumerator LegalModalLifecycle_DismissesCleanlyWithoutLingeringOverlays()
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
            var privacyOverlay = FindGameObjectByNameIncludingInactive("PrivacyPolicyOverlay");
            Assert.IsNotNull(privacyOverlay);
            Assert.IsTrue(privacyOverlay.activeSelf);

            controller.HidePrivacyPolicyOverlay();
            yield return null;
            Assert.IsFalse(privacyOverlay.activeSelf, "Privacy overlay should dismiss cleanly.");

            controller.ShowTermsOverlay();
            yield return null;
            var termsOverlay = FindGameObjectByNameIncludingInactive("TermsOverlay");
            Assert.IsNotNull(termsOverlay);
            Assert.IsTrue(termsOverlay.activeSelf);

            controller.HideOptionsOverlay();
            yield return null;

            Assert.IsFalse(controller.IsOptionsOverlayVisible, "Options should be hidden after dismissal.");
            Assert.IsFalse(termsOverlay.activeSelf, "Terms should be hidden when Options closes.");
            Assert.IsFalse(privacyOverlay.activeSelf, "Privacy should remain hidden after Options closes.");
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static GameObject FindGameObjectByNameIncludingInactive(string name)
        {
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                var go = allObjects[i];
                if (go == null || go.hideFlags != HideFlags.None || go.name != name)
                {
                    continue;
                }

                return go;
            }

            return null;
        }
    }
}
