using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Decantra.Domain.Model;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Decantra.Tests.PlayMode
{
    public sealed class VisualVerificationCapturePlayModeTests
    {
        private const float SilhouetteMin = 1.6f;
        private const float LayerThicknessMinPx = 4f;

        [UnityTest]
        public IEnumerator GenerateVisualVerificationArtifacts()
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            SceneBootstrap.EnsureScene();
            yield return null;
            yield return null;

            var controller = ResolvePrimaryController();
            Assert.NotNull(controller, "GameController not found.");
            EnsureColorPaletteWired(controller);

            string projectRoot = Directory.GetCurrentDirectory();
            string screenshotsDir = Path.Combine(projectRoot, "docs", "repro", "visual-verification", "screenshots");
            string reportsDir = Path.Combine(projectRoot, "docs", "repro", "visual-verification", "reports");
            Directory.CreateDirectory(screenshotsDir);
            Directory.CreateDirectory(reportsDir);

            DeleteIfExists(Path.Combine(screenshotsDir, "level-20.png"));
            DeleteIfExists(Path.Combine(screenshotsDir, "level-24.png"));
            DeleteIfExists(Path.Combine(screenshotsDir, "level-36.png"));
            DeleteIfExists(Path.Combine(screenshotsDir, "level-3x3.png"));
            DeleteIfExists(Path.Combine(screenshotsDir, "completed-bottles-corks.png"));
            DeleteIfExists(Path.Combine(screenshotsDir, "empty-bottles.png"));

            float minimumLayerThicknessPixels = float.MaxValue;

            yield return LoadLevel(controller, 20, 682415);
            yield return CaptureScreenshot(Path.Combine(screenshotsDir, "level-20.png"));
            minimumLayerThicknessPixels = Mathf.Min(minimumLayerThicknessPixels, ComputeMinimumLayerThicknessPixels(controller));

            yield return LoadLevel(controller, 24, 873193);
            string level24Path = Path.Combine(screenshotsDir, "level-24.png");
            yield return CaptureScreenshot(level24Path);
            minimumLayerThicknessPixels = Mathf.Min(minimumLayerThicknessPixels, ComputeMinimumLayerThicknessPixels(controller));

            yield return LoadLevel(controller, 36, 192731);
            yield return CaptureScreenshot(Path.Combine(screenshotsDir, "level-36.png"));
            minimumLayerThicknessPixels = Mathf.Min(minimumLayerThicknessPixels, ComputeMinimumLayerThicknessPixels(controller));

            yield return LoadLevel(controller, 12, 473921);
            yield return CaptureScreenshot(Path.Combine(screenshotsDir, "level-3x3.png"));

            yield return CaptureCompletedBottleScene(controller, Path.Combine(screenshotsDir, "completed-bottles-corks.png"));

            InvokeBottle3DWriteReport();
            yield return null;

            string runtimeReportPath = Path.Combine(Application.persistentDataPath, "DecantraScreenshots", "cork-layout-report.json");
            Assert.That(File.Exists(runtimeReportPath), Is.True, $"Missing runtime layout report: {runtimeReportPath}");
            var baseReport = JsonUtility.FromJson<BaseLayoutReport>(File.ReadAllText(runtimeReportPath));
            Assert.NotNull(baseReport, "Failed to parse runtime cork layout report.");

            yield return CaptureEmptyBottleScene(controller, Path.Combine(screenshotsDir, "empty-bottles.png"));
            float silhouetteContrastRatio = ComputeEmptyBottleSilhouetteContrast(controller, Path.Combine(screenshotsDir, "empty-bottles.png"));

            if (minimumLayerThicknessPixels == float.MaxValue)
            {
                minimumLayerThicknessPixels = 0f;
            }

            var finalReport = new VisualLayoutReport
            {
                bottleCount = baseReport.bottleCount,
                bottleOverlapDetected = baseReport.bottleOverlapDetected,
                shadowOverlapDetected = baseReport.shadowOverlapDetected,
                hudIntrusionDetected = baseReport.hudIntrusionDetected,
                corkCount = baseReport.corkCount,
                completedBottleCount = baseReport.completedBottleCount,
                silhouetteContrastRatio = silhouetteContrastRatio,
                minimumLayerThicknessPixels = minimumLayerThicknessPixels,
                corkAspectRatioMin = baseReport.corkAspectRatioMin,
                corkAspectRatioMax = baseReport.corkAspectRatioMax,
                corkInsertionDepthRatioMin = baseReport.corkInsertionDepthRatioMin,
                corkInsertionDepthRatioMax = baseReport.corkInsertionDepthRatioMax,
                generatedAt = DateTime.UtcNow.ToString("O"),
                source = "playmode-visual-verification"
            };

            string reportPath = Path.Combine(reportsDir, "layout-report.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(finalReport, true));

            Assert.False(finalReport.bottleOverlapDetected, "Bottle overlap detected.");
            Assert.False(finalReport.shadowOverlapDetected, "Shadow overlap detected.");
            Assert.False(finalReport.hudIntrusionDetected, "HUD intrusion detected.");
            Assert.AreEqual(finalReport.completedBottleCount, finalReport.corkCount, "corkCount != completedBottleCount.");
            Assert.GreaterOrEqual(finalReport.silhouetteContrastRatio, SilhouetteMin, "Silhouette contrast below threshold.");
            Assert.GreaterOrEqual(finalReport.minimumLayerThicknessPixels, LayerThicknessMinPx, "Thin liquid layer below threshold.");
            Assert.GreaterOrEqual(finalReport.corkAspectRatioMin, 1.2f, "Cork aspect ratio min below threshold.");
            Assert.LessOrEqual(finalReport.corkAspectRatioMax, 2.0f, "Cork aspect ratio max above threshold.");
            Assert.GreaterOrEqual(finalReport.corkInsertionDepthRatioMin, 0.7f, "Cork insertion depth min below threshold.");
            Assert.LessOrEqual(finalReport.corkInsertionDepthRatioMax, 0.8f, "Cork insertion depth max above threshold.");

            LogAssert.ignoreFailingMessages = previousIgnore;
        }

        private static IEnumerator LoadLevel(GameController controller, int level, int seed)
        {
            controller.LoadLevel(level, seed);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private static IEnumerator CaptureCompletedBottleScene(GameController controller, string screenshotPath)
        {
            var stateField = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            var renderMethod = typeof(GameController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(stateField);
            Assert.NotNull(renderMethod);

            var original = stateField.GetValue(controller) as LevelState;
            Assert.NotNull(original);

            var solvedBottles = new List<Bottle>(9);
            for (int i = 0; i < 7; i++)
            {
                var color = (ColorId)(i % 8);
                solvedBottles.Add(new Bottle(new ColorId?[4] { color, color, color, color }));
            }

            solvedBottles.Add(new Bottle(4));
            solvedBottles.Add(new Bottle(4));

            var solved = new LevelState(
                solvedBottles,
                original.MovesUsed,
                original.MovesAllowed,
                original.OptimalMoves,
                original.LevelIndex,
                original.Seed,
                original.ScrambleMoves,
                original.BackgroundPaletteIndex);

            stateField.SetValue(controller, solved);
            renderMethod.Invoke(controller, null);
            yield return null;
            yield return CaptureScreenshot(screenshotPath);
        }

        private static IEnumerator CaptureEmptyBottleScene(GameController controller, string screenshotPath)
        {
            int[] levels = { 24, 20, 36, 10, 1 };
            int[] seeds = { 873193, 682415, 192731, 421907, 10991 };

            for (int i = 0; i < levels.Length; i++)
            {
                yield return LoadLevel(controller, levels[i], seeds[i]);
                if (CountEmptyBottles(controller) > 0)
                {
                    break;
                }
            }

            yield return CaptureScreenshot(screenshotPath);
        }

        private static float ComputeMinimumLayerThicknessPixels(GameController controller)
        {
            if (!TryCollectBottleScreenSamples(controller, out var samples))
            {
                return 0f;
            }

            float minThickness = float.MaxValue;
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (sample.Capacity <= 0 || sample.Count <= 0)
                {
                    continue;
                }

                float slotThickness = sample.ScreenRect.height / sample.Capacity;
                if (slotThickness < minThickness)
                {
                    minThickness = slotThickness;
                }
            }

            return minThickness == float.MaxValue ? 0f : minThickness;
        }

        private static float ComputeEmptyBottleSilhouetteContrast(GameController controller, string screenshotPath)
        {
            if (!TryLoadTexture(screenshotPath, out var texture))
            {
                return 0f;
            }

            try
            {
                if (!TryCollectBottleScreenSamples(controller, out var samples))
                {
                    return 0f;
                }

                float total = 0f;
                int count = 0;
                for (int i = 0; i < samples.Count; i++)
                {
                    if (!samples[i].IsEmpty)
                    {
                        continue;
                    }

                    float ratio = EstimateSilhouetteContrastRatio(texture, samples[i].ScreenRect);
                    if (ratio > 0f)
                    {
                        total += ratio;
                        count++;
                    }
                }

                return count > 0 ? total / count : 0f;
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static bool TryCollectBottleScreenSamples(GameController controller, out List<BottleScreenSample> samples)
        {
            samples = new List<BottleScreenSample>(9);
            var camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (controller == null || camera == null)
            {
                return false;
            }

            var stateField = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            var state = stateField?.GetValue(controller) as LevelState;
            var views = ResolveBottleViews(controller);
            if (state == null || views == null)
            {
                return false;
            }

            int count = Mathf.Min(state.Bottles.Count, views.Count);
            for (int i = 0; i < count; i++)
            {
                var view = views[i];
                var bottle = state.Bottles[i];
                if (view == null || bottle == null)
                {
                    continue;
                }

                if (!TryGetBottleScreenRect(view, camera, out var rect))
                {
                    continue;
                }

                samples.Add(new BottleScreenSample(rect, bottle.IsEmpty, bottle.Capacity, bottle.Count));
            }

            return samples.Count > 0;
        }

        private static bool TryGetBottleScreenRect(BottleView view, Camera camera, out Rect rect)
        {
            rect = default;
            if (view == null || camera == null)
            {
                return false;
            }

            var bottle3D = view.GetComponent("Bottle3DView");
            if (bottle3D != null)
            {
                var rootProperty = bottle3D.GetType().GetProperty("WorldRootTransform", BindingFlags.Instance | BindingFlags.Public);
                var root = rootProperty?.GetValue(bottle3D) as Transform;
                if (root != null)
                {
                    var glass = root.Find("GlassBody");
                    if (glass != null)
                    {
                        var renderer = glass.GetComponent<MeshRenderer>();
                        if (renderer != null && TryProjectBoundsToScreenRect(camera, renderer.bounds, out rect))
                        {
                            return true;
                        }
                    }
                }
            }

            return TryGetBottleViewScreenRect(view, camera, out rect);
        }

        private static bool TryGetBottleViewScreenRect(BottleView view, Camera camera, out Rect rect)
        {
            rect = default;
            if (view == null)
            {
                return false;
            }

            var rectTransform = view.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return false;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }

            minX = Mathf.Clamp(minX, 0f, Screen.width - 1f);
            maxX = Mathf.Clamp(maxX, 0f, Screen.width - 1f);
            minY = Mathf.Clamp(minY, 0f, Screen.height - 1f);
            maxY = Mathf.Clamp(maxY, 0f, Screen.height - 1f);

            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool TryProjectBoundsToScreenRect(Camera camera, Bounds bounds, out Rect rect)
        {
            rect = default;
            var corners = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool any = false;

            for (int i = 0; i < corners.Length; i++)
            {
                var p = camera.WorldToScreenPoint(corners[i]);
                if (p.z <= 0f) continue;
                any = true;
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }

            if (!any)
            {
                return false;
            }

            minX = Mathf.Clamp(minX, 0f, Screen.width - 1f);
            maxX = Mathf.Clamp(maxX, 0f, Screen.width - 1f);
            minY = Mathf.Clamp(minY, 0f, Screen.height - 1f);
            maxY = Mathf.Clamp(maxY, 0f, Screen.height - 1f);
            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool TryLoadTexture(string path, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
                return false;
            }

            return true;
        }

        private static float EstimateSilhouetteContrastRatio(Texture2D texture, Rect screenRect)
        {
            if (texture == null || screenRect.width < 4f || screenRect.height < 4f)
            {
                return 0f;
            }

            int xMin = Mathf.Clamp(Mathf.RoundToInt(screenRect.xMin), 0, texture.width - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(screenRect.xMax), 0, texture.width - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(screenRect.yMin), 0, texture.height - 1);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(screenRect.yMax), 0, texture.height - 1);
            if (xMax <= xMin || yMax <= yMin)
            {
                return 0f;
            }

            int edgeThickness = Mathf.Max(2, Mathf.RoundToInt(Mathf.Min(xMax - xMin, yMax - yMin) * 0.06f));
            float edgeLuma = MeanLumaOnBorder(texture, xMin, xMax, yMin, yMax, edgeThickness);

            int ring = edgeThickness * 2;
            int oxMin = Mathf.Max(0, xMin - ring);
            int oxMax = Mathf.Min(texture.width - 1, xMax + ring);
            int oyMin = Mathf.Max(0, yMin - ring);
            int oyMax = Mathf.Min(texture.height - 1, yMax + ring);
            float bgLuma = MeanLumaInRing(texture, oxMin, oxMax, oyMin, oyMax, xMin, xMax, yMin, yMax);

            Color edgeColor = MeanColorOnBorder(texture, xMin, xMax, yMin, yMax, edgeThickness);
            Color bgColor = MeanColorInRing(texture, oxMin, oxMax, oyMin, oyMax, xMin, xMax, yMin, yMax);

            float bright = Mathf.Max(edgeLuma, bgLuma);
            float dark = Mathf.Min(edgeLuma, bgLuma);
            float lumaRatio = (bright + 0.05f) / Mathf.Max(0.0001f, dark + 0.05f);

            Vector3 edgeRgb = new Vector3(edgeColor.r, edgeColor.g, edgeColor.b);
            Vector3 bgRgb = new Vector3(bgColor.r, bgColor.g, bgColor.b);
            float rgbDistance = Vector3.Distance(edgeRgb, bgRgb);
            float chromaRatio = 1f + rgbDistance * 6f;

            return Mathf.Max(lumaRatio, chromaRatio);
        }

        private static float MeanLumaOnBorder(Texture2D texture, int xMin, int xMax, int yMin, int yMax, int thickness)
        {
            float sum = 0f;
            int count = 0;
            for (int y = yMin; y <= yMax; y += 2)
            {
                for (int x = xMin; x <= xMax; x += 2)
                {
                    bool border = (x - xMin) < thickness || (xMax - x) < thickness || (y - yMin) < thickness || (yMax - y) < thickness;
                    if (!border) continue;
                    Color c = texture.GetPixel(x, y);
                    sum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        private static Color MeanColorOnBorder(Texture2D texture, int xMin, int xMax, int yMin, int yMax, int thickness)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int y = yMin; y <= yMax; y += 2)
            {
                for (int x = xMin; x <= xMax; x += 2)
                {
                    bool border = (x - xMin) < thickness || (xMax - x) < thickness || (y - yMin) < thickness || (yMax - y) < thickness;
                    if (!border) continue;
                    Color c = texture.GetPixel(x, y);
                    sum += new Vector3(c.r, c.g, c.b);
                    count++;
                }
            }

            if (count <= 0) return Color.black;
            Vector3 avg = sum / count;
            return new Color(avg.x, avg.y, avg.z, 1f);
        }

        private static float MeanLumaInRing(Texture2D texture, int xMinOuter, int xMaxOuter, int yMinOuter, int yMaxOuter, int xMinInner, int xMaxInner, int yMinInner, int yMaxInner)
        {
            float sum = 0f;
            int count = 0;
            for (int y = yMinOuter; y <= yMaxOuter; y += 2)
            {
                for (int x = xMinOuter; x <= xMaxOuter; x += 2)
                {
                    bool inInner = x >= xMinInner && x <= xMaxInner && y >= yMinInner && y <= yMaxInner;
                    if (inInner) continue;
                    Color c = texture.GetPixel(x, y);
                    sum += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        private static Color MeanColorInRing(Texture2D texture, int xMinOuter, int xMaxOuter, int yMinOuter, int yMaxOuter, int xMinInner, int xMaxInner, int yMinInner, int yMaxInner)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int y = yMinOuter; y <= yMaxOuter; y += 2)
            {
                for (int x = xMinOuter; x <= xMaxOuter; x += 2)
                {
                    bool inInner = x >= xMinInner && x <= xMaxInner && y >= yMinInner && y <= yMaxInner;
                    if (inInner) continue;
                    Color c = texture.GetPixel(x, y);
                    sum += new Vector3(c.r, c.g, c.b);
                    count++;
                }
            }

            if (count <= 0) return Color.black;
            Vector3 avg = sum / count;
            return new Color(avg.x, avg.y, avg.z, 1f);
        }

        private static IEnumerator CaptureScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);

            yield return new WaitForEndOfFrame();
            Texture2D tex;
            if (Application.isBatchMode)
            {
                tex = CaptureFromCamera();
            }
            else
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                if (tex == null)
                {
                    tex = CaptureFromCamera();
                }
            }

            Assert.NotNull(tex, "No screenshot texture could be captured.");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);

            float timeout = Time.realtimeSinceStartup + 2f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(File.Exists(path), Is.True, $"Screenshot was not captured: {path}");
            yield return null;
        }

        private static Texture2D CaptureFromCamera()
        {
            var camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                return null;
            }

            int width = Mathf.Max(1280, Screen.width);
            int height = Mathf.Max(720, Screen.height);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D tex = null;

            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }

            return tex;
        }

        private static int CountEmptyBottles(GameController controller)
        {
            var field = typeof(GameController).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
            var state = field?.GetValue(controller) as LevelState;
            if (state == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < state.Bottles.Count; i++)
            {
                if (state.Bottles[i] != null && state.Bottles[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private static GameController ResolvePrimaryController()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameController best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < controllers.Length; i++)
            {
                var candidate = controllers[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = candidate.gameObject.activeInHierarchy ? 100 : 0;
                score += ResolveBottleViews(candidate).Count * 10;

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static List<BottleView> ResolveBottleViews(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            var views = field?.GetValue(controller) as List<BottleView>;
            return views ?? new List<BottleView>();
        }

        private static void EnsureColorPaletteWired(GameController controller)
        {
            if (controller == null)
            {
                return;
            }

            var field = typeof(GameController).GetField("_colorPalette", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            if (field.GetValue(controller) != null)
            {
                return;
            }

            var palette = UnityEngine.Object.FindFirstObjectByType<ColorPalette>();
            if (palette != null)
            {
                field.SetValue(controller, palette);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void InvokeBottle3DWriteReport()
        {
            Type bottle3DType = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                bottle3DType = assemblies[i].GetType("Decantra.Presentation.View3D.Bottle3DView");
                if (bottle3DType != null)
                {
                    break;
                }
            }

            Assert.NotNull(bottle3DType, "Bottle3DView type not found.");
            var method = bottle3DType.GetMethod("WriteReport", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method, "Bottle3DView.WriteReport method not found.");
            method.Invoke(null, null);
        }

        [Serializable]
        private sealed class BaseLayoutReport
        {
            public int bottleCount;
            public bool bottleOverlapDetected;
            public bool shadowOverlapDetected;
            public bool hudIntrusionDetected;
            public int corkCount;
            public int completedBottleCount;
            public float corkAspectRatioMin;
            public float corkAspectRatioMax;
            public float corkInsertionDepthRatioMin;
            public float corkInsertionDepthRatioMax;
        }

        [Serializable]
        private sealed class VisualLayoutReport
        {
            public int bottleCount;
            public bool bottleOverlapDetected;
            public bool shadowOverlapDetected;
            public bool hudIntrusionDetected;
            public int corkCount;
            public int completedBottleCount;
            public float silhouetteContrastRatio;
            public float minimumLayerThicknessPixels;
            public float corkAspectRatioMin;
            public float corkAspectRatioMax;
            public float corkInsertionDepthRatioMin;
            public float corkInsertionDepthRatioMax;
            public string generatedAt;
            public string source;
        }

        private readonly struct BottleScreenSample
        {
            public BottleScreenSample(Rect screenRect, bool isEmpty, int capacity, int count)
            {
                ScreenRect = screenRect;
                IsEmpty = isEmpty;
                Capacity = capacity;
                Count = count;
            }

            public Rect ScreenRect { get; }
            public bool IsEmpty { get; }
            public int Capacity { get; }
            public int Count { get; }
        }
    }
}
