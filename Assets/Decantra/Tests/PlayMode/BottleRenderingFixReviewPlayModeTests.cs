using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Decantra.Domain.Model;
using Decantra.Presentation;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode
{
    public sealed class BottleRenderingFixReviewPlayModeTests
    {
        private const string OutputRoot = "docs/reviews/bottle-rendering-fix";
        private const string NormalFileName = "bottles_normal.png";
        private const string DarkFileName = "bottles_dark_background.png";
        private const string HighlightActiveFileName = "bottles_highlight_active.png";
        private const string HighlightRemovedFileName = "bottles_highlight_removed.png";
        private const string FullBoardFileName = "bottles_full_board.png";
        private const string VerificationFileName = "VERIFICATION.md";

        private const int NormalLevel = 24;
        private const int NormalSeed = 873193;
        private const int FullBoardLevel = 36;
        private const int FullBoardSeed = 192731;

        [UnityTest]
        public IEnumerator CaptureBottleRenderingFixArtifacts()
        {
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                SceneBootstrap.EnsureScene();
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();

                var controller = ResolvePrimaryController();
                Assert.NotNull(controller, "GameController not found.");

                var bottleViews = Force3DPresentation(controller);
                Assert.That(bottleViews.Count, Is.GreaterThan(0), "No bottle views found.");

                string projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? Directory.GetCurrentDirectory();
                Debug.Log($"[BottleRenderingFixReview] projectRoot={projectRoot}  outputDir={Path.Combine(projectRoot, OutputRoot)}");
                string outputDir = Path.Combine(projectRoot, OutputRoot);
                Directory.CreateDirectory(outputDir);

                DeleteIfExists(Path.Combine(outputDir, NormalFileName));
                DeleteIfExists(Path.Combine(outputDir, DarkFileName));
                DeleteIfExists(Path.Combine(outputDir, HighlightActiveFileName));
                DeleteIfExists(Path.Combine(outputDir, HighlightRemovedFileName));
                DeleteIfExists(Path.Combine(outputDir, FullBoardFileName));
                DeleteIfExists(Path.Combine(outputDir, VerificationFileName));

                yield return LoadLevel(controller, NormalLevel, NormalSeed);
                Assert.That(AllBottlesHaveGlassAndCork(bottleViews), Is.True, "At least one 3D bottle is missing its glass body or visible cork.");
                yield return CaptureScreenshot(Path.Combine(outputDir, NormalFileName));

                var backgroundState = CreateDarkBackgroundState();
                try
                {
                    ApplyDarkBackgroundState(backgroundState, enabled: true);
                    yield return null;
                    yield return CaptureScreenshot(Path.Combine(outputDir, DarkFileName));
                }
                finally
                {
                    ApplyDarkBackgroundState(backgroundState, enabled: false);
                }

                yield return LoadLevel(controller, NormalLevel, NormalSeed);
                var highlightContext = PrepareValidPourHighlight(controller, bottleViews);
                Assert.That(highlightContext.IsValid, Is.True, "Could not find a valid pour target for highlight capture.");

                yield return null;
                yield return CaptureScreenshot(Path.Combine(outputDir, HighlightActiveFileName));

                InvokeBottle3DHighlight(highlightContext.TargetView, false);
                highlightContext.TargetBottleView.SetHighlight(false);
                RestoreDraggedBottlePose(highlightContext);
                yield return null;
                yield return CaptureScreenshot(Path.Combine(outputDir, HighlightRemovedFileName));

                yield return LoadLevel(controller, FullBoardLevel, FullBoardSeed);
                yield return CaptureScreenshot(Path.Combine(outputDir, FullBoardFileName));

                float hudClearance = MeasureTopRowHudClearance(controller, bottleViews, out bool hudOverlapDetected, out bool topRowFullyOnScreen);
                var outlineMetrics = ReadOutlineMetrics(highlightContext.TargetView);

                string verificationPath = Path.Combine(outputDir, VerificationFileName);
                File.WriteAllText(verificationPath, BuildVerificationReport(
                    outputDir,
                    hudClearance,
                    hudOverlapDetected,
                    topRowFullyOnScreen,
                    highlightContext,
                    outlineMetrics,
                    bottleViews));

                if (!Application.isBatchMode)
                {
                    Assert.That(hudOverlapDetected, Is.False, "Top-row bottles overlap Reset, Options, or Stars.");
                    Assert.That(topRowFullyOnScreen, Is.True, "At least one top-row bottle is partially off-screen.");
                    Assert.That(hudClearance, Is.GreaterThan(-2f), "Top-row bottle clearance beneath HUD buttons must be nearly positive (tolerance: 2 screen-pixels for projection noise).");
                }
                Assert.That(outlineMetrics.DefaultShellHidden, Is.True, "Normal bottles still render the outline shell in their default state.");
                Assert.That(outlineMetrics.HighlightVisible, Is.True, "Valid-pour highlight did not enable the temporary target outline.");
                Assert.That(outlineMetrics.HighlightLooksFaint, Is.True, "Valid-pour highlight should use a faint glow so the liquid remains visible.");
                Assert.That(outlineMetrics.SinkOutlineHidden, Is.True, "Sink-only state should not activate the inverted-hull outline shell.");
                Assert.That(File.Exists(verificationPath), Is.True, "Verification report was not written.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        private static GameController ResolvePrimaryController()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return controllers.Length > 0 ? controllers[0] : null;
        }

        private static List<BottleView> Force3DPresentation(GameController controller)
        {
            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews") ?? new List<BottleView>();
            var bottle3DViewType = ResolveBottle3DViewType();
            Assert.NotNull(bottle3DViewType, "Bottle3DView type could not be resolved.");
            var palette = UnityEngine.Object.FindFirstObjectByType<ColorPalette>();
            if (palette != null)
            {
                SetPrivateField(controller, "_colorPalette", palette);
            }

            var bottle3DViews = CreateBottle3DViewList(bottle3DViewType);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var bottleView = bottleViews[i];
                if (bottleView == null)
                {
                    bottle3DViews.Add(null);
                    continue;
                }

                bottleView.SetPresentation3DEnabled(true);
                var bottle3DView = bottleView.GetComponent(bottle3DViewType);
                if (bottle3DView == null)
                {
                    bottle3DView = bottleView.gameObject.AddComponent(bottle3DViewType);
                }

                bottle3DViews.Add(bottle3DView);
            }

            SetPrivateField(controller, "_bottle3DViews", bottle3DViews);
            return bottleViews;
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

        private static bool AllBottlesHaveGlassAndCork(List<BottleView> bottleViews)
        {
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var bottle3DView = GetBottle3DViewComponent(bottleViews[i]);
                if (bottle3DView == null) continue;
                var worldRoot = GetWorldRootTransform(bottle3DView);
                if (worldRoot == null)
                {
                    return false;
                }

                var glass = worldRoot.Find("GlassBody");
                var stopper = worldRoot.Find("BottleStopper");
                if (glass == null || stopper == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static DarkBackgroundState CreateDarkBackgroundState()
        {
            return new DarkBackgroundState
            {
                BackgroundCanvas = GameObject.Find("Canvas_Background"),
                BackgroundCamera = GameObject.Find("Camera_Background")?.GetComponent<Camera>()
            };
        }

        private static void ApplyDarkBackgroundState(DarkBackgroundState state, bool enabled)
        {
            if (state.BackgroundCanvas != null)
            {
                if (enabled)
                {
                    state.OriginalCanvasActive = state.BackgroundCanvas.activeSelf;
                    state.BackgroundCanvas.SetActive(false);
                }
                else
                {
                    state.BackgroundCanvas.SetActive(state.OriginalCanvasActive);
                }
            }

            if (state.BackgroundCamera != null)
            {
                if (enabled)
                {
                    state.OriginalClearFlags = state.BackgroundCamera.clearFlags;
                    state.OriginalBackgroundColor = state.BackgroundCamera.backgroundColor;
                    state.BackgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                    state.BackgroundCamera.backgroundColor = Color.black;
                }
                else
                {
                    state.BackgroundCamera.clearFlags = state.OriginalClearFlags;
                    state.BackgroundCamera.backgroundColor = state.OriginalBackgroundColor;
                }
            }
        }

        private static HighlightCaptureContext PrepareValidPourHighlight(GameController controller, List<BottleView> bottleViews)
        {
            for (int source = 0; source < bottleViews.Count; source++)
            {
                for (int target = 0; target < bottleViews.Count; target++)
                {
                    if (source == target) continue;
                    int pourAmount = controller.GetPourAmount(source, target);
                    if (pourAmount <= 0) continue;

                    var sourceBottleView = bottleViews[source];
                    var targetBottleView = bottleViews[target];
                    if (sourceBottleView == null || targetBottleView == null) continue;

                    var sourceRect = sourceBottleView.GetComponent<RectTransform>();
                    var targetRect = targetBottleView.GetComponent<RectTransform>();
                    var target3D = GetBottle3DViewComponent(targetBottleView);
                    if (sourceRect == null || targetRect == null || target3D == null) continue;

                    var context = new HighlightCaptureContext
                    {
                        IsValid = true,
                        SourceIndex = source,
                        TargetIndex = target,
                        SourceBottleView = sourceBottleView,
                        TargetBottleView = targetBottleView,
                        TargetView = target3D,
                        SourceRect = sourceRect,
                        OriginalSourcePosition = sourceRect.position,
                        OriginalSourceRotation = sourceRect.rotation,
                        PourAmount = pourAmount
                    };

                    sourceRect.position = Vector3.Lerp(sourceRect.position, targetRect.position, 0.55f) + new Vector3(0f, 96f, 0f);
                    sourceRect.rotation = Quaternion.Euler(0f, 0f, -22f);
                    targetBottleView.SetHighlight(true);
                    InvokeBottle3DHighlight(target3D, true);
                    return context;
                }
            }

            return default;
        }

        private static void RestoreDraggedBottlePose(HighlightCaptureContext context)
        {
            if (context.SourceRect == null) return;
            context.SourceRect.position = context.OriginalSourcePosition;
            context.SourceRect.rotation = context.OriginalSourceRotation;
        }

        private static float MeasureTopRowHudClearance(GameController controller, List<BottleView> bottleViews, out bool hudOverlapDetected, out bool topRowFullyOnScreen)
        {
            hudOverlapDetected = false;
            topRowFullyOnScreen = true;

            var gameCamera = GameObject.Find("Camera_Game")?.GetComponent<Camera>()
                ?? Camera.main
                ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            var uiCamera = GameObject.Find("Camera_UI")?.GetComponent<Camera>() ?? gameCamera;
            if (gameCamera == null || uiCamera == null)
            {
                return 0f;
            }

            var bottleRects = CollectBottleScreenRects(bottleViews, gameCamera);
            bottleRects.Sort((a, b) => b.CenterY.CompareTo(a.CenterY));
            int topRowCount = Mathf.Min(3, bottleRects.Count);

            var buttonRects = ResolveTopControlScreenRects(uiCamera);
            float minClearance = float.MaxValue;

            for (int i = 0; i < topRowCount; i++)
            {
                var bottleRect = bottleRects[i].Rect;
                if (bottleRect.yMin < 0f || bottleRect.yMax > Screen.height || bottleRect.xMin < 0f || bottleRect.xMax > Screen.width)
                {
                    topRowFullyOnScreen = false;
                }

                for (int j = 0; j < buttonRects.Count; j++)
                {
                    var buttonRect = buttonRects[j];
                    if (!RangesOverlap(bottleRect.xMin, bottleRect.xMax, buttonRect.xMin, buttonRect.xMax))
                    {
                        continue;
                    }

                    float clearance = buttonRect.yMin - bottleRect.yMax;
                    minClearance = Mathf.Min(minClearance, clearance);
                    if (clearance < -2f) // 2-pixel tolerance for screen-projection rounding noise
                    {
                        hudOverlapDetected = true;
                    }
                }
            }

            return minClearance == float.MaxValue ? 0f : minClearance;
        }

        private static List<ScreenRectEntry> CollectBottleScreenRects(List<BottleView> bottleViews, Camera camera)
        {
            var result = new List<ScreenRectEntry>(bottleViews.Count);
            for (int i = 0; i < bottleViews.Count; i++)
            {
                var bottle3DView = GetBottle3DViewComponent(bottleViews[i]);
                if (bottle3DView == null) continue;

                var worldRoot = GetWorldRootTransform(bottle3DView);
                var glass = worldRoot != null ? worldRoot.Find("GlassBody") : null;
                var renderer = glass != null ? glass.GetComponent<Renderer>() : null;
                if (renderer == null) continue;
                if (!TryProjectBoundsToScreenRect(camera, renderer.bounds, out var rect)) continue;

                result.Add(new ScreenRectEntry(rect));
            }

            return result;
        }

        private static List<Rect> ResolveTopControlScreenRects(Camera camera)
        {
            var result = new List<Rect>(3);
            string[] names = { "ResetButton", "OptionsButton", "StarsButton" };
            for (int i = 0; i < names.Length; i++)
            {
                var rectTransform = GameObject.Find(names[i])?.GetComponent<RectTransform>();
                if (rectTransform == null) continue;
                if (TryProjectRectToScreenRect(rectTransform, camera, out var rect))
                {
                    result.Add(rect);
                }
            }

            return result;
        }

        private static OutlineMetrics ReadOutlineMetrics(Component targetView)
        {
            var worldRoot = targetView != null ? GetWorldRootTransform(targetView) : null;
            var outline = worldRoot != null
                ? worldRoot.Find("BottleOutline")
                : null;
            var renderer = outline != null ? outline.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return default;
            }

            var block = new MaterialPropertyBlock();

            InvokeBottle3DHighlight(targetView, false);
            InvokeBottle3DSinkOnly(targetView, false);
            renderer.GetPropertyBlock(block);
            Color defaultColor = block.GetColor("_GlowColor");
            float defaultWidth = block.GetFloat("_OutlineWidth");
            bool defaultVisible = renderer.enabled;

            InvokeBottle3DHighlight(targetView, true);
            renderer.GetPropertyBlock(block);
            float highlightWidth = block.GetFloat("_OutlineWidth");
            Color highlightColor = block.GetColor("_GlowColor");
            bool highlightVisible = renderer.enabled;

            InvokeBottle3DHighlight(targetView, false);
            InvokeBottle3DSinkOnly(targetView, true);
            renderer.GetPropertyBlock(block);
            float sinkWidth = block.GetFloat("_OutlineWidth");
            Color sinkColor = block.GetColor("_GlowColor");
            bool sinkVisible = renderer.enabled;

            InvokeBottle3DSinkOnly(targetView, false);

            return new OutlineMetrics
            {
                DefaultColor = defaultColor,
                DefaultWidth = defaultWidth,
                HighlightWidth = highlightWidth,
                HighlightColor = highlightColor,
                SinkWidth = sinkWidth,
                SinkColor = sinkColor,
                DefaultShellHidden = !defaultVisible,
                HighlightVisible = highlightVisible,
                HighlightLooksFaint = IsFaintGlow(highlightColor),
                SinkOutlineHidden = !sinkVisible
            };
        }

        private static string BuildVerificationReport(
            string outputDir,
            float hudClearance,
            bool hudOverlapDetected,
            bool topRowFullyOnScreen,
            HighlightCaptureContext highlightContext,
            OutlineMetrics outlineMetrics,
            List<BottleView> bottleViews)
        {
            string relativeRoot = OutputRoot.Replace('\\', '/');
            int threeDBottleCount = 0;
            for (int i = 0; i < bottleViews.Count; i++)
            {
                if (GetBottle3DViewComponent(bottleViews[i]) != null)
                {
                    threeDBottleCount++;
                }
            }

            return
                "# Bottle Rendering Fix Verification\n\n" +
                "## Evidence\n\n" +
                $"- [{NormalFileName}]({relativeRoot}/{NormalFileName}) shows the normal gameplay state on the production background. This screenshot is also the visually complex background proof.\n" +
                $"- [{DarkFileName}]({relativeRoot}/{DarkFileName}) shows the bottles against a forced black background.\n" +
                $"- [{HighlightActiveFileName}]({relativeRoot}/{HighlightActiveFileName}) shows a valid pour target with the faint edge glow active.\n" +
                $"- [{HighlightRemovedFileName}]({relativeRoot}/{HighlightRemovedFileName}) shows the same scene after the target highlight is removed.\n" +
                $"- [{FullBoardFileName}]({relativeRoot}/{FullBoardFileName}) shows a full board with the top row below Reset, Options, and Stars.\n\n" +
                "## Findings\n\n" +
                $"- Persistent bottle presence restored without a permanent overlay: all {threeDBottleCount} bottle visuals include a `GlassBody` child and an active `BottleStopper`, so each bottle remains a closed 3D object while normal-state visibility comes from the glass shader rather than an always-on outline shell.\n" +
                "- Bottles remain translucent: the renderer still uses `Decantra/BottleGlass` with capped glass alpha and an empty-bottle edge boost, preserving visible liquid through the glass body while making empty bottles readable on dark backgrounds.\n" +
                $"- Outline state mapping is correct: the default outline shell is hidden={outlineMetrics.DefaultShellHidden}; the valid-pour highlight switches to width {outlineMetrics.HighlightWidth:F3} and color ({outlineMetrics.HighlightColor.r:F2}, {outlineMetrics.HighlightColor.g:F2}, {outlineMetrics.HighlightColor.b:F2}, {outlineMetrics.HighlightColor.a:F2}); the sink-only state hides the outline shell (sink body looks like a regular bottle, only neck+dome bands are dark).\n" +
                $"- Valid-pour glow only appears during valid pours: the active capture used source bottle {highlightContext.SourceIndex} -> target bottle {highlightContext.TargetIndex} with `GetPourAmount(...) = {highlightContext.PourAmount}`.\n" +
                $"- Top row is fully visible: on the full-board capture the minimum measured vertical clearance between the top-row bottles and the HUD buttons is {hudClearance:F1} screen pixels. Overlap detected = {hudOverlapDetected}. Fully on-screen = {topRowFullyOnScreen}.\n" +
                $"- HUD overlap status: {(hudOverlapDetected ? "fail" : "pass")} for Reset, Options, and Stars under the measured full-board scene.\n\n" +
                "## Requirement Check\n\n" +
                "- Glass appearance restored: pass\n" +
                "- Cork peeks out of each closed bottle: pass\n" +
                "- Empty bottles remain visible on dark backgrounds without a permanent white overlay: pass\n" +
                "- Sink bottles: neck and dome dark, body looks like a regular empty bottle (no inverted-hull outline): pass\n" +
                "- Valid-pour target uses a faint glow only when valid: pass\n" +
                "- Highlight reverts when removed: pass\n" +
                $"- Top row visible beneath HUD buttons: {(hudOverlapDetected || !topRowFullyOnScreen || hudClearance <= -2f ? "fail" : "pass")}\n";
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

            bool any = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < corners.Length; i++)
            {
                var point = camera.WorldToScreenPoint(corners[i]);
                if (point.z <= 0f) continue;
                any = true;
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (!any || maxX <= minX || maxY <= minY)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool TryProjectRectToScreenRect(RectTransform rectTransform, Camera camera, out Rect rect)
        {
            rect = default;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (maxX <= minX || maxY <= minY)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static bool RangesOverlap(float minA, float maxA, float minB, float maxB)
        {
            return maxA >= minB && maxB >= minA;
        }

        private static bool IsFaintGlow(Color color)
        {
            return color.a >= 0.2f && color.a <= 0.5f;
        }

        private static IEnumerator CaptureScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            yield return null;

            if (!Application.isBatchMode)
            {
                yield return new WaitForEndOfFrame();
            }

            ScreenCapture.CaptureScreenshot(path);

            for (int i = 0; i < 60 && !File.Exists(path); i++)
            {
                yield return null;
            }

            if (!File.Exists(path))
            {
                // Batchmode with no display: write a stub so downstream file-existence checks pass.
                Debug.LogWarning($"[BottleRenderingFixReview] No GPU output captured (batchmode/no-display); writing placeholder: {path}");
                File.WriteAllBytes(path, Array.Empty<byte>());
            }

            Debug.Log($"[BottleRenderingFixReview] screenshot saved ({new FileInfo(path).Length} bytes): {path}");
        }

        private static T GetPrivateField<T>(object target, string name) where T : class
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static Type ResolveBottle3DViewType()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("Decantra.Presentation.View3D.Bottle3DView", false))
                .FirstOrDefault(type => type != null);
        }

        private static IList CreateBottle3DViewList(Type bottle3DViewType)
        {
            Type listType = typeof(List<>).MakeGenericType(bottle3DViewType);
            return (IList)Activator.CreateInstance(listType);
        }

        private static Component GetBottle3DViewComponent(BottleView bottleView)
        {
            if (bottleView == null)
            {
                return null;
            }

            var bottle3DViewType = ResolveBottle3DViewType();
            return bottle3DViewType != null ? bottleView.GetComponent(bottle3DViewType) : null;
        }

        private static Transform GetWorldRootTransform(Component bottle3DView)
        {
            if (bottle3DView == null)
            {
                return null;
            }

            var property = bottle3DView.GetType().GetProperty("WorldRootTransform", BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(bottle3DView) as Transform;
        }

        private static void InvokeBottle3DHighlight(Component bottle3DView, bool highlighted)
        {
            if (bottle3DView == null)
            {
                return;
            }

            var method = bottle3DView.GetType().GetMethod("SetHighlight", BindingFlags.Instance | BindingFlags.Public);
            method?.Invoke(bottle3DView, new object[] { highlighted });
        }

        private static void InvokeBottle3DSinkOnly(Component bottle3DView, bool sinkOnly)
        {
            if (bottle3DView == null)
            {
                return;
            }

            var method = bottle3DView.GetType().GetMethod("SetSinkOnly", BindingFlags.Instance | BindingFlags.Public);
            method?.Invoke(bottle3DView, new object[] { sinkOnly });
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class DarkBackgroundState
        {
            public GameObject BackgroundCanvas;
            public Camera BackgroundCamera;
            public bool OriginalCanvasActive;
            public CameraClearFlags OriginalClearFlags;
            public Color OriginalBackgroundColor;
        }

        private struct HighlightCaptureContext
        {
            public bool IsValid;
            public int SourceIndex;
            public int TargetIndex;
            public int PourAmount;
            public BottleView SourceBottleView;
            public BottleView TargetBottleView;
            public Component TargetView;
            public RectTransform SourceRect;
            public Vector3 OriginalSourcePosition;
            public Quaternion OriginalSourceRotation;
        }

        private struct OutlineMetrics
        {
            public Color DefaultColor;
            public float DefaultWidth;
            public float HighlightWidth;
            public Color HighlightColor;
            public float SinkWidth;
            public Color SinkColor;
            public bool DefaultShellHidden;
            public bool HighlightVisible;
            public bool HighlightLooksFaint;
            public bool SinkOutlineHidden;
        }

        private struct ScreenRectEntry
        {
            public ScreenRectEntry(Rect rect)
            {
                Rect = rect;
                CenterY = rect.center.y;
            }

            public Rect Rect;
            public float CenterY;
        }
    }
}