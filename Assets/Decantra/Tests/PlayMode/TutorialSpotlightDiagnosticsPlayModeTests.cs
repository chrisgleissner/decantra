using System.Collections;
using System.Reflection;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public sealed class TutorialSpotlightDiagnosticsPlayModeTests
    {
        private const float SpotlightReadyTimeoutSeconds = 2.5f;

        [UnityTest]
        public IEnumerator TutorialDiagnostics_AreAvailableAndSpotlightVisible()
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            Assert.IsNotNull(controller, "GameController not found after SceneBootstrap.EnsureScene().");

            var tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(tutorialManager, "TutorialManager not found after scene bootstrap.");

            controller.ReplayTutorial();

            float elapsed = 0f;
            while (!tutorialManager.IsRunning && elapsed < SpotlightReadyTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(tutorialManager.IsRunning, "Tutorial did not enter running state within timeout.");

            var stepMethod = tutorialManager.GetType().GetMethod("TryGetCurrentStepSnapshot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(stepMethod, "TryGetCurrentStepSnapshot method not found via reflection.");
            var stepArgs = new object[] { 0, string.Empty };
            var stepOk = (bool)stepMethod.Invoke(tutorialManager, stepArgs);
            Assert.IsTrue(stepOk, "TryGetCurrentStepSnapshot returned false.");
            Assert.GreaterOrEqual((int)stepArgs[0], 0, "Step index must be non-negative.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(stepArgs[1]?.ToString()), "Target name should be available for current tutorial step.");

            var diagnosticsMethod = tutorialManager.GetType().GetMethod("TryGetRenderDiagnostics", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(diagnosticsMethod, "TryGetRenderDiagnostics method not found via reflection.");
            var diagnosticsArgs = new object[] { null };
            var diagnosticsOk = (bool)diagnosticsMethod.Invoke(tutorialManager, diagnosticsArgs);
            Assert.IsTrue(diagnosticsOk, "TryGetRenderDiagnostics returned false.");
            Assert.IsNotNull(diagnosticsArgs[0], "Diagnostics payload should not be null.");

            object payload = diagnosticsArgs[0];
            string renderMode = ReadMember(payload, "RenderMode", "unknown");
            string scaleMode = ReadMember(payload, "ScaleMode", "unknown");
            bool spotlightVisible = ReadMember(payload, "SpotlightVisible", false);
            bool spotlightMaskActive = ReadMember(payload, "SpotlightMaskActive", false);
            Rect spotlightRect = ReadMember(payload, "SpotlightRectLocal", default(Rect));
            Rect canvasRect = ReadMember(payload, "CanvasRectLocal", default(Rect));

            Assert.AreNotEqual("unknown", renderMode, "Render diagnostics fell back to unknown render mode.");
            Assert.AreNotEqual("unknown", scaleMode, "Render diagnostics fell back to unknown scale mode.");
            Assert.IsTrue(spotlightVisible, "Spotlight should be visible for active tutorial step.");
            Assert.IsTrue(spotlightMaskActive, "Spotlight mask should be active for active tutorial step.");
            Assert.Greater(spotlightRect.width, 1f, "Spotlight width must be > 1 px.");
            Assert.Greater(spotlightRect.height, 1f, "Spotlight height must be > 1 px.");
            Assert.Greater(canvasRect.width, 1f, "Canvas width must be > 1 px.");
            Assert.Greater(canvasRect.height, 1f, "Canvas height must be > 1 px.");
        }

        private static T ReadMember<T>(object source, string memberName, T fallback)
        {
            if (source == null)
            {
                return fallback;
            }

            var type = source.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                object value = property.GetValue(source);
                if (value is T typed)
                {
                    return typed;
                }
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                object value = field.GetValue(source);
                if (value is T typed)
                {
                    return typed;
                }
            }

            return fallback;
        }
    }
}
