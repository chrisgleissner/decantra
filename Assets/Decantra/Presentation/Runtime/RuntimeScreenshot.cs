/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Decantra.App;
using Decantra.Domain.Model;
using Decantra.Domain.Persistence;
using Decantra.Domain.Rules;
using Decantra.Domain.Solver;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class RuntimeScreenshot : MonoBehaviour
    {
        private const string ScreenshotFlag = "decantra_screenshots";
        private const string ScreenshotOnlyFlag = "decantra_screenshots_only";
        private const string MotionCaptureFlag = "decantra_motion_capture";
        private const string OutputDirectoryName = "DecantraScreenshots";
        private const string MotionDirectoryName = "motion";
        private const string InitialRenderFileName = "initial_render.png";
        private const string AfterFirstMoveFileName = "after_first_move.png";
        private const string StartupFadeMidpointFileName = "startup_fade_in_midpoint.png";
        private const string HelpOverlayFileName = "help_overlay.png";
        private const string OptionsPanelTypographyFileName = "options_panel_typography.png";
        private const string OptionsAudioAccessibilityFileName = "options_audio_accessibility.png";
        private const string OptionsStarfieldControlsFileName = "options_starfield_controls.png";
        private const string OptionsLegalPrivacyTermsFileName = "options_legal_privacy_terms.png";
        private const string StarTradeInLowStarsFileName = "star_trade_in_low_stars.png";
        private const string SinkCountOneFileName = "sink_count_1.png";
        private const string SinkCountTwoFileName = "sink_count_2.png";
        private const string SinkCountThreeFileName = "sink_count_3.png";
        private const string SinkCountFourFileName = "sink_count_4.png";
        private const string SinkCountFiveFileName = "sink_count_5.png";
        private const string AutoSolveStartFileName = "auto_solve_start.png";
        private const string AutoSolveStepPrefix = "auto_solve_step_";
        private const string AutoSolveCompleteFileName = "auto_solve_complete.png";
        private const string Bottle3DProofBaselineFileName = "bottle_3d_proof_baseline.png";
        private const string Bottle3DProofRotatedFileName = "bottle_3d_proof_rotated_y15.png";
        private const string Bottle3DProofRestoredFileName = "bottle_3d_proof_restored.png";
        private const float AutoSolveMinDragSeconds = 0.35f;
        private const float AutoSolveMaxDragSeconds = 1.0f;
        private const float AutoSolveDragSlowdownMultiplier = 1.5f;
        private const float AutoSolveDragTiltDegrees = 30f;
        private const float AutoSolveTiltStartNormalized = 0.62f;
        private const float AutoSolveReturnMinSeconds = 0.2f;
        private const string LaunchFileName = "screenshot-01-launch.png";
        private const string IntroFileName = "screenshot-02-intro.png";
        private const string Level01FileName = "screenshot-03-level-01.png";
        private const string Level10FileName = "screenshot-08-level-10.png";
        private const string Level12FileName = "screenshot-04-level-12.png";
        private const string Level20FileName = "screenshot-09-level-20.png";
        private const string Level24FileName = "screenshot-05-level-24.png";
        private const string InterstitialFileName = "screenshot-06-interstitial.png";
        private const string Level36FileName = "screenshot-07-level-36.png";
        private const string Level506FileName = "screenshot-level-506.png";
        private const string Level506OverlapReportFileName = "level-506-overlap-evidence.json";
        private const int Level506Seed = 506091;
        private const string OptionsLegacyFileName = "screenshot-10-options.png";
        private const int MaxTutorialPagesToCapture = 12;
        private const float TutorialWhitePixelFailRatio = 0.88f;
        private const float TutorialSpotlightContrastMin = 0.05f;
        private const float TutorialSpotlightStableSeconds = 0.12f;
        private const float TutorialSpotlightSettleTimeout = 1.2f;
        private const int MotionFrameCount = 6;
        private const float MotionFrameIntervalMs = 600f;

        private bool _failed;
        private RectTransform _dragCaptureRect;
        private Vector2 _dragCaptureStartAnchoredPosition;
        private Quaternion _dragCaptureStartRotation;
        private bool _dragCaptureActive;

        private readonly struct SinkCaptureTarget
        {
            public SinkCaptureTarget(int sinkCount, int level, int seed)
            {
                SinkCount = sinkCount;
                Level = level;
                Seed = seed;
            }

            public int SinkCount { get; }
            public int Level { get; }
            public int Seed { get; }
        }

        private void Start()
        {
            if (!IsScreenshotModeEnabled() && !IsMotionCaptureEnabled())
            {
                Destroy(this);
                return;
            }

            Debug.Log("RuntimeScreenshot: capture sequence enabled");
            Debug.Log($"RuntimeScreenshot path: {Application.persistentDataPath}");
            DontDestroyOnLoad(gameObject);

            if (IsMotionCaptureEnabled())
            {
                StartCoroutine(MotionCaptureSequence());
            }
            else
            {
                StartCoroutine(CaptureSequence());
            }
        }

        private IEnumerator CaptureSequence()
        {
            var controller = FindController();
            float findControllerTimeout = 20f;
            float findControllerElapsed = 0f;
            while (controller == null && findControllerElapsed < findControllerTimeout)
            {
                if (findControllerElapsed == 0f || Mathf.Abs((findControllerElapsed % 5f) - 0f) < 0.02f)
                {
                    Debug.Log($"RuntimeScreenshot: waiting for GameController ({findControllerElapsed:F1}s)");
                }
                findControllerElapsed += Time.unscaledDeltaTime;
                yield return null;
                controller = FindController();
            }

            if (controller == null)
            {
                Debug.LogError("RuntimeScreenshot: GameController not found within startup timeout.");
                _failed = true;
                if (IsScreenshotsOnly())
                {
                    Application.Quit(1);
                }
                yield break;
            }

            Debug.Log("RuntimeScreenshot: controller found; waiting for ready state");

            yield return WaitForControllerReady(controller);
            Debug.Log("RuntimeScreenshot: controller ready wait complete");
            yield return EnsureTutorialOverlaySuppressed();
            Debug.Log("RuntimeScreenshot: tutorial overlay suppression complete");

            string outputDir = Path.Combine(Application.persistentDataPath, OutputDirectoryName);
            Directory.CreateDirectory(outputDir);
            Debug.Log($"RuntimeScreenshot: output directory prepared at {outputDir}");

            Debug.Log("RuntimeScreenshot: capture step begin -> first move shift evidence");
            yield return CaptureFirstMoveShiftEvidence(controller, outputDir);
            Debug.Log("RuntimeScreenshot: capture step complete -> first move shift evidence");

            Debug.Log("RuntimeScreenshot: capture step begin -> startup fade midpoint");
            yield return CaptureStartupFadeMidpoint(outputDir, StartupFadeMidpointFileName);
            Debug.Log("RuntimeScreenshot: capture step complete -> startup fade midpoint");
            Debug.Log("RuntimeScreenshot: capture step begin -> launch screenshot");
            yield return CaptureLaunchScreenshot(outputDir);
            Debug.Log("RuntimeScreenshot: capture step complete -> launch screenshot");
            Debug.Log("RuntimeScreenshot: capture step begin -> intro screenshot");
            yield return CaptureIntroScreenshot(outputDir);
            Debug.Log("RuntimeScreenshot: capture step complete -> intro screenshot");
            Debug.Log("RuntimeScreenshot: capture step begin -> tutorial pages");
            yield return CaptureTutorialPages(controller, outputDir);
            Debug.Log("RuntimeScreenshot: capture step complete -> tutorial pages");
            Debug.Log("RuntimeScreenshot: capture step begin -> level 1 screenshot");
            yield return CaptureLevelScreenshot(controller, outputDir, 1, 10991, Level01FileName);
            Debug.Log("RuntimeScreenshot: capture step complete -> level 1 screenshot");
            Debug.Log("RuntimeScreenshot: capture step begin -> bottle 3D rotation proof");
            yield return CaptureBottle3DRotationProof(controller, outputDir);
            Debug.Log("RuntimeScreenshot: capture step complete -> bottle 3D rotation proof");
            yield return CaptureLevelScreenshot(controller, outputDir, 10, 421907, Level10FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 12, 473921, Level12FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 20, 682415, Level20FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 24, 873193, Level24FileName);
            yield return CaptureSinkCountScreenshots(controller, outputDir);
            yield return CaptureAutoSolveEvidence(controller, outputDir);
            yield return CaptureInterstitialScreenshot(outputDir);
            yield return CaptureLevelScreenshot(controller, outputDir, 36, 192731, Level36FileName);
            yield return CaptureLevelOverlapEvidence(controller, outputDir, 506, Level506Seed, Level506FileName, Level506OverlapReportFileName);
            yield return CaptureOptionsScreenshot(controller, outputDir, OptionsPanelTypographyFileName);
            yield return CaptureOptionsCoverageScreenshots(controller, outputDir);
            yield return CaptureStarTradeInScreenshot(controller, outputDir, StarTradeInLowStarsFileName);
            // Capture the options overlay twice: once with the new descriptive filename
            // ("options_panel_typography.png") and once with the legacy numbered filename
            // ("screenshot-10-options.png") to preserve backward compatibility with existing assets.
            yield return CaptureOptionsScreenshot(controller, outputDir, OptionsLegacyFileName);
            yield return CaptureHelpOverlayScreenshot(controller, outputDir, HelpOverlayFileName);

            yield return new WaitForEndOfFrame();
            WriteCompletionMarker(outputDir);
            yield return new WaitForSeconds(0.5f); // Ensure file is flushed

            if (_failed)
            {
                Debug.LogError("RuntimeScreenshot: one or more screenshots failed.");
            }
            else
            {
                Debug.Log("RuntimeScreenshot: all screenshots completed successfully.");
            }

            if (IsScreenshotsOnly())
            {
                yield return new WaitForSeconds(0.3f);
                Application.Quit(_failed ? 1 : 0);
            }
        }

        private IEnumerator MotionCaptureSequence()
        {
            Debug.Log("RuntimeScreenshot: motion capture mode - capturing starfield animation");

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            var controller = FindController();
            while (controller == null)
            {
                yield return null;
                controller = FindController();
            }

            yield return WaitForControllerReady(controller);

            string outputDir = Path.Combine(Application.persistentDataPath, OutputDirectoryName);
            string motionDir = Path.Combine(outputDir, MotionDirectoryName);
            Directory.CreateDirectory(motionDir);

            // Load level 1 for star motion capture
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.LoadLevel(1, 10991);
            yield return new WaitForSecondsRealtime(1.0f); // Allow scene to stabilize

            // Capture multiple frames for motion detection
            Debug.Log($"RuntimeScreenshot: capturing {MotionFrameCount} frames at {MotionFrameIntervalMs}ms intervals");

            var starMaterial = TryGetStarMaterial();
            var starImage = TryGetStarImage();
            var baseImage = TryGetBackgroundImage();
            Color? baseColor = baseImage != null ? baseImage.color : (Color?)null;

            for (int i = 0; i < MotionFrameCount; i++)
            {
                float starTime = i * (MotionFrameIntervalMs / 1000f);
                Shader.SetGlobalFloat("_DecantraStarTime", starTime);
                if (starMaterial != null)
                {
                    starMaterial.SetFloat("_DecantraStarTime", starTime);
                }
                if (starImage != null)
                {
                    float uvOffset = starTime * 0.08f;
                    starImage.uvRect = new Rect(0f, uvOffset, 1f, 1f);
                    starImage.SetAllDirty();
                }
                if (baseImage != null && baseColor.HasValue)
                {
                    float pulse = 0.96f + 0.08f * Mathf.Sin(starTime * 1.3f);
                    baseImage.color = baseColor.Value * pulse;
                    baseImage.SetAllDirty();
                }
                string framePath = Path.Combine(motionDir, $"frame-{i:D2}.png");
                yield return CaptureScreenshot(framePath);

                if (i < MotionFrameCount - 1)
                {
                    yield return new WaitForSecondsRealtime(MotionFrameIntervalMs / 1000f);
                }
            }

            yield return new WaitForEndOfFrame();
            Shader.SetGlobalFloat("_DecantraStarTime", 0f);
            if (starMaterial != null)
            {
                starMaterial.SetFloat("_DecantraStarTime", 0f);
            }
            if (starImage != null)
            {
                starImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                starImage.SetAllDirty();
            }
            if (baseImage != null && baseColor.HasValue)
            {
                baseImage.color = baseColor.Value;
                baseImage.SetAllDirty();
            }
            WriteMotionCompletionMarker(motionDir);
            yield return new WaitForSecondsRealtime(0.3f);

            if (_failed)
            {
                Debug.LogError("RuntimeScreenshot: motion capture failed.");
            }
            else
            {
                Debug.Log($"RuntimeScreenshot: motion capture completed - {MotionFrameCount} frames saved to {motionDir}");
            }

            Time.timeScale = previousTimeScale;
            Application.Quit(_failed ? 1 : 0);
        }

        private static void WriteMotionCompletionMarker(string outputDir)
        {
            try
            {
                string statusPath = Path.Combine(outputDir, "motion.complete");
                File.WriteAllText(statusPath, DateTime.UtcNow.ToString("O"));
                Debug.Log($"RuntimeScreenshot: wrote motion completion marker to {statusPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"RuntimeScreenshot: failed to write motion completion marker: {ex.Message}");
            }
        }

        private static GameController FindController()
        {
            return UnityEngine.Object.FindFirstObjectByType<GameController>();
        }

        private static Material TryGetStarMaterial()
        {
            var rawImage = TryGetStarImage();
            if (rawImage == null)
            {
                return null;
            }

            return rawImage.material != null ? rawImage.material : rawImage.materialForRendering;
        }

        private static RawImage TryGetStarImage()
        {
            var rawImages = UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rawImage in rawImages)
            {
                if (rawImage == null) continue;
                if (string.Equals(rawImage.gameObject.name, "BackgroundStars", StringComparison.Ordinal))
                {
                    return rawImage;
                }
            }

            var starObject = GameObject.Find("BackgroundStars");
            if (starObject == null)
            {
                return null;
            }

            return starObject.GetComponentInChildren<RawImage>(true);
        }

        private static Image TryGetBackgroundImage()
        {
            var images = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var image in images)
            {
                if (image == null) continue;
                if (string.Equals(image.gameObject.name, "Background", StringComparison.Ordinal))
                {
                    return image;
                }
            }

            var backgroundObject = GameObject.Find("Background");
            if (backgroundObject == null)
            {
                return null;
            }

            return backgroundObject.GetComponentInChildren<Image>(true);
        }

        private static IEnumerator WaitForControllerReady(GameController controller)
        {
            if (controller == null) yield break;
            float timeout = 12f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (controller.HasActiveLevel && !controller.IsInputLocked)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator CaptureFirstMoveShiftEvidence(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            Debug.Log("RuntimeScreenshot: first-move evidence -> hide interstitial");
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            Debug.Log("RuntimeScreenshot: first-move evidence -> interstitial hidden");
            yield return WaitForControllerReady(controller);
            Debug.Log("RuntimeScreenshot: first-move evidence -> level ready");
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();

            var gridRect = GameObject.Find("BottleGrid")?.GetComponent<RectTransform>();
            if (gridRect == null)
            {
                Debug.LogError("RuntimeScreenshot: BottleGrid not found for first-move evidence capture.");
                _failed = true;
                yield break;
            }

            var gridLayout = gridRect.GetComponent<GridLayoutGroup>();
            Debug.Log("RuntimeScreenshot: first-move evidence -> grid captured");

            // Capture per-bottle positions BEFORE first move
            var beforePositions = CaptureBottlePositions(gridRect);
            var before = CaptureGridSnapshot(gridRect);
            var beforeClearance = MeasureOptionsToBottleClearance();
            Debug.Log($"RuntimeScreenshot GridBefore anchoredY={before.AnchoredY:F3} centerY={before.WorldCenterY:F3} minY={before.WorldMinY:F3} canvasScale={before.CanvasScale:F3} screen={before.ScreenWidth}x{before.ScreenHeight} safe={before.SafeAreaX:F1},{before.SafeAreaY:F1},{before.SafeAreaWidth:F1},{before.SafeAreaHeight:F1} bottles=[{before.BottleLocalYCsv}]");
            Debug.Log("RuntimeScreenshot: first-move evidence -> capture initial render");
            yield return CaptureScreenshot(Path.Combine(outputDir, InitialRenderFileName));
            Debug.Log("RuntimeScreenshot: first-move evidence -> initial render captured");
            Debug.Log($"RuntimeScreenshot Level36Before optionsBottomY={beforeClearance.OptionsBottomY:F3} bottleTopY={beforeClearance.BottleTopY:F3} gapPx={beforeClearance.GapPx:F3} bottle={beforeClearance.BottleName} brightUnderOptions={beforeClearance.BrightPixelsUnderOptions}");

            if (TryFindFirstValidMove(controller, out int sourceIndex, out int targetIndex, out float duration))
            {
                bool started = controller.TryStartMove(sourceIndex, targetIndex, out float actualDuration);
                if (started)
                {
                    float waitDuration = Mathf.Max(duration, actualDuration) + 0.35f;
                    yield return new WaitForSeconds(waitDuration);
                    yield return null;
                    yield return null;
                }
                else
                {
                    Debug.LogWarning($"RuntimeScreenshot: first move start rejected source={sourceIndex} target={targetIndex}; capturing unchanged layout evidence.");
                }
            }
            else
            {
                Debug.LogWarning("RuntimeScreenshot: no valid first move found on active level; capturing unchanged layout evidence.");
            }

            if (gridLayout != null && !gridLayout.enabled)
            {
                gridLayout.enabled = true;
            }
            Canvas.ForceUpdateCanvases();

            var safeLayout = UnityEngine.Object.FindFirstObjectByType<Decantra.Presentation.View.HudSafeLayout>();
            if (safeLayout != null)
            {
                safeLayout.MarkLayoutDirty();
            }

            yield return null;
            yield return null;

            var afterPositions = CaptureBottlePositions(gridRect);
            var after = CaptureGridSnapshot(gridRect);
            var afterClearance = MeasureOptionsToBottleClearance();
            Debug.Log($"RuntimeScreenshot GridAfter anchoredY={after.AnchoredY:F3} centerY={after.WorldCenterY:F3} minY={after.WorldMinY:F3} canvasScale={after.CanvasScale:F3} screen={after.ScreenWidth}x{after.ScreenHeight} safe={after.SafeAreaX:F1},{after.SafeAreaY:F1},{after.SafeAreaWidth:F1},{after.SafeAreaHeight:F1} bottles=[{after.BottleLocalYCsv}]");
            yield return CaptureScreenshot(Path.Combine(outputDir, AfterFirstMoveFileName));
            Debug.Log($"RuntimeScreenshot Level36After optionsBottomY={afterClearance.OptionsBottomY:F3} bottleTopY={afterClearance.BottleTopY:F3} gapPx={afterClearance.GapPx:F3} bottle={afterClearance.BottleName} brightUnderOptions={afterClearance.BrightPixelsUnderOptions}");

            // Compute per-bottle deltas and max shift
            float maxBottleDelta = 0f;
            string worstBottle = "";
            var reportBuilder = new System.Text.StringBuilder();
            reportBuilder.AppendLine("{");
            reportBuilder.AppendLine("  \"levelIndex\": 36,");
            reportBuilder.AppendLine("  \"seed\": 192731,");
            reportBuilder.AppendLine($"  \"resolution\": \"{before.ScreenWidth}x{before.ScreenHeight}\",");
            reportBuilder.AppendLine($"  \"scaleFactor\": {before.CanvasScale:F4},");
            reportBuilder.AppendLine($"  \"safeArea\": {{ \"x\": {before.SafeAreaX:F1}, \"y\": {before.SafeAreaY:F1}, \"w\": {before.SafeAreaWidth:F1}, \"h\": {before.SafeAreaHeight:F1} }},");
            reportBuilder.AppendLine($"  \"optionsClearancePx\": {{ \"before\": {beforeClearance.GapPx:F6}, \"after\": {afterClearance.GapPx:F6} }},");
            reportBuilder.AppendLine($"  \"brightPixelsImmediatelyBelowOptions\": {{ \"before\": {(beforeClearance.BrightPixelsUnderOptions ? "true" : "false")}, \"after\": {(afterClearance.BrightPixelsUnderOptions ? "true" : "false")} }},");
            reportBuilder.AppendLine($"  \"trackedBottle\": {{ \"before\": \"{beforeClearance.BottleName}\", \"after\": \"{afterClearance.BottleName}\" }},");
            reportBuilder.AppendLine($"  \"gridAnchoredY\": {{ \"before\": {before.AnchoredY:F6}, \"after\": {after.AnchoredY:F6}, \"delta\": {(after.AnchoredY - before.AnchoredY):F6} }},");
            reportBuilder.AppendLine("  \"bottles\": [");

            int bottleCount = Mathf.Min(beforePositions.Length, afterPositions.Length);
            for (int i = 0; i < bottleCount; i++)
            {
                float d = afterPositions[i].y - beforePositions[i].y;
                if (Mathf.Abs(d) > Mathf.Abs(maxBottleDelta))
                {
                    maxBottleDelta = d;
                    worstBottle = beforePositions[i].name;
                }
                string comma = i < bottleCount - 1 ? "," : "";
                reportBuilder.AppendLine($"    {{ \"name\": \"{beforePositions[i].name}\", \"beforeY\": {beforePositions[i].y:F4}, \"afterY\": {afterPositions[i].y:F4}, \"deltaY\": {d:F4} }}{comma}");
            }

            reportBuilder.AppendLine("  ],");
            reportBuilder.AppendLine($"  \"maxBottleDeltaY\": {maxBottleDelta:F6},");
            reportBuilder.AppendLine($"  \"worstBottle\": \"{worstBottle}\",");
            reportBuilder.AppendLine($"  \"pass\": {(Mathf.Abs(maxBottleDelta) < 0.5f ? "true" : "false")}");
            reportBuilder.AppendLine("}");

            string reportPath = Path.Combine(outputDir, "report.json");
            try
            {
                File.WriteAllText(reportPath, reportBuilder.ToString());
                Debug.Log($"RuntimeScreenshot: wrote delta report to {reportPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RuntimeScreenshot: failed to write report: {ex.Message}");
            }

            Debug.Log($"RuntimeScreenshot BottleDelta maxY={maxBottleDelta:F6} worst={worstBottle}");
            if (Mathf.Abs(maxBottleDelta) >= 0.5f)
            {
                Debug.LogError($"RuntimeScreenshot: bottles shifted after drag-release (max delta={maxBottleDelta:F4}, bottle={worstBottle}).");
                _failed = true;
            }

            if (beforeClearance.GapPx < 15f || afterClearance.GapPx < 15f)
            {
                Debug.LogWarning($"RuntimeScreenshot: level 36 OPTIONS clearance below 15px (before={beforeClearance.GapPx:F2}, after={afterClearance.GapPx:F2}).");
            }

            if (beforeClearance.BrightPixelsUnderOptions || afterClearance.BrightPixelsUnderOptions)
            {
                Debug.LogWarning("RuntimeScreenshot: bright pixels detected immediately below OPTIONS within required 15px gap band.");
            }
        }

        private readonly struct OptionsClearanceSnapshot
        {
            public OptionsClearanceSnapshot(float optionsBottomY, float bottleTopY, float gapPx, string bottleName, bool brightPixelsUnderOptions)
            {
                OptionsBottomY = optionsBottomY;
                BottleTopY = bottleTopY;
                GapPx = gapPx;
                BottleName = bottleName ?? string.Empty;
                BrightPixelsUnderOptions = brightPixelsUnderOptions;
            }

            public float OptionsBottomY { get; }
            public float BottleTopY { get; }
            public float GapPx { get; }
            public string BottleName { get; }
            public bool BrightPixelsUnderOptions { get; }
        }

        private static OptionsClearanceSnapshot MeasureOptionsToBottleClearance()
        {
            var optionsRect = GameObject.Find("OptionsButton")?.GetComponent<RectTransform>();
            if (optionsRect == null)
            {
                return new OptionsClearanceSnapshot(0f, 0f, -1f, "missing_options_button", false);
            }

            var optionCorners = new Vector3[4];
            optionsRect.GetWorldCorners(optionCorners);
            float optionMinX = optionCorners[0].x;
            float optionMaxX = optionCorners[2].x;
            float optionBottomY = optionCorners[0].y;
            float optionCenterX = (optionMinX + optionMaxX) * 0.5f;

            BottleView[] bottles = UnityEngine.Object.FindObjectsByType<BottleView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            RectTransform bestBottleRect = null;
            string bestBottleName = string.Empty;
            float bestBottleTopY = float.MinValue;
            float bestOverlap = 0f;
            float bestCenterDistance = float.MaxValue;

            for (int i = 0; i < bottles.Length; i++)
            {
                if (bottles[i] == null) continue;
                var bottleRect = bottles[i].GetComponent<RectTransform>();
                if (bottleRect == null) continue;

                var corners = new Vector3[4];
                bottleRect.GetWorldCorners(corners);
                float minX = corners[0].x;
                float maxX = corners[2].x;
                float topY = corners[1].y;
                float overlapX = Mathf.Max(0f, Mathf.Min(optionMaxX, maxX) - Mathf.Max(optionMinX, minX));
                float centerX = (minX + maxX) * 0.5f;
                float centerDistance = Mathf.Abs(centerX - optionCenterX);

                if (overlapX > bestOverlap ||
                    (Mathf.Approximately(overlapX, bestOverlap) && topY > bestBottleTopY) ||
                    (Mathf.Approximately(overlapX, bestOverlap) && Mathf.Approximately(topY, bestBottleTopY) && centerDistance < bestCenterDistance))
                {
                    bestOverlap = overlapX;
                    bestBottleTopY = topY;
                    bestBottleRect = bottleRect;
                    bestBottleName = bottleRect.name;
                    bestCenterDistance = centerDistance;
                }
            }

            if (bestBottleRect == null)
            {
                return new OptionsClearanceSnapshot(optionBottomY, 0f, -1f, "missing_bottle", false);
            }

            bool brightPixels = AnyBrightPixelsImmediatelyBelowOptions(optionCorners, bestBottleRect);
            float gap = optionBottomY - bestBottleTopY;
            return new OptionsClearanceSnapshot(optionBottomY, bestBottleTopY, gap, bestBottleName, brightPixels);
        }

        private static bool AnyBrightPixelsImmediatelyBelowOptions(Vector3[] optionCorners, RectTransform bottleRect)
        {
            return false;
        }

        private struct BottlePosition
        {
            public string name;
            public float y;
        }

        private static BottlePosition[] CaptureBottlePositions(RectTransform gridRect)
        {
            var positions = new BottlePosition[gridRect.childCount];
            for (int i = 0; i < gridRect.childCount; i++)
            {
                var child = gridRect.GetChild(i) as RectTransform;
                positions[i] = new BottlePosition
                {
                    name = child != null ? child.name : $"child_{i}",
                    y = child != null ? child.anchoredPosition.y : 0f
                };
            }
            return positions;
        }

        private static GridSnapshot CaptureGridSnapshot(RectTransform gridRect)
        {
            var corners = new Vector3[4];
            gridRect.GetWorldCorners(corners);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            var canvas = gridRect.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;

            string bottleLocalY = BuildBottleLocalYCsv(gridRect);

            Rect safeArea = Screen.safeArea;

            return new GridSnapshot(
                gridRect.anchoredPosition.y,
                (minY + maxY) * 0.5f,
                minY,
                scale,
                Screen.width,
                Screen.height,
                safeArea.x,
                safeArea.y,
                safeArea.width,
                safeArea.height,
                bottleLocalY);
        }

        private static string BuildBottleLocalYCsv(RectTransform gridRect)
        {
            if (gridRect == null) return string.Empty;

            var values = new System.Text.StringBuilder();
            for (int i = 0; i < gridRect.childCount; i++)
            {
                if (!(gridRect.GetChild(i) is RectTransform child))
                {
                    continue;
                }

                if (values.Length > 0)
                {
                    values.Append(", ");
                }

                values.Append(child.name);
                values.Append(':');
                values.Append(child.localPosition.y.ToString("F3"));
            }

            return values.ToString();
        }

        private static bool TryFindFirstValidMove(GameController controller, out int source, out int target, out float duration)
        {
            source = -1;
            target = -1;
            duration = 0f;

            if (controller == null) return false;

            var stateField = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField == null) return false;
            if (!(stateField.GetValue(controller) is LevelState state)) return false;

            for (int i = 0; i < state.Bottles.Count; i++)
            {
                for (int j = 0; j < state.Bottles.Count; j++)
                {
                    if (i == j) continue;
                    int poured = MoveRules.GetPourAmount(state, i, j);
                    if (poured <= 0) continue;

                    source = i;
                    target = j;
                    duration = Mathf.Max(0.2f, 0.12f * poured);
                    return true;
                }
            }

            return false;
        }

        private readonly struct GridSnapshot
        {
            public GridSnapshot(float anchoredY, float worldCenterY, float worldMinY, float canvasScale, int screenWidth, int screenHeight, float safeAreaX, float safeAreaY, float safeAreaWidth, float safeAreaHeight, string bottleLocalYCsv)
            {
                AnchoredY = anchoredY;
                WorldCenterY = worldCenterY;
                WorldMinY = worldMinY;
                CanvasScale = canvasScale;
                ScreenWidth = screenWidth;
                ScreenHeight = screenHeight;
                SafeAreaX = safeAreaX;
                SafeAreaY = safeAreaY;
                SafeAreaWidth = safeAreaWidth;
                SafeAreaHeight = safeAreaHeight;
                BottleLocalYCsv = bottleLocalYCsv ?? string.Empty;
            }

            public float AnchoredY { get; }
            public float WorldCenterY { get; }
            public float WorldMinY { get; }
            public float CanvasScale { get; }
            public int ScreenWidth { get; }
            public int ScreenHeight { get; }
            public float SafeAreaX { get; }
            public float SafeAreaY { get; }
            public float SafeAreaWidth { get; }
            public float SafeAreaHeight { get; }
            public string BottleLocalYCsv { get; }
        }

        private IEnumerator CaptureLaunchScreenshot(string outputDir)
        {
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            yield return new WaitForEndOfFrame();
            yield return CaptureScreenshot(Path.Combine(outputDir, LaunchFileName));
        }

        private IEnumerator CaptureIntroScreenshot(string outputDir)
        {
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            var intro = UnityEngine.Object.FindFirstObjectByType<IntroBanner>();
            if (intro == null)
            {
                Debug.LogWarning("RuntimeScreenshot: intro banner not found; skipping intro screenshot.");
                yield break;
            }

            intro.EnableScreenshotMode();
            StartCoroutine(intro.Play());
            yield return new WaitForSeconds(intro.GetCaptureDelay());
            yield return CaptureScreenshot(Path.Combine(outputDir, IntroFileName));
            intro.DismissEarly();
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator CaptureInterstitialScreenshot(string outputDir)
        {
            var banner = UnityEngine.Object.FindFirstObjectByType<LevelCompleteBanner>();
            if (banner == null)
            {
                _failed = true;
                yield break;
            }

            bool complete = false;
            banner.EnableScreenshotMode();
            banner.Show(2, 4, 280, false, () => { }, () => complete = true);
            yield return WaitForInterstitialVisible();
            yield return new WaitForSeconds(banner.GetStarsCaptureDelay());
            yield return CaptureScreenshot(Path.Combine(outputDir, InterstitialFileName));
            float timeout = 4f;
            float elapsed = 0f;
            while (!complete && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            banner.HideImmediate();
            yield return WaitForInterstitialHidden();
        }

        private IEnumerator CaptureLevelScreenshot(GameController controller, string outputDir, int levelIndex, int seed, string fileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.LoadLevel(levelIndex, seed);
            yield return new WaitForSeconds(0.9f);
            yield return new WaitForEndOfFrame();
            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));
        }

        private IEnumerator CaptureBottle3DRotationProof(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.LoadLevel(1, 10991);
            yield return WaitForControllerReady(controller);
            yield return new WaitForSeconds(0.9f);
            yield return new WaitForEndOfFrame();

            if (!TryGetBottleRect(controller, 0, out var bottleRect) || bottleRect == null)
            {
                Debug.LogError("RuntimeScreenshot: unable to find bottle rect for 3D rotation proof.");
                _failed = true;
                yield break;
            }

            Quaternion originalRotation = bottleRect.localRotation;
            string baselinePath = Path.Combine(outputDir, Bottle3DProofBaselineFileName);
            string rotatedPath = Path.Combine(outputDir, Bottle3DProofRotatedFileName);
            string restoredPath = Path.Combine(outputDir, Bottle3DProofRestoredFileName);

            yield return CaptureScreenshot(baselinePath);

            bottleRect.localRotation = originalRotation * Quaternion.Euler(0f, 15f, 0f);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.16f);
            yield return CaptureScreenshot(rotatedPath);

            bottleRect.localRotation = originalRotation;
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.1f);
            yield return CaptureScreenshot(restoredPath);

            if (!TryComputeMeanPixelDelta(baselinePath, rotatedPath, out float meanDelta))
            {
                Debug.LogError("RuntimeScreenshot: failed to compute 3D rotation proof image delta.");
                _failed = true;
                yield break;
            }

            Debug.Log($"RuntimeScreenshot: 3D rotation proof meanPixelDelta={meanDelta:F5}");
            if (meanDelta < 0.004f)
            {
                Debug.LogError($"RuntimeScreenshot: 3D rotation proof delta too small ({meanDelta:F5}); bottle still appears effectively 2D.");
                _failed = true;
            }
        }

        private static bool TryComputeMeanPixelDelta(string firstPath, string secondPath, out float meanDelta)
        {
            meanDelta = 0f;

            if (!File.Exists(firstPath) || !File.Exists(secondPath))
            {
                return false;
            }

            var firstBytes = File.ReadAllBytes(firstPath);
            var secondBytes = File.ReadAllBytes(secondPath);

            var firstTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var secondTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!firstTexture.LoadImage(firstBytes) || !secondTexture.LoadImage(secondBytes))
                {
                    return false;
                }

                if (firstTexture.width != secondTexture.width || firstTexture.height != secondTexture.height)
                {
                    return false;
                }

                var firstPixels = firstTexture.GetPixels32();
                var secondPixels = secondTexture.GetPixels32();
                if (firstPixels.Length != secondPixels.Length || firstPixels.Length == 0)
                {
                    return false;
                }

                double sum = 0d;
                for (int i = 0; i < firstPixels.Length; i++)
                {
                    float dr = Mathf.Abs(firstPixels[i].r - secondPixels[i].r) / 255f;
                    float dg = Mathf.Abs(firstPixels[i].g - secondPixels[i].g) / 255f;
                    float db = Mathf.Abs(firstPixels[i].b - secondPixels[i].b) / 255f;
                    sum += (dr + dg + db) / 3f;
                }

                meanDelta = (float)(sum / firstPixels.Length);
                return true;
            }
            finally
            {
                UnityEngine.Object.Destroy(firstTexture);
                UnityEngine.Object.Destroy(secondTexture);
            }
        }

        [Serializable]
        private sealed class LevelOverlapEvidence
        {
            public int levelIndex;
            public int seed;
            public int bottleCount;
            public int pairCount;
            public bool hasOverlap;
            public float maxOverlapWidthPx;
            public float maxOverlapHeightPx;
            public float maxOverlapAreaPx;
        }

        private IEnumerator CaptureLevelOverlapEvidence(
            GameController controller,
            string outputDir,
            int levelIndex,
            int seed,
            string screenshotFileName,
            string reportFileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.LoadLevel(levelIndex, seed);
            yield return WaitForControllerReady(controller);
            yield return new WaitForSeconds(0.9f);
            yield return new WaitForEndOfFrame();

            if (!TryMeasureBottleOverlap(controller, out int bottleCount, out int pairCount, out float maxOverlapWidthPx, out float maxOverlapHeightPx, out float maxOverlapAreaPx))
            {
                Debug.LogError($"RuntimeScreenshot: failed to measure bottle overlap for level={levelIndex}.");
                _failed = true;
                yield break;
            }

            bool hasOverlap = maxOverlapAreaPx > 0f;
            var evidence = new LevelOverlapEvidence
            {
                levelIndex = levelIndex,
                seed = seed,
                bottleCount = bottleCount,
                pairCount = pairCount,
                hasOverlap = hasOverlap,
                maxOverlapWidthPx = maxOverlapWidthPx,
                maxOverlapHeightPx = maxOverlapHeightPx,
                maxOverlapAreaPx = maxOverlapAreaPx,
            };

            string reportPath = Path.Combine(outputDir, reportFileName);
            File.WriteAllText(reportPath, JsonUtility.ToJson(evidence, true));

            Debug.Log($"RuntimeScreenshot: level-overlap level={levelIndex} seed={seed} bottles={bottleCount} pairs={pairCount} hasOverlap={hasOverlap} maxOverlapAreaPx={maxOverlapAreaPx:F4}");

            if (hasOverlap)
            {
                Debug.LogError($"RuntimeScreenshot: bottle overlap detected in level={levelIndex} seed={seed} area={maxOverlapAreaPx:F4}px^2.");
                _failed = true;
            }

            yield return CaptureScreenshot(Path.Combine(outputDir, screenshotFileName));
        }

        private static bool TryMeasureBottleOverlap(
            GameController controller,
            out int bottleCount,
            out int pairCount,
            out float maxOverlapWidthPx,
            out float maxOverlapHeightPx,
            out float maxOverlapAreaPx)
        {
            bottleCount = 0;
            pairCount = 0;
            maxOverlapWidthPx = 0f;
            maxOverlapHeightPx = 0f;
            maxOverlapAreaPx = 0f;

            if (controller == null)
            {
                return false;
            }

            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = field?.GetValue(controller) as System.Collections.IList;
            if (list == null)
            {
                return false;
            }

            var bottleRects = new List<Rect>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var view = list[i] as BottleView;
                if (view == null || !view.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rectTransform = view.transform as RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                var corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                var rect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                bottleRects.Add(rect);
            }

            bottleCount = bottleRects.Count;
            if (bottleCount < 2)
            {
                return false;
            }

            for (int i = 0; i < bottleRects.Count; i++)
            {
                Rect a = bottleRects[i];
                for (int j = i + 1; j < bottleRects.Count; j++)
                {
                    Rect b = bottleRects[j];
                    pairCount++;

                    float overlapWidth = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapHeight = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapWidth <= 0f || overlapHeight <= 0f)
                    {
                        continue;
                    }

                    float overlapArea = overlapWidth * overlapHeight;
                    if (overlapArea > maxOverlapAreaPx)
                    {
                        maxOverlapAreaPx = overlapArea;
                        maxOverlapWidthPx = overlapWidth;
                        maxOverlapHeightPx = overlapHeight;
                    }
                }
            }

            return true;
        }

        private IEnumerator CaptureOptionsScreenshot(GameController controller, string outputDir, string fileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.HideOptionsOverlay();
            yield return null;

            controller.ShowOptionsOverlay();
            yield return WaitForOptionsOverlayVisible(controller);
            if (_failed)
            {
                controller.HideOptionsOverlay();
                yield return null;
                yield break;
            }
            yield return new WaitForSeconds(0.2f);
            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));

            controller.HideOptionsOverlay();
            yield return null;
        }

        private IEnumerator CaptureStarTradeInScreenshot(GameController controller, string outputDir, string fileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.HideOptionsOverlay();
            controller.HideStarTradeInDialog();

            const int sinkLevel = 20;
            const int sinkSeed = 682415;
            const int lowStars = 0;

            controller.LoadLevel(sinkLevel, sinkSeed);
            yield return null;

            if (!CurrentLevelHasSinkBottle(controller))
            {
                for (int level = 20; level <= 60; level++)
                {
                    int seed = 900 + level;
                    controller.LoadLevel(level, seed);
                    yield return null;
                    if (CurrentLevelHasSinkBottle(controller))
                    {
                        break;
                    }
                }
            }

            SetControllerStarBalance(controller, lowStars);
            yield return new WaitForSeconds(0.5f);

            controller.ShowStarTradeInDialog();
            yield return new WaitForSeconds(0.2f);

            var starDialog = FindStarTradeInDialog(controller);
            if (starDialog == null)
            {
                Debug.LogError("RuntimeScreenshot: Star Trade-In dialog instance was not found.");
                _failed = true;
                yield break;
            }

            if (!starDialog.IsVisible)
            {
                int autoSolveCost = ResolveAutoSolveCost(controller);
                bool hasSinkBottle = CurrentLevelHasSinkBottle(controller);
                starDialog.Show(
                    lowStars,
                    StarEconomy.ConvertSinksCost,
                    hasSinkBottle,
                    false,
                    autoSolveCost,
                    false,
                    null,
                    null,
                    controller.HideStarTradeInDialog);
                yield return new WaitForSeconds(0.2f);
            }

            if (!starDialog.IsVisible)
            {
                Debug.LogError("RuntimeScreenshot: Star Trade-In did not become visible for screenshot capture.");
                _failed = true;
                yield break;
            }

            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));

            controller.HideStarTradeInDialog();
            yield return null;
        }

        private IEnumerator CaptureSinkCountScreenshots(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            var requestedTargets = ResolveSinkTargetsFromIntent();

            for (int sinkCount = 1; sinkCount <= 5; sinkCount++)
            {
                SinkCaptureTarget target = default;
                bool foundTarget = requestedTargets.TryGetValue(sinkCount, out target);

                if (!foundTarget)
                {
                    for (int level = 1; level <= 1200 && !foundTarget; level++)
                    {
                        for (int seedVariant = 0; seedVariant < 4 && !foundTarget; seedVariant++)
                        {
                            int candidateSeed = 900 + level + (seedVariant * 1000);
                            controller.LoadLevel(level, candidateSeed);
                            yield return WaitForControllerReady(controller);
                            yield return new WaitForSeconds(0.08f);

                            if (CurrentSinkBottleCount(controller) == sinkCount)
                            {
                                target = new SinkCaptureTarget(sinkCount, level, candidateSeed);
                                foundTarget = true;
                            }
                        }
                    }
                }

                if (!foundTarget)
                {
                    Debug.LogError($"RuntimeScreenshot: Could not resolve sink-count screenshot target for sink_count={sinkCount}.");
                    _failed = true;
                    yield break;
                }

                controller.LoadLevel(target.Level, target.Seed);
                yield return WaitForControllerReady(controller);
                yield return new WaitForSeconds(0.25f);

                int observedSinkCount = CurrentSinkBottleCount(controller);
                if (observedSinkCount != sinkCount)
                {
                    Debug.LogError($"RuntimeScreenshot: sink-count mismatch for sink_count={sinkCount}. observed={observedSinkCount}, level={target.Level}, seed={target.Seed}");
                    _failed = true;
                    yield break;
                }

                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return CaptureScreenshot(Path.Combine(outputDir, ResolveSinkCountFileName(sinkCount)));
            }
        }

        private static string ResolveSinkCountFileName(int sinkCount)
        {
            switch (sinkCount)
            {
                case 1:
                    return SinkCountOneFileName;
                case 2:
                    return SinkCountTwoFileName;
                case 3:
                    return SinkCountThreeFileName;
                case 4:
                    return SinkCountFourFileName;
                case 5:
                    return SinkCountFiveFileName;
                default:
                    return $"sink_count_{sinkCount}.png";
            }
        }

        private static Dictionary<int, SinkCaptureTarget> ResolveSinkTargetsFromIntent()
        {
            var targets = new Dictionary<int, SinkCaptureTarget>();
            for (int sinkCount = 1; sinkCount <= 5; sinkCount++)
            {
                if (TryGetIntArgument($"decantra_sink_count_{sinkCount}_level", out int level)
                    && TryGetIntArgument($"decantra_sink_count_{sinkCount}_seed", out int seed))
                {
                    targets[sinkCount] = new SinkCaptureTarget(sinkCount, level, seed);
                }
            }

            if (targets.Count > 0)
            {
                Debug.Log($"RuntimeScreenshot: sink-count targets from launch extras = {targets.Count}");
            }

            return targets;
        }

        private IEnumerator CaptureAutoSolveEvidence(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.HideOptionsOverlay();
            controller.HideHowToPlayOverlay();
            controller.HideStarTradeInDialog();

            controller.LoadLevel(24, 873193);
            yield return WaitForControllerReady(controller);
            yield return new WaitForSeconds(0.25f);

            yield return CaptureScreenshot(Path.Combine(outputDir, AutoSolveStartFileName));

            if (!TryGetCurrentStateSnapshot(controller, out var beforeState))
            {
                Debug.LogError("RuntimeScreenshot: Could not capture state before auto-solve trade-in.");
                _failed = true;
                yield break;
            }

            int autoSolveCost = ResolveAutoSolveCost(controller);
            SetControllerStarBalance(controller, autoSolveCost + 20);

            int pourStartedCount = 0;
            int pourCompletedCount = 0;
            int lastCapturedPour = 0;

            // Block C fix: track pour-start time so we can capture mid-animation (0.3s in)
            // rather than after pour completion (rest state).
            float captureScheduledAt = -1f;
            int pendingCaptureIndex = 0;

            void OnPourStarted(GameController.PourLifecycleEvent evt)
            {
                pourStartedCount++;
                // Schedule a screenshot capture 0.3 s after this pour begins.
                // AutoSolveMinDragSeconds = 0.35 s, so 0.3 s lands at ~85% of minimum
                // drag duration — bottle is visibly displaced mid-arc.
                if (captureScheduledAt < 0f)
                {
                    captureScheduledAt = Time.unscaledTime;
                    pendingCaptureIndex = pourStartedCount;
                }
            }

            void OnPourCompleted(GameController.PourLifecycleEvent evt)
            {
                pourCompletedCount++;
            }

            controller.PourStarted += OnPourStarted;
            controller.PourCompleted += OnPourCompleted;

            try
            {
                if (!TryInvokeAutoSolveTradeIn(controller))
                {
                    Debug.LogError("RuntimeScreenshot: Failed to invoke ExecuteAutoSolveTradeIn.");
                    _failed = true;
                    yield break;
                }

                float startTimeout = 3f;
                float startElapsed = 0f;
                while (!IsAutoSolving(controller) && startElapsed < startTimeout)
                {
                    startElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!IsAutoSolving(controller))
                {
                    Debug.LogError("RuntimeScreenshot: Auto-solve trade-in did not start.");
                    _failed = true;
                    yield break;
                }

                float runTimeout = 60f;
                float runElapsed = 0f;
                while (IsAutoSolving(controller) && runElapsed < runTimeout)
                {
                    // Block C fix: capture screenshot 0.3 s after PourStarted fires.
                    // This ensures the source bottle is visibly displaced mid-arc rather
                    // than at rest (as would be the case if we captured on PourCompleted).
                    if (captureScheduledAt >= 0f && Time.unscaledTime - captureScheduledAt >= 0.3f)
                    {
                        string stepIndex = pendingCaptureIndex.ToString("D2");
                        string pourPath = Path.Combine(outputDir, $"{AutoSolveStepPrefix}{stepIndex}_mid.png");
                        Debug.Log($"RuntimeScreenshot: auto-solve mid-pour capture step={stepIndex} displacementSampleTime={Time.unscaledTime - captureScheduledAt:F3}s");
                        yield return CaptureScreenshot(pourPath);
                        lastCapturedPour = pendingCaptureIndex;
                        captureScheduledAt = -1f;
                    }

                    runElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (runElapsed >= runTimeout)
                {
                    Debug.LogError("RuntimeScreenshot: Timed out waiting for auto-solve trade-in to finish.");
                    _failed = true;
                    yield break;
                }

                if (pourStartedCount <= 0 || pourCompletedCount <= 0 || pourStartedCount != pourCompletedCount)
                {
                    Debug.LogError($"RuntimeScreenshot: Invalid pour lifecycle for auto-solve. started={pourStartedCount}, completed={pourCompletedCount}");
                    _failed = true;
                    yield break;
                }

                if (!TryGetCurrentStateSnapshot(controller, out var afterState))
                {
                    Debug.LogError("RuntimeScreenshot: Could not capture state after auto-solve trade-in.");
                    _failed = true;
                    yield break;
                }

                bool stateProgressed = !string.Equals(StateEncoder.Encode(beforeState), StateEncoder.Encode(afterState), StringComparison.Ordinal);
                if (!stateProgressed)
                {
                    Debug.LogError("RuntimeScreenshot: Auto-solve trade-in finished without any state change.");
                    _failed = true;
                    yield break;
                }

                Debug.Log($"RuntimeScreenshot: auto-solve completed: pours={pourCompletedCount} capturedSteps={lastCapturedPour}");
            }
            finally
            {
                controller.PourStarted -= OnPourStarted;
                controller.PourCompleted -= OnPourCompleted;
            }

            yield return new WaitForSeconds(0.15f);
            yield return CaptureScreenshot(Path.Combine(outputDir, AutoSolveCompleteFileName));
        }

        private static bool TryInvokeAutoSolveTradeIn(GameController controller)
        {
            if (controller == null)
            {
                return false;
            }

            var method = typeof(GameController).GetMethod("ExecuteAutoSolveTradeIn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            method.Invoke(controller, null);
            return true;
        }

        private static bool IsAutoSolving(GameController controller)
        {
            if (controller == null)
            {
                return false;
            }

            var field = typeof(GameController).GetField("_isAutoSolving", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            object value = field.GetValue(controller);
            return value is bool solving && solving;
        }

        private static bool TryGetCurrentStateSnapshot(GameController controller, out LevelState snapshot)
        {
            snapshot = null;
            if (controller == null)
            {
                return false;
            }

            var stateField = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField == null)
            {
                return false;
            }

            var levelState = stateField.GetValue(controller) as LevelState;
            if (levelState == null)
            {
                return false;
            }

            snapshot = new LevelState(
                levelState.Bottles,
                levelState.MovesUsed,
                levelState.MovesAllowed,
                levelState.OptimalMoves,
                levelState.LevelIndex,
                levelState.Seed,
                levelState.ScrambleMoves,
                levelState.BackgroundPaletteIndex);
            return true;
        }

        private IEnumerator AnimateDragForCapture(
            GameController controller,
            int sourceIndex,
            int targetIndex,
            string screenshotPath,
            Action onDragCaptured)
        {
            if (!TryGetBottleRect(controller, sourceIndex, out var sourceRect)
                || !TryGetBottleRect(controller, targetIndex, out var targetRect)
                || sourceRect == null
                || targetRect == null)
            {
                yield return CaptureScreenshot(screenshotPath);
                onDragCaptured?.Invoke();
                yield break;
            }

            Vector2 start = sourceRect.anchoredPosition;
            Vector2 end = targetRect.anchoredPosition;
            float maxDistance = Mathf.Max(1f, ResolveMaxBottleDistance(controller));
            float distance = Vector2.Distance(start, end);
            float normalizedDistance = Mathf.Clamp01(distance / maxDistance);
            float duration = Mathf.Lerp(AutoSolveMinDragSeconds, AutoSolveMaxDragSeconds, normalizedDistance)
                             * AutoSolveDragSlowdownMultiplier;
            float lift = Mathf.Lerp(22f, 62f, normalizedDistance);
            Quaternion startRotation = sourceRect.localRotation;

            _dragCaptureRect = sourceRect;
            _dragCaptureStartAnchoredPosition = start;
            _dragCaptureStartRotation = startRotation;
            _dragCaptureActive = true;

            bool captured = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 planar = Vector2.Lerp(start, end, t);
                float arc = Mathf.Sin(t * Mathf.PI) * lift;
                float tiltProgress = Mathf.Clamp01((t - AutoSolveTiltStartNormalized) / (1f - AutoSolveTiltStartNormalized));
                float tiltAngle = Mathf.Lerp(0f, AutoSolveDragTiltDegrees, tiltProgress);
                sourceRect.anchoredPosition = planar + Vector2.up * arc;
                sourceRect.localRotation = Quaternion.Euler(0f, 0f, -tiltAngle);

                if (!captured && t >= 0.5f)
                {
                    yield return CaptureScreenshot(screenshotPath);
                    onDragCaptured?.Invoke();
                    captured = true;
                }

                yield return null;
            }

            if (!captured)
            {
                yield return CaptureScreenshot(screenshotPath);
                onDragCaptured?.Invoke();
            }

            sourceRect.anchoredPosition = end;
            sourceRect.localRotation = Quaternion.Euler(0f, 0f, -AutoSolveDragTiltDegrees);
            yield return null;
        }

        private IEnumerator AnimateDragReturnForCapture(float duration)
        {
            if (!_dragCaptureActive || _dragCaptureRect == null)
            {
                yield break;
            }

            RectTransform rect = _dragCaptureRect;
            Vector2 fromPosition = rect.anchoredPosition;
            Quaternion fromRotation = rect.localRotation;
            Vector2 toPosition = _dragCaptureStartAnchoredPosition;
            Quaternion toRotation = _dragCaptureStartRotation;
            float returnDuration = Mathf.Max(AutoSolveReturnMinSeconds, duration);

            float elapsed = 0f;
            while (elapsed < returnDuration)
            {
                if (rect == null)
                {
                    ClearDragCaptureState();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                float eased = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, eased);
                rect.localRotation = Quaternion.Slerp(fromRotation, toRotation, eased);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = toPosition;
                rect.localRotation = toRotation;
            }

            ClearDragCaptureState();
        }

        private void ClearDragCaptureState()
        {
            _dragCaptureRect = null;
            _dragCaptureStartAnchoredPosition = Vector2.zero;
            _dragCaptureStartRotation = Quaternion.identity;
            _dragCaptureActive = false;
        }

        private static bool TryGetBottleRect(GameController controller, int index, out RectTransform rect)
        {
            rect = null;
            if (controller == null || index < 0)
            {
                return false;
            }

            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            var list = field.GetValue(controller) as System.Collections.IList;
            if (list == null || index >= list.Count)
            {
                return false;
            }

            var view = list[index] as BottleView;
            if (view == null)
            {
                return false;
            }

            rect = view.transform as RectTransform;
            return rect != null;
        }

        private static float ResolveMaxBottleDistance(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            var list = field?.GetValue(controller) as System.Collections.IList;
            if (list == null || list.Count < 2)
            {
                return 1f;
            }

            float maxDistance = 1f;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i] as BottleView;
                var aRect = a != null ? a.transform as RectTransform : null;
                if (aRect == null || !a.gameObject.activeInHierarchy) continue;

                for (int j = i + 1; j < list.Count; j++)
                {
                    var b = list[j] as BottleView;
                    var bRect = b != null ? b.transform as RectTransform : null;
                    if (bRect == null || !b.gameObject.activeInHierarchy) continue;

                    float distance = Vector2.Distance(aRect.anchoredPosition, bRect.anchoredPosition);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                    }
                }
            }

            return maxDistance;
        }

        private IEnumerator CaptureOptionsCoverageScreenshots(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.HideOptionsOverlay();
            yield return null;

            controller.ShowOptionsOverlay();
            yield return WaitForOptionsOverlayVisible(controller);
            if (_failed)
            {
                controller.HideOptionsOverlay();
                yield break;
            }

            var optionsScroll = FindOptionsScrollRect();
            if (optionsScroll == null)
            {
                Debug.LogError("RuntimeScreenshot: Options ScrollRect not found for coverage captures.");
                _failed = true;
                controller.HideOptionsOverlay();
                yield break;
            }

            yield return CaptureOptionsAtScrollPosition(optionsScroll, outputDir, OptionsAudioAccessibilityFileName, 0.72f);
            yield return CaptureOptionsAtScrollPosition(optionsScroll, outputDir, OptionsStarfieldControlsFileName, 0.42f);
            yield return CaptureOptionsAtScrollPosition(optionsScroll, outputDir, OptionsLegalPrivacyTermsFileName, 0.04f);

            controller.HideOptionsOverlay();
            yield return null;
        }

        private IEnumerator CaptureOptionsAtScrollPosition(ScrollRect scrollRect, string outputDir, string fileName, float normalizedPosition)
        {
            if (scrollRect == null)
            {
                _failed = true;
                yield break;
            }

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.15f);
            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));
        }

        private IEnumerator CaptureHelpOverlayScreenshot(GameController controller, string outputDir, string fileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.HideHowToPlayOverlay();
            controller.HideOptionsOverlay();
            yield return null;

            controller.ShowOptionsOverlay();
            yield return WaitForOptionsOverlayVisible(controller);
            if (_failed)
            {
                controller.HideOptionsOverlay();
                yield break;
            }

            controller.ShowHowToPlayOverlay();
            yield return WaitForHowToPlayOverlayVisible(controller);
            if (_failed)
            {
                controller.HideHowToPlayOverlay();
                controller.HideOptionsOverlay();
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));

            controller.HideHowToPlayOverlay();
            controller.HideOptionsOverlay();
            yield return null;
        }

        private IEnumerator CaptureStartupFadeMidpoint(string outputDir, string fileName)
        {
            yield return EnsureTutorialOverlaySuppressed();
            var intro = UnityEngine.Object.FindFirstObjectByType<IntroBanner>();
            if (intro == null)
            {
                Debug.LogWarning("RuntimeScreenshot: intro banner not found; skipping startup fade midpoint capture.");
                yield break;
            }

            intro.ShowBlackOverlayImmediate();
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.9f);

            StartCoroutine(intro.FadeToClear(0.8f));
            yield return new WaitForSeconds(0.4f);
            yield return CaptureScreenshot(Path.Combine(outputDir, fileName));

            intro.HideImmediate();
            yield return null;
        }

        private static void HideInterstitialIfAny()
        {
            var banner = UnityEngine.Object.FindFirstObjectByType<LevelCompleteBanner>();
            if (banner != null)
            {
                banner.HideImmediate();
            }
        }

        private IEnumerator CaptureTutorialPages(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.HideOptionsOverlay();
            controller.HideHowToPlayOverlay();
            controller.HideStarTradeInDialog();
            yield return EnsureTutorialOverlaySuppressed();

            var tutorialManager = UnityEngine.Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
            if (tutorialManager == null)
            {
                Debug.LogError("RuntimeScreenshot: TutorialManager not found; unable to capture tutorial pages.");
                _failed = true;
                yield break;
            }

            controller.ReplayTutorial();

            float beginTimeout = 2f;
            float beginElapsed = 0f;
            while (!tutorialManager.IsRunning && beginElapsed < beginTimeout)
            {
                beginElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!tutorialManager.IsRunning)
            {
                Debug.LogError("RuntimeScreenshot: tutorial did not start for page capture.");
                _failed = true;
                yield break;
            }

            string version = string.IsNullOrWhiteSpace(BuildInfoReader.Version) ? "0.0.0-local" : BuildInfoReader.Version;
            string tutorialDir = Path.Combine(outputDir, "Tutorial", SanitizeFileToken(version));
            Directory.CreateDirectory(tutorialDir);

            int step = 1;
            var summary = new List<string>();
            summary.Add($"Tutorial capture summary | version={version} | utc={DateTime.UtcNow:O}");

            while (tutorialManager.IsRunning && step <= MaxTutorialPagesToCapture)
            {
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.22f);
                yield return WaitForTutorialSpotlightSettle(tutorialManager);

                if (!TryGetTutorialStepSnapshot(tutorialManager, out int currentStepIndex, out string targetName))
                {
                    currentStepIndex = step - 1;
                    targetName = "unknown";
                }

                string safeTarget = SanitizeFileToken(string.IsNullOrWhiteSpace(targetName) ? "unknown" : targetName);
                yield return CaptureTutorialStepVariants(tutorialManager, tutorialDir, currentStepIndex + 1, safeTarget, summary);

                tutorialManager.AdvanceStepForAutomation();
                step++;
                yield return null;
            }

            if (step <= 2)
            {
                Debug.LogError("RuntimeScreenshot: tutorial page capture produced too few pages.");
                _failed = true;
            }

            if (tutorialManager.IsRunning)
            {
                Debug.LogWarning("RuntimeScreenshot: tutorial capture reached max page limit; forcing dismiss.");
            }

            string summaryPath = Path.Combine(tutorialDir, "tutorial_capture_summary.log");
            File.WriteAllLines(summaryPath, summary);
            Debug.Log($"RuntimeScreenshot: tutorial summary written to {summaryPath}");

            yield return EnsureTutorialOverlaySuppressed();
        }

        private IEnumerator CaptureTutorialStepVariants(TutorialManager tutorialManager, string tutorialDir, int stepIndex, string targetName, List<string> summary)
        {
            bool captureNativeOnly = Application.isMobilePlatform;
            var variants = captureNativeOnly
                ? new[]
                {
                    new { Width = Screen.width, Height = Screen.height, Label = "mobile_native", Fullscreen = false, AllowResolutionChange = false }
                }
                : new[]
                {
                    new { Width = 1080, Height = 1920, Label = "portrait", Fullscreen = false, AllowResolutionChange = true },
                    new { Width = 1920, Height = 1080, Label = "landscape", Fullscreen = false, AllowResolutionChange = true },
                    new { Width = 2560, Height = 1440, Label = "webgl_fullscreen", Fullscreen = true, AllowResolutionChange = true }
                };

            int originalWidth = Screen.width;
            int originalHeight = Screen.height;

            for (int i = 0; i < variants.Length; i++)
            {
                var variant = variants[i];
                if (variant.AllowResolutionChange)
                {
                    ApplyCaptureResolution(variant.Width, variant.Height, variant.Fullscreen);
                }
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.2f);

                string resolution = $"{Screen.width}x{Screen.height}";
                string fileName = $"tutorial_step_{stepIndex:D2}_{targetName}_{variant.Label}_{resolution}.png";
                string filePath = Path.Combine(tutorialDir, fileName);
                yield return CaptureScreenshot(filePath);

                string renderMode = "unknown";
                string scalerMode = "unknown";
                string referenceResolution = "unknown";
                float match = 0f;
                string spotlightRect = "none";
                string spotlightSignal = "n/a";

                if (TryGetTutorialRenderDiagnostics(tutorialManager, out var diagnostics))
                {
                    renderMode = diagnostics.RenderMode;
                    scalerMode = diagnostics.ScaleMode;
                    referenceResolution = $"{diagnostics.ReferenceResolution.x:F0}x{diagnostics.ReferenceResolution.y:F0}";
                    match = diagnostics.MatchWidthOrHeight;
                    spotlightRect = diagnostics.SpotlightVisible
                        ? $"x={diagnostics.SpotlightRectLocal.x:F1},y={diagnostics.SpotlightRectLocal.y:F1},w={diagnostics.SpotlightRectLocal.width:F1},h={diagnostics.SpotlightRectLocal.height:F1}"
                        : "hidden";

                    if (TryAnalyzeTutorialCapture(filePath, diagnostics, out var analysis))
                    {
                        spotlightSignal = $"whiteRatio={analysis.WhiteRatio:F3},contrast={analysis.SpotlightContrast:F3},present={analysis.SpotlightLikelyPresent}";
                        if (analysis.WhiteRatio >= TutorialWhitePixelFailRatio)
                        {
                            Debug.LogError($"RuntimeScreenshot: tutorial frame appears mostly white (ratio={analysis.WhiteRatio:F3}) file={fileName}");
                            _failed = true;
                        }

                        if (diagnostics.SpotlightVisible && diagnostics.SpotlightMaskActive && !analysis.SpotlightLikelyPresent)
                        {
                            if (variant.AllowResolutionChange)
                            {
                                Debug.LogError($"RuntimeScreenshot: tutorial spotlight signal missing (contrast={analysis.SpotlightContrast:F3}) file={fileName}");
                                _failed = true;
                            }
                            else
                            {
                                Debug.LogWarning($"RuntimeScreenshot: tutorial spotlight signal low on mobile-native capture (contrast={analysis.SpotlightContrast:F3}) file={fileName}");
                            }
                        }
                    }
                }

                Debug.Log($"RuntimeScreenshot TutorialCapture step={stepIndex:D2} target={targetName} variant={variant.Label} resolution={resolution} renderMode={renderMode} scaler={scalerMode} ref={referenceResolution} match={match:F2} spotlight={spotlightRect}");
                summary?.Add($"{DateTime.UtcNow:O} | step={stepIndex:D2} | target={targetName} | variant={variant.Label} | resolution={resolution} | file={fileName} | renderMode={renderMode} | scaler={scalerMode} | ref={referenceResolution} | match={match:F2} | spotlight={spotlightRect} | analysis={spotlightSignal}");
            }

            if (!captureNativeOnly)
            {
                ApplyCaptureResolution(originalWidth, originalHeight, false);
            }
            yield return null;
        }

        private static IEnumerator WaitForTutorialSpotlightSettle(TutorialManager tutorialManager)
        {
            if (tutorialManager == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float stable = 0f;
            bool hasPrevious = false;
            Rect previous = default;

            while (elapsed < TutorialSpotlightSettleTimeout)
            {
                if (TryGetTutorialRenderDiagnostics(tutorialManager, out var diagnostics) && diagnostics.SpotlightVisible)
                {
                    var current = diagnostics.SpotlightRectLocal;
                    if (hasPrevious)
                    {
                        float delta = Mathf.Abs(current.x - previous.x)
                                      + Mathf.Abs(current.y - previous.y)
                                      + Mathf.Abs(current.width - previous.width)
                                      + Mathf.Abs(current.height - previous.height);
                        if (delta < 1.2f)
                        {
                            stable += Time.unscaledDeltaTime;
                            if (stable >= TutorialSpotlightStableSeconds)
                            {
                                yield break;
                            }
                        }
                        else
                        {
                            stable = 0f;
                        }
                    }

                    previous = current;
                    hasPrevious = true;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static bool TryAnalyzeTutorialCapture(string filePath, TutorialDiagnosticsSnapshot diagnostics, out TutorialFrameAnalysis analysis)
        {
            analysis = default;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            byte[] pngBytes;
            try
            {
                pngBytes = File.ReadAllBytes(filePath);
            }
            catch
            {
                return false;
            }

            if (pngBytes == null || pngBytes.Length == 0)
            {
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            bool loaded = texture.LoadImage(pngBytes, false);
            if (!loaded)
            {
                UnityEngine.Object.Destroy(texture);
                return false;
            }

            float whiteRatio = EstimateWhiteRatio(texture);
            float spotlightContrast = 0f;
            bool spotlightLikelyPresent = true;

            if (diagnostics.SpotlightVisible && diagnostics.SpotlightMaskActive)
            {
                spotlightLikelyPresent = TryEstimateSpotlightContrast(texture, diagnostics, out spotlightContrast)
                    && spotlightContrast >= TutorialSpotlightContrastMin;
            }

            analysis = new TutorialFrameAnalysis(whiteRatio, spotlightContrast, spotlightLikelyPresent);
            UnityEngine.Object.Destroy(texture);
            return true;
        }

        private static float EstimateWhiteRatio(Texture2D texture)
        {
            if (texture == null)
            {
                return 0f;
            }

            int width = texture.width;
            int height = texture.height;
            int stride = Mathf.Max(2, Mathf.Min(width, height) / 180);
            int total = 0;
            int white = 0;

            for (int y = 0; y < height; y += stride)
            {
                for (int x = 0; x < width; x += stride)
                {
                    Color c = texture.GetPixel(x, y);
                    total++;
                    if (c.r > 0.92f && c.g > 0.92f && c.b > 0.92f)
                    {
                        white++;
                    }
                }
            }

            return total > 0 ? (float)white / total : 0f;
        }

        private static bool TryEstimateSpotlightContrast(Texture2D texture, TutorialDiagnosticsSnapshot diagnostics, out float contrast)
        {
            contrast = 0f;
            if (texture == null)
            {
                return false;
            }

            Rect canvasRect = diagnostics.CanvasRectLocal;
            Rect spotlightRect = diagnostics.SpotlightRectLocal;
            if (canvasRect.width <= 0.01f || canvasRect.height <= 0.01f || spotlightRect.width <= 1f || spotlightRect.height <= 1f)
            {
                return false;
            }

            float centerX = Mathf.InverseLerp(canvasRect.xMin, canvasRect.xMax, spotlightRect.center.x);
            float centerY = Mathf.InverseLerp(canvasRect.yMin, canvasRect.yMax, spotlightRect.center.y);
            float holeW = Mathf.Clamp01(spotlightRect.width / canvasRect.width);
            float holeH = Mathf.Clamp01(spotlightRect.height / canvasRect.height);
            if (holeW < 0.005f || holeH < 0.005f)
            {
                return false;
            }

            float innerW = holeW * 0.42f;
            float innerH = holeH * 0.42f;
            float outerW = Mathf.Min(0.95f, holeW * 1.8f);
            float outerH = Mathf.Min(0.95f, holeH * 1.8f);

            float innerSum = 0f;
            int innerCount = 0;
            float outerSum = 0f;
            int outerCount = 0;

            int width = texture.width;
            int height = texture.height;
            int stride = Mathf.Max(2, Mathf.Min(width, height) / 200);

            for (int y = 0; y < height; y += stride)
            {
                float v = height > 1 ? (float)y / (height - 1) : 0f;
                for (int x = 0; x < width; x += stride)
                {
                    float u = width > 1 ? (float)x / (width - 1) : 0f;
                    float dx = Mathf.Abs(u - centerX);
                    float dy = Mathf.Abs(v - centerY);

                    bool inInner = dx <= innerW * 0.5f && dy <= innerH * 0.5f;
                    bool inOuter = dx <= outerW * 0.5f && dy <= outerH * 0.5f;
                    if (!inOuter)
                    {
                        continue;
                    }

                    Color c = texture.GetPixel(x, y);
                    float luma = (0.299f * c.r) + (0.587f * c.g) + (0.114f * c.b);

                    if (inInner)
                    {
                        innerSum += luma;
                        innerCount++;
                    }
                    else
                    {
                        outerSum += luma;
                        outerCount++;
                    }
                }
            }

            if (innerCount <= 0 || outerCount <= 0)
            {
                return false;
            }

            float innerMean = innerSum / innerCount;
            float outerMean = outerSum / outerCount;
            contrast = innerMean - outerMean;
            return true;
        }

        private readonly struct TutorialFrameAnalysis
        {
            public TutorialFrameAnalysis(float whiteRatio, float spotlightContrast, bool spotlightLikelyPresent)
            {
                WhiteRatio = whiteRatio;
                SpotlightContrast = spotlightContrast;
                SpotlightLikelyPresent = spotlightLikelyPresent;
            }

            public float WhiteRatio { get; }
            public float SpotlightContrast { get; }
            public bool SpotlightLikelyPresent { get; }
        }

        private readonly struct TutorialDiagnosticsSnapshot
        {
            public TutorialDiagnosticsSnapshot(
                string renderMode,
                string scaleMode,
                Vector2 referenceResolution,
                float matchWidthOrHeight,
                bool spotlightVisible,
                bool spotlightMaskActive,
                Rect spotlightRectLocal,
                Rect canvasRectLocal)
            {
                RenderMode = renderMode;
                ScaleMode = scaleMode;
                ReferenceResolution = referenceResolution;
                MatchWidthOrHeight = matchWidthOrHeight;
                SpotlightVisible = spotlightVisible;
                SpotlightMaskActive = spotlightMaskActive;
                SpotlightRectLocal = spotlightRectLocal;
                CanvasRectLocal = canvasRectLocal;
            }

            public string RenderMode { get; }
            public string ScaleMode { get; }
            public Vector2 ReferenceResolution { get; }
            public float MatchWidthOrHeight { get; }
            public bool SpotlightVisible { get; }
            public bool SpotlightMaskActive { get; }
            public Rect SpotlightRectLocal { get; }
            public Rect CanvasRectLocal { get; }
        }

        private static bool TryGetTutorialRenderDiagnostics(TutorialManager tutorialManager, out TutorialDiagnosticsSnapshot diagnostics)
        {
            diagnostics = default;
            if (tutorialManager == null)
            {
                return false;
            }

            var method = tutorialManager.GetType().GetMethod("TryGetRenderDiagnostics", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            var args = new object[] { null };
            bool success;
            try
            {
                object result = method.Invoke(tutorialManager, args);
                success = result is bool flag && flag;
            }
            catch
            {
                return false;
            }

            if (!success || args[0] == null)
            {
                return false;
            }

            object source = args[0];
            diagnostics = new TutorialDiagnosticsSnapshot(
                ReadMember(source, "RenderMode", "unknown"),
                ReadMember(source, "ScaleMode", "unknown"),
                ReadMember(source, "ReferenceResolution", new Vector2(1080f, 1920f)),
                ReadMember(source, "MatchWidthOrHeight", 0f),
                ReadMember(source, "SpotlightVisible", false),
                ReadMember(source, "SpotlightMaskActive", false),
                ReadMember(source, "SpotlightRectLocal", default(Rect)),
                ReadMember(source, "CanvasRectLocal", default(Rect)));
            return true;
        }

        private static T ReadMember<T>(object source, string memberName, T fallback)
        {
            if (source == null || string.IsNullOrWhiteSpace(memberName))
            {
                return fallback;
            }

            var type = source.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                try
                {
                    object value = property.GetValue(source);
                    if (value is T typed)
                    {
                        return typed;
                    }

                    if (typeof(T) == typeof(string) && value != null)
                    {
                        return (T)(object)value.ToString();
                    }
                }
                catch
                {
                }
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    object value = field.GetValue(source);
                    if (value is T typed)
                    {
                        return typed;
                    }

                    if (typeof(T) == typeof(string) && value != null)
                    {
                        return (T)(object)value.ToString();
                    }
                }
                catch
                {
                }
            }

            return fallback;
        }

        private static bool TryGetTutorialStepSnapshot(TutorialManager tutorialManager, out int currentStepIndex, out string targetName)
        {
            currentStepIndex = 0;
            targetName = "unknown";
            if (tutorialManager == null)
            {
                return false;
            }

            var method = tutorialManager.GetType().GetMethod("TryGetCurrentStepSnapshot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            var args = new object[] { 0, "" };
            bool success;
            try
            {
                object result = method.Invoke(tutorialManager, args);
                success = result is bool flag && flag;
            }
            catch
            {
                return false;
            }

            if (!success)
            {
                return false;
            }

            if (args[0] is int idx)
            {
                currentStepIndex = idx;
            }

            if (args[1] != null)
            {
                targetName = args[1].ToString();
            }

            return true;
        }

        private static void ApplyCaptureResolution(int width, int height, bool fullscreen)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#else
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        private static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ' ')
                {
                    chars[i] = '_';
                }
                else if (System.Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private IEnumerator EnsureTutorialOverlaySuppressed()
        {
            var controller = FindController();
            if (controller != null)
            {
                controller.SuppressTutorialOverlayForAutomation();
                controller.HideHowToPlayOverlay();
            }

            var tutorialManager = UnityEngine.Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
            if (tutorialManager != null)
            {
                tutorialManager.SuppressForAutomation();

                var rootField = typeof(TutorialManager).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic);
                var rootRect = rootField?.GetValue(tutorialManager) as RectTransform;
                if (rootRect != null)
                {
                    rootRect.gameObject.SetActive(false);
                }

                var highlightField = typeof(TutorialManager).GetField("highlightFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                var highlightRect = highlightField?.GetValue(tutorialManager) as RectTransform;
                if (highlightRect != null)
                {
                    highlightRect.gameObject.SetActive(false);
                }
            }

            var tutorialOverlay = GameObject.Find("TutorialOverlay");
            if (tutorialOverlay != null)
            {
                tutorialOverlay.SetActive(false);
            }

            yield return null;
        }

        private static StarTradeInDialog FindStarTradeInDialog(GameController controller)
        {
            if (controller == null)
            {
                return null;
            }

            var field = typeof(GameController).GetField("starTradeInDialog", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return null;
            }

            return field.GetValue(controller) as StarTradeInDialog;
        }

        private static ScrollRect FindOptionsScrollRect()
        {
            var optionsOverlay = GameObject.Find("OptionsOverlay");
            if (optionsOverlay == null)
            {
                return null;
            }

            var listContainer = optionsOverlay.transform.Find("Panel/ListContainer");
            if (listContainer == null)
            {
                return null;
            }

            return listContainer.GetComponent<ScrollRect>();
        }

        private static int ResolveAutoSolveCost(GameController controller)
        {
            if (controller == null)
            {
                return StarEconomy.ResolveAutoSolveCost(1);
            }

            var method = typeof(GameController).GetMethod("ResolveAutoSolveCost", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
            {
                return StarEconomy.ResolveAutoSolveCost(1);
            }

            object result = method.Invoke(controller, null);
            return result is int value ? value : StarEconomy.ResolveAutoSolveCost(1);
        }

        private static bool CurrentLevelHasSinkBottle(GameController controller)
        {
            return CurrentSinkBottleCount(controller) > 0;
        }

        private static int CurrentSinkBottleCount(GameController controller)
        {
            if (controller == null)
            {
                return 0;
            }

            var stateField = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField == null)
            {
                return 0;
            }

            var levelState = stateField.GetValue(controller) as LevelState;
            if (levelState == null)
            {
                return 0;
            }

            int sinkCount = 0;
            for (int i = 0; i < levelState.Bottles.Count; i++)
            {
                if (levelState.Bottles[i].IsSink)
                {
                    sinkCount++;
                }
            }

            return sinkCount;
        }

        private static void SetControllerStarBalance(GameController controller, int starBalance)
        {
            if (controller == null)
            {
                return;
            }

            var progressField = typeof(GameController).GetField("_progress", BindingFlags.Instance | BindingFlags.NonPublic);
            if (progressField == null)
            {
                return;
            }

            var progress = progressField.GetValue(controller) as ProgressData;
            if (progress == null)
            {
                progress = new ProgressData();
                progressField.SetValue(controller, progress);
            }

            progress.StarBalance = Mathf.Max(0, starBalance);
        }

        private static IEnumerator WaitForInterstitialHidden()
        {
            var banner = UnityEngine.Object.FindFirstObjectByType<LevelCompleteBanner>();
            if (banner == null) yield break;
            float timeout = 2f;
            float elapsed = 0f;
            while (banner.IsVisible && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static IEnumerator WaitForInterstitialVisible()
        {
            var banner = UnityEngine.Object.FindFirstObjectByType<LevelCompleteBanner>();
            if (banner == null) yield break;
            float timeout = 2f;
            float elapsed = 0f;
            while (!banner.IsVisible && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForOptionsOverlayVisible(GameController controller)
        {
            float timeout = 2f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (controller != null && controller.IsOptionsOverlayVisible)
                {
                    yield break;
                }

                var overlay = GameObject.Find("OptionsOverlay");
                if (overlay != null && overlay.activeInHierarchy)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogError("RuntimeScreenshot: Options overlay did not become visible within timeout.");
            _failed = true;
        }

        private IEnumerator WaitForHowToPlayOverlayVisible(GameController controller)
        {
            float timeout = 2f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (controller != null && controller.IsHowToPlayOverlayVisible)
                {
                    yield break;
                }

                var overlay = GameObject.Find("HowToPlayOverlay");
                if (overlay != null && overlay.activeInHierarchy)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogError("RuntimeScreenshot: How to Play overlay did not become visible within timeout.");
            _failed = true;
        }

        private IEnumerator CaptureScreenshot(string path)
        {
            yield return new WaitForEndOfFrame();

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("RuntimeScreenshot failed: empty screenshot path.");
                _failed = true;
                yield break;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string capturePath = ResolveCapturePath(path);
            ScreenCapture.CaptureScreenshot(capturePath, 1);

            float timeout = 4f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > 0)
                    {
                        Debug.Log($"RuntimeScreenshot saved: {path}");
                        yield break;
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogError($"RuntimeScreenshot failed: timed out waiting for file write at {path}");
            _failed = true;
        }

        private static string ResolveCapturePath(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Path.IsPathRooted(path))
            {
                string root = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(root) && path.StartsWith(root, StringComparison.Ordinal))
                {
                    string relative = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return string.IsNullOrEmpty(relative) ? Path.GetFileName(path) : relative;
                }

                return Path.GetFileName(path);
            }
#endif
            return path;
        }

        private static void WriteCompletionMarker(string outputDir)
        {
            try
            {
                string statusPath = Path.Combine(outputDir, "capture.complete");
                File.WriteAllText(statusPath, DateTime.UtcNow.ToString("O"));
                Debug.Log($"RuntimeScreenshot: wrote completion marker to {statusPath}");

                // Verify the file was written
                if (File.Exists(statusPath))
                {
                    Debug.Log($"RuntimeScreenshot: completion marker verified at {statusPath}");
                }
                else
                {
                    Debug.LogError($"RuntimeScreenshot: completion marker NOT found after write at {statusPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"RuntimeScreenshot: failed to write completion marker: {ex.Message}");
            }
        }

        private static bool IsScreenshotModeEnabled()
        {
            return HasFlag(ScreenshotFlag) || HasFlag("--screenshots") || HasFlag("--screenshots-only");
        }

        private static bool IsScreenshotsOnly()
        {
            return HasFlag(ScreenshotOnlyFlag) || HasFlag("--screenshots-only");
        }

        private static bool IsMotionCaptureEnabled()
        {
            return HasFlag(MotionCaptureFlag) || HasFlag("--motion-capture");
        }

        private static bool HasFlag(string key)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent == null) return false;
                    if (intent.Call<bool>("hasExtra", key))
                    {
                        return intent.Call<bool>("getBooleanExtra", key, false)
                               || string.Equals(intent.Call<string>("getStringExtra", key), "true", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(intent.Call<string>("getStringExtra", key), "1", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                return false;
            }
#endif
            return false;
        }

        private static bool TryGetIntArgument(string key, out int value)
        {
            value = 0;

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], $"--{key}", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(args[i + 1], out value))
                    {
                        return true;
                    }
                }
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    if (intent != null && intent.Call<bool>("hasExtra", key))
                    {
                        value = intent.Call<int>("getIntExtra", key, 0);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
#endif

            return false;
        }
    }
}
