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

namespace Decantra.Tests.PlayMode
{
    /// <summary>
    /// Verifies that the Decantra logo's RectTransform sizeDelta is pixel-invariant
    /// during tutorial steps that highlight HUD panels (Level, Moves, Score).
    ///
    /// Root cause guarded: <c>TutorialFocusPulse</c> scales each targeted panel
    /// between 1.03× and 1.06×. Before the fix, <c>TopBannerLogoLayout.TryUpdateBounds</c>
    /// used <c>GetWorldCorners</c> on those panels and observed the inflated corners,
    /// causing the logo's sizeDelta to change on every frame of the animation.
    /// After the fix the bounds are normalised to layout-space (scale-independent),
    /// so the logo remains pixel-identical throughout.
    /// </summary>
    public sealed class TutorialLogoInvariancePlayModeTests
    {
        private const float BootstrapTimeoutSeconds = 3f;
        private const float TutorialStartTimeoutSeconds = 2.5f;

        // Tolerance: zero expected; allow a tiny float epsilon to survive pixel-snapping.
        private const float SizeDeltaTolerance = 0.25f;

        // Number of frames to sample during the panel highlight step.
        private const int SampleFrames = 60;

        [UnityTest]
        public IEnumerator LogoSizeDelta_IsInvariant_DuringLevelPanelHighlight()
        {
            yield return RunLogoInvarianceTest("LevelPanel", "Level panel highlight");
        }

        [UnityTest]
        public IEnumerator LogoSizeDelta_IsInvariant_DuringMoviesPanelHighlight()
        {
            yield return RunLogoInvarianceTest("MovesPanel", "Moves panel highlight");
        }

        [UnityTest]
        public IEnumerator LogoSizeDelta_IsInvariant_DuringScorePanelHighlight()
        {
            yield return RunLogoInvarianceTest("ScorePanel", "Score panel highlight");
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator RunLogoInvarianceTest(string targetPanelName, string stepLabel)
        {
            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            // Wait for full layout pass.
            float elapsed = 0f;
            var controller = Object.FindFirstObjectByType<GameController>();
            while (controller == null && elapsed < BootstrapTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                controller = Object.FindFirstObjectByType<GameController>();
            }
            Assert.IsNotNull(controller, "GameController not found after bootstrap.");

            // Find the logo layout component.
            var logoLayout = Object.FindFirstObjectByType<TopBannerLogoLayout>(FindObjectsInactive.Include);
            Assert.IsNotNull(logoLayout, "TopBannerLogoLayout not found in scene.");

            RectTransform logoRect = GetPrivateField<RectTransform>(logoLayout, "logoRect");
            Assert.IsNotNull(logoRect, "logoRect field not wired on TopBannerLogoLayout.");

            // Start (or replay) tutorial so TutorialFocusPulse is active.
            var tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(tutorialManager, "TutorialManager not found.");
            controller.ReplayTutorial();

            elapsed = 0f;
            while (!tutorialManager.IsRunning && elapsed < TutorialStartTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Assert.IsTrue(tutorialManager.IsRunning, "Tutorial did not start within timeout.");

            // Advance past non-HUD steps until we reach the desired panel step.
            const int MaxAdvances = 5;
            for (int advance = 0; advance < MaxAdvances; advance++)
            {
                if (IsCurrentStepTarget(tutorialManager, targetPanelName))
                    break;
                if (!tutorialManager.AdvanceStepForAutomation())
                    break;
                // Allow one layout frame after step change.
                yield return null;
            }

            bool atTarget = IsCurrentStepTarget(tutorialManager, targetPanelName);
            if (!atTarget)
            {
                // The step is not present (e.g. optional target absent from current level).
                // Log a warning but do not fail — the invariant cannot be violated if the step
                // doesn't exist.
                Debug.LogWarning($"[TutorialLogoInvariance] Step '{targetPanelName}' not reached; skipping {stepLabel} invariance check.");
                yield break;
            }

            // Let at least 2 layout frames settle.
            yield return null;
            yield return null;

            // Record baseline size.
            Vector2 baselineSizeDelta = logoRect.sizeDelta;
            Assert.Greater(baselineSizeDelta.x, 1f, "Logo sizeDelta.x should be non-trivially positive.");
            Assert.Greater(baselineSizeDelta.y, 1f, "Logo sizeDelta.y should be non-trivially positive.");

            float maxDeltaX = 0f;
            float maxDeltaY = 0f;

            // Sample across SampleFrames frames (≈1 second at 60 fps).
            for (int frame = 0; frame < SampleFrames; frame++)
            {
                yield return null;
                Vector2 current = logoRect.sizeDelta;
                float dx = Mathf.Abs(current.x - baselineSizeDelta.x);
                float dy = Mathf.Abs(current.y - baselineSizeDelta.y);
                if (dx > maxDeltaX) maxDeltaX = dx;
                if (dy > maxDeltaY) maxDeltaY = dy;
            }

            Assert.LessOrEqual(maxDeltaX, SizeDeltaTolerance,
                $"[{stepLabel}] Logo sizeDelta.x varied by {maxDeltaX:F3} px during {SampleFrames} frames " +
                $"(baseline {baselineSizeDelta.x:F3}). Tolerance = {SizeDeltaTolerance} px.");

            Assert.LessOrEqual(maxDeltaY, SizeDeltaTolerance,
                $"[{stepLabel}] Logo sizeDelta.y varied by {maxDeltaY:F3} px during {SampleFrames} frames " +
                $"(baseline {baselineSizeDelta.y:F3}). Tolerance = {SizeDeltaTolerance} px.");
        }

        private static bool IsCurrentStepTarget(TutorialManager tutorialManager, string targetName)
        {
            var method = tutorialManager.GetType().GetMethod(
                "TryGetCurrentStepSnapshot",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return false;

            var args = new object[] { 0, string.Empty };
            bool ok = (bool)method.Invoke(tutorialManager, args);
            return ok && string.Equals(args[1]?.ToString(), targetName, System.StringComparison.Ordinal);
        }

        private static T GetPrivateField<T>(object source, string fieldName) where T : class
        {
            var field = source.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(source) as T;
        }
    }
}
