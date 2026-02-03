/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using System.IO;
using Decantra.Presentation.Controller;
using UnityEngine;

namespace Decantra.Presentation
{
    public sealed class RuntimeScreenshot : MonoBehaviour
    {
        private const string ScreenshotFlag = "decantra_screenshots";
        private const string ScreenshotOnlyFlag = "decantra_screenshots_only";
        private const string OutputDirectoryName = "DecantraScreenshots";

        private static readonly string[] ScreenshotFiles =
        {
            "screenshot-01-launch.png",
            "screenshot-02-initial-level.png",
            "screenshot-03-interstitial.png",
            "screenshot-04-advanced-level.png"
        };

        private bool _failed;

        private void Start()
        {
            if (!IsScreenshotModeEnabled())
            {
                Destroy(this);
                return;
            }

            Debug.Log("RuntimeScreenshot: capture sequence enabled");
            Debug.Log($"RuntimeScreenshot path: {Application.persistentDataPath}");
            DontDestroyOnLoad(gameObject);
            StartCoroutine(CaptureSequence());
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

            yield return CaptureLaunchScreenshot(outputDir);
            yield return CaptureInitialLevelScreenshot(controller, outputDir);
            yield return CaptureInterstitialScreenshot(outputDir);
            yield return CaptureAdvancedLevelScreenshot(controller, outputDir);

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

        private static GameController FindController()
        {
            return UnityEngine.Object.FindFirstObjectByType<GameController>();
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

        private IEnumerator CaptureLaunchScreenshot(string outputDir)
        {
            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            yield return new WaitForEndOfFrame();
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[0]));
        }

        private IEnumerator CaptureInitialLevelScreenshot(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.LoadLevel(1, 10991);
            yield return new WaitForSeconds(0.8f);
            yield return new WaitForEndOfFrame();
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[1]));
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
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[2]));
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

        private IEnumerator CaptureAdvancedLevelScreenshot(GameController controller, string outputDir)
        {
            if (controller == null)
            {
                _failed = true;
                yield break;
            }

            HideInterstitialIfAny();
            yield return WaitForInterstitialHidden();
            controller.LoadLevel(12, 473921);
            yield return new WaitForSeconds(0.9f);
            yield return new WaitForEndOfFrame();
            yield return CaptureScreenshot(Path.Combine(outputDir, ScreenshotFiles[3]));
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
