/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
        private const string SinkIndicatorLightFileName = "sink_indicator_light.png";
        private const string SinkIndicatorDarkFileName = "sink_indicator_dark.png";
        private const string SinkIndicatorComparisonFileName = "sink_indicator_comparison.png";
        private const string AutoSolveStartFileName = "auto_solve_start.png";
        private const string AutoSolveStepPrefix = "auto_solve_step_";
        private const string AutoSolveCompleteFileName = "auto_solve_complete.png";
        private const float AutoSolveMinDragSeconds = 0.35f;
        private const float AutoSolveMaxDragSeconds = 1.0f;
        private const string LaunchFileName = "screenshot-01-launch.png";
        private const string IntroFileName = "screenshot-02-intro.png";
        private const string Level01FileName = "screenshot-03-level-01.png";
        private const string Level10FileName = "screenshot-08-level-10.png";
        private const string Level12FileName = "screenshot-04-level-12.png";
        private const string Level20FileName = "screenshot-09-level-20.png";
        private const string Level24FileName = "screenshot-05-level-24.png";
        private const string InterstitialFileName = "screenshot-06-interstitial.png";
        private const string Level36FileName = "screenshot-07-level-36.png";
        private const string OptionsLegacyFileName = "screenshot-10-options.png";
        private const string TutorialPageFilePrefix = "how_to_play_tutorial_page_";
        private const int MaxTutorialPagesToCapture = 12;
        private const int MotionFrameCount = 6;
        private const float MotionFrameIntervalMs = 600f;

        private bool _failed;

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
            while (controller == null)
            {
                yield return null;
                controller = FindController();
            }

            yield return WaitForControllerReady(controller);
            yield return EnsureTutorialOverlaySuppressed();

            string outputDir = Path.Combine(Application.persistentDataPath, OutputDirectoryName);
            Directory.CreateDirectory(outputDir);

            yield return CaptureFirstMoveShiftEvidence(controller, outputDir);

            yield return CaptureStartupFadeMidpoint(outputDir, StartupFadeMidpointFileName);
            yield return CaptureLaunchScreenshot(outputDir);
            yield return CaptureIntroScreenshot(outputDir);
            yield return CaptureTutorialPages(controller, outputDir);
            yield return CaptureLevelScreenshot(controller, outputDir, 1, 10991, Level01FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 10, 421907, Level10FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 12, 473921, Level12FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 20, 682415, Level20FileName);
            yield return CaptureLevelScreenshot(controller, outputDir, 24, 873193, Level24FileName);
            yield return CaptureSinkIndicatorScreenshots(controller, outputDir);
            yield return CaptureAutoSolveEvidence(controller, outputDir);
            yield return CaptureInterstitialScreenshot(outputDir);
            yield return CaptureLevelScreenshot(controller, outputDir, 36, 192731, Level36FileName);
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

            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.LoadLevel(36, 192731);
            yield return WaitForControllerReady(controller);
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

            // Capture per-bottle positions BEFORE first move
            var beforePositions = CaptureBottlePositions(gridRect);
            var before = CaptureGridSnapshot(gridRect);
            var beforeClearance = MeasureOptionsToBottleClearance();
            Debug.Log($"RuntimeScreenshot GridBefore anchoredY={before.AnchoredY:F3} centerY={before.WorldCenterY:F3} minY={before.WorldMinY:F3} canvasScale={before.CanvasScale:F3} screen={before.ScreenWidth}x{before.ScreenHeight} safe={before.SafeAreaX:F1},{before.SafeAreaY:F1},{before.SafeAreaWidth:F1},{before.SafeAreaHeight:F1} bottles=[{before.BottleLocalYCsv}]");
            yield return CaptureScreenshot(Path.Combine(outputDir, InitialRenderFileName));
            Debug.Log($"RuntimeScreenshot Level36Before optionsBottomY={beforeClearance.OptionsBottomY:F3} bottleTopY={beforeClearance.BottleTopY:F3} gapPx={beforeClearance.GapPx:F3} bottle={beforeClearance.BottleName} brightUnderOptions={beforeClearance.BrightPixelsUnderOptions}");

            // Perform the first valid user move on level 36.
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
                    Debug.LogError($"RuntimeScreenshot: failed to start first move source={sourceIndex} target={targetIndex}.");
                    _failed = true;
                }
            }
            else
            {
                Debug.LogError("RuntimeScreenshot: no valid first move found on level 36.");
                _failed = true;
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
            if (optionCorners == null || optionCorners.Length < 4 || bottleRect == null)
            {
                return false;
            }

            var bottleCorners = new Vector3[4];
            bottleRect.GetWorldCorners(bottleCorners);

            int xMin = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(optionCorners[0].x, bottleCorners[0].x)), 0, Screen.width - 1);
            int xMax = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(optionCorners[2].x, bottleCorners[2].x)), 0, Screen.width - 1);
            if (xMax <= xMin)
            {
                return false;
            }

            int yTop = Mathf.Clamp(Mathf.FloorToInt(optionCorners[0].y) - 1, 0, Screen.height - 1);
            int yBottom = Mathf.Clamp(yTop - 14, 0, Screen.height - 1);
            if (yTop <= yBottom)
            {
                return false;
            }

            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
            texture.Apply(false, false);

            bool found = false;
            for (int y = yBottom; y <= yTop && !found; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Color c = texture.GetPixel(x, y);
                    float brightness = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (brightness > 0.5f)
                    {
                        found = true;
                        break;
                    }
                }
            }

            UnityEngine.Object.Destroy(texture);
            return found;
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

        private IEnumerator CaptureSinkIndicatorScreenshots(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            yield return EnsureTutorialOverlaySuppressed();
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();

            controller.LoadLevel(20, 682415);
            yield return WaitForControllerReady(controller);
            yield return new WaitForSeconds(0.25f);

            var background = TryGetBackgroundImage();
            Color originalBackground = background != null ? background.color : Color.black;

            try
            {
                if (background != null)
                {
                    background.color = new Color(0.92f, 0.95f, 1f, 1f);
                    background.SetAllDirty();
                }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return CaptureScreenshot(Path.Combine(outputDir, SinkIndicatorLightFileName));

                if (background != null)
                {
                    background.color = new Color(0.08f, 0.11f, 0.18f, 1f);
                    background.SetAllDirty();
                }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return CaptureScreenshot(Path.Combine(outputDir, SinkIndicatorDarkFileName));

                if (background != null)
                {
                    background.color = originalBackground;
                    background.SetAllDirty();
                }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                yield return CaptureScreenshot(Path.Combine(outputDir, SinkIndicatorComparisonFileName));
            }
            finally
            {
                if (background != null)
                {
                    background.color = originalBackground;
                    background.SetAllDirty();
                }
            }
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

            if (!TryGetCurrentStateSnapshot(controller, out var solveState))
            {
                Debug.LogError("RuntimeScreenshot: Could not access level state for auto-solve evidence capture.");
                _failed = true;
                yield break;
            }

            var solver = new BfsSolver();
            var result = solver.SolveWithPath(solveState, 8_000_000, 8_000, allowSinkMoves: true);
            if (result == null || result.Path == null || result.Path.Count == 0)
            {
                Debug.LogError("RuntimeScreenshot: Solver returned no path for auto-solve evidence capture.");
                _failed = true;
                yield break;
            }

            int successfulMoves = 0;
            int capturedDragFrames = 0;
            int capturedPourFrames = 0;

            for (int i = 0; i < result.Path.Count; i++)
            {
                var move = result.Path[i];
                string stepIndex = (i + 1).ToString("D2");
                string dragPath = Path.Combine(outputDir, $"{AutoSolveStepPrefix}{stepIndex}_drag.png");
                string pourPath = Path.Combine(outputDir, $"{AutoSolveStepPrefix}{stepIndex}_pour.png");

                bool dragCaptured = false;
                yield return AnimateDragForCapture(controller, move.Source, move.Target, dragPath, () => dragCaptured = true);
                if (dragCaptured)
                {
                    capturedDragFrames++;
                }

                if (!controller.TryStartMove(move.Source, move.Target, out float pourDuration))
                {
                    Debug.LogError($"RuntimeScreenshot: Failed to start auto-solve move {i} ({move.Source}->{move.Target}).");
                    _failed = true;
                    yield break;
                }
                successfulMoves++;

                float pourCaptureDelay = Mathf.Clamp(pourDuration * 0.5f, 0.12f, 0.65f);
                yield return new WaitForSeconds(pourCaptureDelay);
                yield return CaptureScreenshot(pourPath);
                capturedPourFrames++;

                float timeout = 8f;
                float elapsed = 0f;
                while (controller.IsInputLocked && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (elapsed >= timeout)
                {
                    Debug.LogError("RuntimeScreenshot: Timed out waiting for auto-solve move animation to finish.");
                    _failed = true;
                    yield break;
                }

                yield return new WaitForSeconds(0.06f);
            }

            if (successfulMoves <= 0 || capturedDragFrames <= 0 || capturedPourFrames <= 0)
            {
                Debug.LogError("RuntimeScreenshot: Auto-solve evidence capture did not record move playback frames.");
                _failed = true;
                yield break;
            }

            yield return new WaitForSeconds(0.15f);
            yield return CaptureScreenshot(Path.Combine(outputDir, AutoSolveCompleteFileName));
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
            float duration = Mathf.Lerp(AutoSolveMinDragSeconds, AutoSolveMaxDragSeconds, normalizedDistance);
            float lift = Mathf.Lerp(22f, 62f, normalizedDistance);

            bool captured = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 planar = Vector2.Lerp(start, end, t);
                float arc = Mathf.Sin(t * Mathf.PI) * lift;
                sourceRect.anchoredPosition = planar + Vector2.up * arc;

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

            sourceRect.anchoredPosition = start;
            yield return null;
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

            int page = 1;
            while (tutorialManager.IsRunning && page <= MaxTutorialPagesToCapture)
            {
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.16f);
                string pagePath = Path.Combine(outputDir, $"{TutorialPageFilePrefix}{page:D2}.png");
                yield return CaptureScreenshot(pagePath);

                tutorialManager.AdvanceStepForAutomation();
                page++;
                yield return null;
            }

            if (page <= 2)
            {
                Debug.LogError("RuntimeScreenshot: tutorial page capture produced too few pages.");
                _failed = true;
            }

            if (tutorialManager.IsRunning)
            {
                Debug.LogWarning("RuntimeScreenshot: tutorial capture reached max page limit; forcing dismiss.");
            }

            yield return EnsureTutorialOverlaySuppressed();
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

            for (int i = 0; i < levelState.Bottles.Count; i++)
            {
                if (levelState.Bottles[i].IsSink)
                {
                    return true;
                }
            }

            return false;
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
            try
            {
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                {
                    Debug.LogError("RuntimeScreenshot failed: texture null");
                    _failed = true;
                    yield break;
                }

                var bytes = texture.EncodeToPNG();
                Destroy(texture);
                File.WriteAllBytes(path, bytes);
                if (!File.Exists(path) || bytes == null || bytes.Length == 0)
                {
                    _failed = true;
                }
                Debug.Log($"RuntimeScreenshot saved: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"RuntimeScreenshot failed: {ex.Message}");
                _failed = true;
            }
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
    }
}
