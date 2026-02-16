/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        public IEnumerator ReplayTutorial_SeparatesLevelAndMovesSteps_AndKeepsTextWithinContainer()
        {
            SceneBootstrap.EnsureScene();
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller);

            controller.ReplayTutorial();
            yield return null;

            var tutorialManager = Object.FindFirstObjectByType<TutorialManager>();
            Assert.IsNotNull(tutorialManager);

            var steps = GetPrivateField<List<TutorialStepData>>(tutorialManager, "_steps");
            Assert.IsNotNull(steps);

            int levelStepIndex = steps.FindIndex(step => step.Id == "goal");
            int movesStepIndex = steps.FindIndex(step => step.Id == "moves");
            Assert.GreaterOrEqual(levelStepIndex, 0, "LEVEL step should exist.");
            Assert.AreEqual(levelStepIndex + 1, movesStepIndex, "MOVES step should directly follow LEVEL step.");

            var levelStep = steps[levelStepIndex];
            var movesStep = steps[movesStepIndex];
            Assert.AreEqual("LevelPanel", levelStep.TargetObjectName);
            Assert.AreEqual("MovesPanel", movesStep.TargetObjectName);
            Assert.AreEqual("LEVEL & Difficulty\nDisplays the current level.\nThe three circles show difficulty. The more filled they are, the harder the level.", levelStep.Instruction);
            Assert.AreEqual("MOVES\nCurrent moves versus allowed moves.\nFewer moves give a higher score.", movesStep.Instruction);
            StringAssert.DoesNotContain("MOVES", levelStep.Instruction);

            var instructionText = GetPrivateField<Text>(tutorialManager, "instructionText");
            Assert.IsNotNull(instructionText);
            var textRect = instructionText.GetComponent<RectTransform>();
            Assert.IsNotNull(textRect);

            Canvas.ForceUpdateCanvases();
            yield return null;

            AssertInstructionFits(instructionText, textRect, levelStep.Instruction);
            AssertInstructionFits(instructionText, textRect, movesStep.Instruction);
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

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} was not found on {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static void AssertInstructionFits(Text instructionText, RectTransform textRect, string content)
        {
            var generationSettings = instructionText.GetGenerationSettings(new Vector2(textRect.rect.width, 0f));
            float preferredHeight = instructionText.cachedTextGeneratorForLayout.GetPreferredHeight(content, generationSettings) / instructionText.pixelsPerUnit;
            Assert.LessOrEqual(preferredHeight, textRect.rect.height, $"Instruction text overflowed container. content={content}");
        }
    }
}
