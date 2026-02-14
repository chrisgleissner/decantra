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
using Decantra.Domain.Rules;
using Decantra.Presentation.Controller;
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
        private const int MotionFrameCount = 6;
        private const float MotionFrameIntervalMs = 600f;

        private static readonly string[] ScreenshotFiles =
        {
            "startup_fade_in_midpoint.png",
            "help_overlay.png",
            "options_panel_typography.png",
            "screenshot-01-launch.png",
            "screenshot-02-intro.png",
            "screenshot-03-level-01.png",
            "screenshot-08-level-10.png",
            "screenshot-04-level-12.png",
            "screenshot-09-level-20.png",
            "screenshot-05-level-24.png",
            "screenshot-06-interstitial.png",
            "screenshot-07-level-36.png",
            "screenshot-10-options.png"
        };

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

            string outputDir = Path.Combine(Application.persistentDataPath, OutputDirectoryName);
            Directory.CreateDirectory(outputDir);

            yield return CaptureFirstMoveShiftEvidence(controller, outputDir);

            yield return CaptureStartupFadeMidpoint(outputDir, ScreenshotFiles[0]);
            yield return CaptureLaunchScreenshot(outputDir);
            yield return CaptureIntroScreenshot(outputDir);
            yield return CaptureLevelScreenshot(controller, outputDir, 1, 10991, ScreenshotFiles[5]);
            yield return CaptureLevelScreenshot(controller, outputDir, 10, 421907, ScreenshotFiles[6]);
            yield return CaptureLevelScreenshot(controller, outputDir, 12, 473921, ScreenshotFiles[7]);
            yield return CaptureLevelScreenshot(controller, outputDir, 20, 682415, ScreenshotFiles[8]);
            yield return CaptureLevelScreenshot(controller, outputDir, 24, 873193, ScreenshotFiles[9]);
            yield return CaptureInterstitialScreenshot(outputDir);
            yield return CaptureLevelScreenshot(controller, outputDir, 36, 192731, ScreenshotFiles[11]);
            yield return CaptureOptionsScreenshot(controller, outputDir, ScreenshotFiles[2]);
            // Capture the options overlay twice: once with the new descriptive filename
            // ("options_panel_typography.png") and once with the legacy numbered filename
            // ("screenshot-10-options.png") to preserve backward compatibility with existing assets.
            yield return CaptureOptionsScreenshot(controller, outputDir, ScreenshotFiles[12]);
            yield return CaptureHelpOverlayScreenshot(controller, outputDir, ScreenshotFiles[1]);

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

            // Capture per-bottle positions BEFORE drag
            var beforePositions = CaptureBottlePositions(gridRect);
            var before = CaptureGridSnapshot(gridRect);
            Debug.Log($"RuntimeScreenshot GridBefore anchoredY={before.AnchoredY:F3} centerY={before.WorldCenterY:F3} minY={before.WorldMinY:F3} canvasScale={before.CanvasScale:F3} screen={before.ScreenWidth}x{before.ScreenHeight} safe={before.SafeAreaX:F1},{before.SafeAreaY:F1},{before.SafeAreaWidth:F1},{before.SafeAreaHeight:F1} bottles=[{before.BottleLocalYCsv}]");
            yield return CaptureScreenshot(Path.Combine(outputDir, InitialRenderFileName));

            // Simulate drag-release (the actual bug trigger) instead of tap-pour
            if (gridLayout != null)
            {
                gridLayout.enabled = false;
            }

            // Wait frames to let cumulative offset bug manifest (pre-fix)
            for (int f = 0; f < 10; f++)
            {
                yield return null;
            }

            // Simulate drag release
            if (gridLayout != null)
            {
                gridLayout.enabled = true;
            }

            var safeLayout = UnityEngine.Object.FindFirstObjectByType<Decantra.Presentation.View.HudSafeLayout>();
            if (safeLayout != null)
            {
                safeLayout.MarkLayoutDirty();
            }

            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();

            var afterPositions = CaptureBottlePositions(gridRect);
            var after = CaptureGridSnapshot(gridRect);
            Debug.Log($"RuntimeScreenshot GridAfter anchoredY={after.AnchoredY:F3} centerY={after.WorldCenterY:F3} minY={after.WorldMinY:F3} canvasScale={after.CanvasScale:F3} screen={after.ScreenWidth}x{after.ScreenHeight} safe={after.SafeAreaX:F1},{after.SafeAreaY:F1},{after.SafeAreaWidth:F1},{after.SafeAreaHeight:F1} bottles=[{after.BottleLocalYCsv}]");
            yield return CaptureScreenshot(Path.Combine(outputDir, AfterFirstMoveFileName));

            // Compute per-bottle deltas and max shift
            float maxBottleDelta = 0f;
            string worstBottle = "";
            var reportBuilder = new System.Text.StringBuilder();
            reportBuilder.AppendLine("{");
            reportBuilder.AppendLine($"  \"resolution\": \"{before.ScreenWidth}x{before.ScreenHeight}\",");
            reportBuilder.AppendLine($"  \"scaleFactor\": {before.CanvasScale:F4},");
            reportBuilder.AppendLine($"  \"safeArea\": {{ \"x\": {before.SafeAreaX:F1}, \"y\": {before.SafeAreaY:F1}, \"w\": {before.SafeAreaWidth:F1}, \"h\": {before.SafeAreaHeight:F1} }},");
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
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[3]));
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
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[4]));
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
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[10]));
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

        private IEnumerator CaptureHelpOverlayScreenshot(GameController controller, string outputDir, string fileName)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

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
