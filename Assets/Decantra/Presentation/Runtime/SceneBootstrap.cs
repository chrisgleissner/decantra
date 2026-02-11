/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.Reflection;
using Decantra.Domain.Background;
using Decantra.Domain.Model;
using Decantra.Domain.Rules;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public static class SceneBootstrap
    {
        private static Sprite roundedSprite;
        private static Sprite innerBottleSprite;
        private static Sprite liquidFillSprite;
        private static Sprite liquidSurfaceSprite;
        private static Sprite curvedHighlightSprite;
        private static Sprite softCircleSprite;
        private static Sprite topReflectionSprite;
        private static Sprite reflectionStripSprite;
        private static Sprite placeholderSprite;
        private static Material backgroundCloudMaterial;
        private static Material backgroundStarsMaterial;

        private struct RenderCameras
        {
            public Camera Background;
            public Camera Game;
            public Camera UI;
        }

        private readonly struct LevelPatternCacheKey : System.IEquatable<LevelPatternCacheKey>
        {
            private readonly int _globalSeed;
            private readonly int _levelIndex;

            public LevelPatternCacheKey(int globalSeed, int levelIndex)
            {
                _globalSeed = globalSeed;
                _levelIndex = levelIndex;
            }

            public bool Equals(LevelPatternCacheKey other)
            {
                return _globalSeed == other._globalSeed && _levelIndex == other._levelIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is LevelPatternCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_globalSeed * 397) ^ _levelIndex;
                }
            }
        }

        private struct LevelPatternSprites
        {
            public Sprite Macro;
            public Sprite Meso;
            public Sprite Accent;
            public Sprite Micro;
            public float GenerationMilliseconds;
        }

        private static readonly Dictionary<LevelPatternCacheKey, LevelPatternSprites> _levelPatternsByKey = new Dictionary<LevelPatternCacheKey, LevelPatternSprites>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        public static void EnsureScene()
        {
            var cameras = EnsureRenderCameras();
            var existingController = Object.FindFirstObjectByType<GameController>();
            if (existingController != null && HasRequiredWiring(existingController))
            {
                EnsureBackgroundRendering(existingController, cameras);
                EnsureRestartDialog(existingController);
                EnsureHudSafeLayout();
                WireResetButton(existingController);
                WireOptionsButton(existingController);
                WireShareButton(existingController);
                EnsureLevelJumpOverlay(existingController);
                WireLevelJumpOverlay(existingController);
                return;
            }

            Debug.Log("SceneBootstrap: building runtime UI");

            var backgroundCanvas = CreateCanvas("Canvas_Background", cameras.Background, GetLayerIndex("BackgroundClouds"), false);
            var gameCanvas = CreateCanvas("Canvas_Game", cameras.Game, GetLayerIndex("Game"), false);
            var uiCanvas = CreateCanvas("Canvas_UI", cameras.UI, GetLayerIndex("UI"), true);

            var backgroundLayers = CreateBackground(backgroundCanvas.transform);
            CreateEventSystem();

            var hudView = CreateHud(uiCanvas.transform, out var topHudRect, out var secondaryHudRect, out var brandLockupRect, out var bottomHudRect, out var layoutPadding);
            var gridRoot = CreateGridRoot(gameCanvas.transform, out var bottleAreaRect);

            var hudSafeLayout = uiCanvas.gameObject.AddComponent<Decantra.Presentation.View.HudSafeLayout>();
            hudSafeLayout.Configure(topHudRect, secondaryHudRect, brandLockupRect, bottomHudRect, bottleAreaRect, gridRoot.GetComponent<RectTransform>(), layoutPadding + 12f, layoutPadding);

            var palette = CreatePalette();

            var bottleViews = new List<BottleView>();
            for (int i = 0; i < 9; i++)
            {
                var bottleView = CreateBottle(gridRoot.transform, i + 1, palette);
                bottleView.Initialize(i);
                var bottleInput = bottleView.GetComponent<BottleInput>() ?? bottleView.gameObject.AddComponent<BottleInput>();

                bottleViews.Add(bottleView);

                SetPrivateField(bottleInput, "bottleView", bottleView);
            }

            var controller = existingController;
            if (controller == null)
            {
                var controllerGo = new GameObject("GameController");
                controller = controllerGo.AddComponent<GameController>();
            }
            SetPrivateField(controller, "bottleViews", bottleViews);
            SetPrivateField(controller, "hudView", hudView);
            SetPrivateField(controller, "backgroundImage", backgroundLayers.Base);
            SetPrivateField(controller, "backgroundDetail", backgroundLayers.Detail);
            SetPrivateField(controller, "backgroundFlow", backgroundLayers.Flow);
            SetPrivateField(controller, "backgroundShapes", backgroundLayers.Shapes);
            SetPrivateField(controller, "backgroundVignette", backgroundLayers.Vignette);
            SetPrivateField(controller, "backgroundBubbles", backgroundLayers.Bubbles);
            SetPrivateField(controller, "backgroundLargeStructure", backgroundLayers.LargeStructure);
            SetPrivateField(controller, "backgroundStars", backgroundLayers.StarsRoot);

            var banner = CreateLevelBanner(uiCanvas.transform);
            SetPrivateField(controller, "levelBanner", banner);

            var levelJumpOverlay = CreateLevelJumpOverlay(uiCanvas.transform);
            SetPrivateField(controller, "levelJumpOverlay", levelJumpOverlay.Root);
            SetPrivateField(controller, "levelJumpInput", levelJumpOverlay.Input);
            SetPrivateField(controller, "levelJumpGoButton", levelJumpOverlay.GoButton);
            SetPrivateField(controller, "levelJumpDismissButton", levelJumpOverlay.DismissButton);

            var intro = CreateIntroBanner(uiCanvas.transform);
            SetPrivateField(controller, "introBanner", intro);

            var outOfMoves = CreateOutOfMovesBanner(uiCanvas.transform);
            SetPrivateField(controller, "outOfMovesBanner", outOfMoves);

            var settings = CreateSettingsPanel(uiCanvas.transform, controller);
            WireResetButton(controller);
            var optionsOverlay = CreateOptionsOverlay(uiCanvas.transform, controller);
            SetPrivateField(controller, "_optionsOverlay", optionsOverlay);
            SetPrivateField(controller, "_starfieldMaterial", GetBackgroundStarsMaterial());
            WireOptionsButton(controller);

            var restartDialog = CreateRestartDialog(uiCanvas.transform);
            SetPrivateField(controller, "restartDialog", restartDialog);
            WireShareButton(controller);
            WireLevelJumpOverlay(controller);

            var toolsGo = new GameObject("RuntimeTools");
            toolsGo.AddComponent<RuntimeScreenshot>();

            foreach (var bottleView in bottleViews)
            {
                var input = bottleView.GetComponent<BottleInput>();
                SetPrivateField(input, "controller", controller);
            }
        }

        private static void EnsureHudSafeLayout()
        {
            var topHudRect = GameObject.Find("TopHud")?.GetComponent<RectTransform>();
            var secondaryHudRect = GameObject.Find("SecondaryHud")?.GetComponent<RectTransform>();
            var brandLockupRect = GameObject.Find("BrandLockup")?.GetComponent<RectTransform>();
            var bottomHudRect = GameObject.Find("BottomHud")?.GetComponent<RectTransform>();
            var bottleAreaRect = GameObject.Find("BottleArea")?.GetComponent<RectTransform>();
            var bottleGridRect = GameObject.Find("BottleGrid")?.GetComponent<RectTransform>();
            if (topHudRect == null || secondaryHudRect == null || bottomHudRect == null || bottleAreaRect == null || bottleGridRect == null)
            {
                return;
            }

            var uiCanvas = topHudRect.GetComponentInParent<Canvas>();
            if (uiCanvas == null)
            {
                return;
            }

            var safeLayout = uiCanvas.GetComponent<HudSafeLayout>();
            if (safeLayout == null)
            {
                safeLayout = uiCanvas.gameObject.AddComponent<HudSafeLayout>();
            }

            // Match runtime bootstrap defaults when wiring an existing scene.
            safeLayout.Configure(topHudRect, secondaryHudRect, brandLockupRect, bottomHudRect, bottleAreaRect, bottleGridRect, 36f, 24f);
        }

        private static void EnsureRestartDialog(GameController controller)
        {
            if (controller == null) return;
            var existing = GetPrivateField<RestartGameDialog>(controller, "restartDialog");
            if (existing != null) return;
            var restartGo = GameObject.Find("RestartDialog");
            if (restartGo == null) return;
            var dialog = restartGo.GetComponent<RestartGameDialog>();
            if (dialog == null) return;
            SetPrivateField(controller, "restartDialog", dialog);
        }

        private static void EnsureLevelJumpOverlay(GameController controller)
        {
            if (controller == null) return;
            var existing = GetPrivateField<GameObject>(controller, "levelJumpOverlay");
            if (existing != null) return;

            var overlayGo = GameObject.Find("LevelJumpOverlay");
            if (overlayGo == null)
            {
                var canvas = Object.FindFirstObjectByType<Canvas>();
                if (canvas == null) return;
                var created = CreateLevelJumpOverlay(canvas.transform);
                SetPrivateField(controller, "levelJumpOverlay", created.Root);
                SetPrivateField(controller, "levelJumpInput", created.Input);
                SetPrivateField(controller, "levelJumpGoButton", created.GoButton);
                SetPrivateField(controller, "levelJumpDismissButton", created.DismissButton);
                return;
            }

            var input = overlayGo.GetComponentInChildren<InputField>(true);
            var goButton = overlayGo.transform.Find("Panel/GoButton")?.GetComponent<Button>();
            var dismissButton = overlayGo.transform.Find("DimmerButton")?.GetComponent<Button>();
            SetPrivateField(controller, "levelJumpOverlay", overlayGo);
            SetPrivateField(controller, "levelJumpInput", input);
            SetPrivateField(controller, "levelJumpGoButton", goButton);
            SetPrivateField(controller, "levelJumpDismissButton", dismissButton);
        }

        private static bool HasRequiredWiring(GameController controller)
        {
            if (controller == null) return false;
            var bottleViews = GetPrivateField<List<BottleView>>(controller, "bottleViews");
            var hud = GetPrivateField<HudView>(controller, "hudView");
            var background = GetPrivateField<Image>(controller, "backgroundImage");
            var stars = GetPrivateField<GameObject>(controller, "backgroundStars");
            var banner = GetPrivateField<LevelCompleteBanner>(controller, "levelBanner");
            var outOfMoves = GetPrivateField<OutOfMovesBanner>(controller, "outOfMovesBanner");
            var levelPanelButton = GetPrivateField<Button>(controller, "levelPanelButton");
            var shareButton = GetPrivateField<Button>(controller, "shareButton");
            var shareRoot = GetPrivateField<GameObject>(controller, "shareButtonRoot");
            return bottleViews != null && bottleViews.Count > 0 && hud != null && background != null && stars != null && banner != null && outOfMoves != null && levelPanelButton != null && shareButton != null && shareRoot != null;
        }

        private static Canvas CreateCanvas(string name, Camera camera, int layer, bool pixelPerfect)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.pixelPerfect = pixelPerfect;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            SetLayerRecursively(canvasGo, layer);
            return canvas;
        }

        private static int GetLayerIndex(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            if (layer < 0)
            {
                Debug.LogError($"SceneBootstrap: Missing layer '{name}'. Check ProjectSettings/TagManager.asset.");
                return 0;
            }
            return layer;
        }

        private static RenderCameras EnsureRenderCameras()
        {
            int starsLayer = GetLayerIndex("BackgroundStars");
            int cloudsLayer = GetLayerIndex("BackgroundClouds");
            int gameLayer = GetLayerIndex("Game");
            int uiLayer = GetLayerIndex("UI");

            int backgroundMask = (1 << starsLayer) | (1 << cloudsLayer);
            int gameMask = 1 << gameLayer;
            int uiMask = 1 << uiLayer;

            var background = EnsureCamera("Camera_Background", CameraClearFlags.SolidColor, 0f, backgroundMask, Color.black);
            var game = EnsureCamera("Camera_Game", CameraClearFlags.Depth, 1f, gameMask, Color.black);
            var ui = EnsureCamera("Camera_UI", CameraClearFlags.Depth, 2f, uiMask, Color.black);

            return new RenderCameras
            {
                Background = background,
                Game = game,
                UI = ui
            };
        }

        private static Camera EnsureCamera(string name, CameraClearFlags clearFlags, float depth, int cullingMask, Color background)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
            }

            var camera = go.GetComponent<Camera>();
            if (camera == null)
            {
                camera = go.AddComponent<Camera>();
            }

            camera.clearFlags = clearFlags;
            camera.depth = depth;
            camera.cullingMask = cullingMask;
            camera.orthographic = true;
            camera.backgroundColor = background;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;

            return camera;
        }

        private static void EnsureBackgroundRendering(GameController controller, RenderCameras cameras)
        {
            if (controller == null) return;

            var baseImage = GetPrivateField<Image>(controller, "backgroundImage");
            var detailImage = GetPrivateField<Image>(controller, "backgroundDetail");
            var flowImage = GetPrivateField<Image>(controller, "backgroundFlow");
            var shapesImage = GetPrivateField<Image>(controller, "backgroundShapes");
            var bubblesImage = GetPrivateField<Image>(controller, "backgroundBubbles");
            var largeImage = GetPrivateField<Image>(controller, "backgroundLargeStructure");
            var vignetteImage = GetPrivateField<Image>(controller, "backgroundVignette");
            var starsRoot = GetPrivateField<GameObject>(controller, "backgroundStars");

            int cloudsLayer = GetLayerIndex("BackgroundClouds");
            int starsLayer = GetLayerIndex("BackgroundStars");

            var cloudsMaterial = GetBackgroundCloudMaterial();

            var backgroundCanvas = baseImage != null ? baseImage.GetComponentInParent<Canvas>() : null;
            if (backgroundCanvas == null)
            {
                var backgroundCanvasGo = GameObject.Find("Canvas_Background");
                backgroundCanvas = backgroundCanvasGo != null ? backgroundCanvasGo.GetComponent<Canvas>() : null;
            }

            if (backgroundCanvas != null && HasMissingBackgroundLayers(baseImage, detailImage, flowImage, shapesImage, bubblesImage, largeImage, vignetteImage))
            {
                var rebuilt = RebuildBackgroundLayers(backgroundCanvas.transform);
                baseImage = rebuilt.Base;
                detailImage = rebuilt.Detail;
                flowImage = rebuilt.Flow;
                shapesImage = rebuilt.Shapes;
                bubblesImage = rebuilt.Bubbles;
                largeImage = rebuilt.LargeStructure;
                vignetteImage = rebuilt.Vignette;
                starsRoot = rebuilt.StarsRoot;

                SetPrivateField(controller, "backgroundImage", baseImage);
                SetPrivateField(controller, "backgroundDetail", detailImage);
                SetPrivateField(controller, "backgroundFlow", flowImage);
                SetPrivateField(controller, "backgroundShapes", shapesImage);
                SetPrivateField(controller, "backgroundBubbles", bubblesImage);
                SetPrivateField(controller, "backgroundLargeStructure", largeImage);
                SetPrivateField(controller, "backgroundVignette", vignetteImage);
                SetPrivateField(controller, "backgroundStars", starsRoot);
            }

            if (baseImage != null) baseImage.material = cloudsMaterial;
            if (detailImage != null) detailImage.material = cloudsMaterial;
            if (flowImage != null) flowImage.material = cloudsMaterial;
            if (shapesImage != null) shapesImage.material = cloudsMaterial;
            if (bubblesImage != null) bubblesImage.material = cloudsMaterial;
            if (largeImage != null) largeImage.material = cloudsMaterial;
            if (vignetteImage != null) vignetteImage.material = cloudsMaterial;

            if (backgroundCanvas != null)
            {
                backgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                backgroundCanvas.worldCamera = cameras.Background;
                backgroundCanvas.planeDistance = 10f;
                backgroundCanvas.pixelPerfect = false;
                SetLayerRecursively(backgroundCanvas.gameObject, cloudsLayer);
            }

            if (starsRoot == null && backgroundCanvas != null)
            {
                starsRoot = CreateStarfield(backgroundCanvas.transform, starsLayer, GetBackgroundStarsMaterial());
                SetPrivateField(controller, "backgroundStars", starsRoot);
            }

            if (starsRoot != null)
            {
                SetLayerRecursively(starsRoot, starsLayer);
                var starsImage = starsRoot.GetComponentInChildren<RawImage>(true);
                if (starsImage != null)
                {
                    starsImage.material = GetBackgroundStarsMaterial();
                }
            }
        }

        private static bool HasMissingBackgroundLayers(Image baseImage, Image detailImage, Image flowImage, Image shapesImage, Image bubblesImage, Image largeImage, Image vignetteImage)
        {
            return baseImage == null || detailImage == null || flowImage == null || shapesImage == null || bubblesImage == null || largeImage == null || vignetteImage == null;
        }

        private static BackgroundLayers RebuildBackgroundLayers(Transform parent)
        {
            if (parent == null)
            {
                return default;
            }

            DestroyBackgroundChild(parent, "BackgroundStars");
            DestroyBackgroundChild(parent, "Background");
            DestroyBackgroundChild(parent, "BackgroundLargeStructure");
            DestroyBackgroundChild(parent, "BackgroundFlow");
            DestroyBackgroundChild(parent, "BackgroundShapes");
            DestroyBackgroundChild(parent, "BackgroundBubbles");
            DestroyBackgroundChild(parent, "BackgroundDetail");
            DestroyBackgroundChild(parent, "BackgroundVignette");

            return CreateBackground(parent);
        }

        private static void DestroyBackgroundChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) return;
            Object.Destroy(child.gameObject);
        }

        private static GameObject CreateStarfield(Transform parent, int starsLayer, Material starsMaterial)
        {
            var starsGo = CreateUiChild(parent, "BackgroundStars");
            var starsRect = starsGo.GetComponent<RectTransform>();
            starsRect.anchorMin = Vector2.zero;
            starsRect.anchorMax = Vector2.one;
            starsRect.offsetMin = Vector2.zero;
            starsRect.offsetMax = Vector2.zero;
            var starsImage = starsGo.AddComponent<RawImage>();
            starsImage.material = starsMaterial;
            starsImage.color = Color.white;
            starsImage.raycastTarget = false;
            // Provide a texture for the RawImage - the shader generates stars procedurally
            // but RawImage requires a texture to render at all
            var starsTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            starsTex.SetPixel(0, 0, Color.white);
            starsTex.Apply();
            starsImage.texture = starsTex;
            SetLayerRecursively(starsGo, starsLayer);
            return starsGo;
        }

        private static Material GetBackgroundCloudMaterial()
        {
            if (backgroundCloudMaterial != null) return backgroundCloudMaterial;
            var shader = Shader.Find("Decantra/BackgroundClouds");
            if (shader == null)
            {
                Debug.LogError("SceneBootstrap: Missing Decantra/BackgroundClouds shader.");
                return null;
            }

            backgroundCloudMaterial = new Material(shader)
            {
                renderQueue = 1999
            };
            return backgroundCloudMaterial;
        }

        private static Material GetBackgroundStarsMaterial()
        {
            if (backgroundStarsMaterial != null) return backgroundStarsMaterial;
            var shader = Shader.Find("Decantra/BackgroundStars");
            if (shader == null)
            {
                Debug.LogError("SceneBootstrap: Missing Decantra/BackgroundStars shader.");
                return null;
            }

            backgroundStarsMaterial = new Material(shader)
            {
                renderQueue = 1000
            };
            return backgroundStarsMaterial;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        private struct BackgroundLayers
        {
            public Image Base;
            public Image Detail;
            public Image Flow;
            public Image Shapes;
            public Image Vignette;
            public Image Bubbles;
            public Image LargeStructure;
            public GameObject StarsRoot;
        }

        private static BackgroundLayers CreateBackground(Transform parent)
        {
            int cloudsLayer = GetLayerIndex("BackgroundClouds");
            int starsLayer = GetLayerIndex("BackgroundStars");
            var cloudsMaterial = GetBackgroundCloudMaterial();
            var starsMaterial = GetBackgroundStarsMaterial();

            var starsGo = CreateStarfield(parent, starsLayer, starsMaterial);

            var bg = CreateUiChild(parent, "Background");
            var rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var baseImage = bg.AddComponent<Image>();
            baseImage.sprite = GetPlaceholderSprite();
            baseImage.color = Color.black;
            baseImage.type = Image.Type.Simple;
            baseImage.raycastTarget = false;
            baseImage.material = cloudsMaterial;
            SetLayerRecursively(bg, cloudsLayer);

            var largeStructureGo = CreateUiChild(parent, "BackgroundLargeStructure");
            SetLayerRecursively(largeStructureGo, cloudsLayer);
            var largeRect = largeStructureGo.GetComponent<RectTransform>();
            largeRect.anchorMin = Vector2.zero;
            largeRect.anchorMax = Vector2.one;
            largeRect.offsetMin = Vector2.zero;
            largeRect.offsetMax = Vector2.zero;

            var largeImage = largeStructureGo.AddComponent<Image>();
            largeImage.sprite = GetPlaceholderSprite();
            largeImage.color = new Color(1f, 1f, 1f, 0.35f);
            largeImage.type = Image.Type.Simple;
            largeImage.raycastTarget = false;
            largeImage.material = cloudsMaterial;
            var largeDrift = largeStructureGo.AddComponent<BackgroundDrift>();
            SetPrivateField(largeDrift, "driftAmplitude", new Vector2(45f, 60f));
            SetPrivateField(largeDrift, "driftSpeed", new Vector2(0.012f, 0.009f));
            SetPrivateField(largeDrift, "rotationAmplitude", 2.5f);
            SetPrivateField(largeDrift, "rotationSpeed", 0.015f);

            var flowGo = CreateUiChild(parent, "BackgroundFlow");
            SetLayerRecursively(flowGo, cloudsLayer);
            var flowRect = flowGo.GetComponent<RectTransform>();
            flowRect.anchorMin = Vector2.zero;
            flowRect.anchorMax = Vector2.one;
            flowRect.offsetMin = Vector2.zero;
            flowRect.offsetMax = Vector2.zero;

            var flowImage = flowGo.AddComponent<Image>();
            flowImage.sprite = GetPlaceholderSprite();
            flowImage.color = new Color(1f, 1f, 1f, 0.45f);
            flowImage.type = Image.Type.Simple;
            flowImage.raycastTarget = false;
            flowImage.material = cloudsMaterial;
            var flowDrift = flowGo.AddComponent<BackgroundDrift>();
            SetPrivateField(flowDrift, "driftAmplitude", new Vector2(22f, 34f));
            SetPrivateField(flowDrift, "driftSpeed", new Vector2(0.02f, 0.017f));
            SetPrivateField(flowDrift, "rotationAmplitude", 1.1f);
            SetPrivateField(flowDrift, "rotationSpeed", 0.025f);

            var shapesGo = CreateUiChild(parent, "BackgroundShapes");
            SetLayerRecursively(shapesGo, cloudsLayer);
            var shapesRect = shapesGo.GetComponent<RectTransform>();
            shapesRect.anchorMin = Vector2.zero;
            shapesRect.anchorMax = Vector2.one;
            shapesRect.offsetMin = Vector2.zero;
            shapesRect.offsetMax = Vector2.zero;

            var shapesImage = shapesGo.AddComponent<Image>();
            shapesImage.sprite = GetPlaceholderSprite();
            shapesImage.color = new Color(1f, 1f, 1f, 0.32f);
            shapesImage.type = Image.Type.Simple;
            shapesImage.raycastTarget = false;
            shapesImage.material = cloudsMaterial;
            var shapesDrift = shapesGo.AddComponent<BackgroundDrift>();
            SetPrivateField(shapesDrift, "driftAmplitude", new Vector2(12f, 18f));
            SetPrivateField(shapesDrift, "driftSpeed", new Vector2(0.028f, 0.022f));
            SetPrivateField(shapesDrift, "rotationAmplitude", 0.6f);
            SetPrivateField(shapesDrift, "rotationSpeed", 0.02f);

            var detailGo = CreateUiChild(parent, "BackgroundDetail");
            SetLayerRecursively(detailGo, cloudsLayer);
            var detailRect = detailGo.GetComponent<RectTransform>();
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;

            var detailImage = detailGo.AddComponent<Image>();
            detailImage.sprite = GetPlaceholderSprite();
            detailImage.color = new Color(1f, 1f, 1f, 0.36f);
            detailImage.type = Image.Type.Tiled;
            detailImage.raycastTarget = false;
            detailImage.material = cloudsMaterial;
            var detailDrift = detailGo.AddComponent<BackgroundDrift>();
            SetPrivateField(detailDrift, "driftAmplitude", new Vector2(6f, 8f));
            SetPrivateField(detailDrift, "driftSpeed", new Vector2(0.06f, 0.05f));
            SetPrivateField(detailDrift, "rotationAmplitude", 0.2f);
            SetPrivateField(detailDrift, "rotationSpeed", 0.05f);

            var bubblesGo = CreateUiChild(parent, "BackgroundBubbles");
            SetLayerRecursively(bubblesGo, cloudsLayer);
            var bubblesRect = bubblesGo.GetComponent<RectTransform>();
            bubblesRect.anchorMin = Vector2.zero;
            bubblesRect.anchorMax = Vector2.one;
            bubblesRect.offsetMin = Vector2.zero;
            bubblesRect.offsetMax = Vector2.zero;

            var bubblesImage = bubblesGo.AddComponent<Image>();
            bubblesImage.sprite = GetPlaceholderSprite();
            bubblesImage.color = new Color(1f, 1f, 1f, 0.28f);
            bubblesImage.type = Image.Type.Simple;
            bubblesImage.raycastTarget = false;
            bubblesImage.material = cloudsMaterial;
            var bubblesDrift = bubblesGo.AddComponent<BackgroundDrift>();
            SetPrivateField(bubblesDrift, "driftAmplitude", new Vector2(8f, 12f));
            SetPrivateField(bubblesDrift, "driftSpeed", new Vector2(0.018f, 0.025f));
            SetPrivateField(bubblesDrift, "rotationAmplitude", 0.4f);
            SetPrivateField(bubblesDrift, "rotationSpeed", 0.012f);

            // Vignette effect completely disabled - creates dated egg-shaped spotlight appearance
            var vignetteGo = CreateUiChild(parent, "BackgroundVignette");
            SetLayerRecursively(vignetteGo, cloudsLayer);
            var vignetteRect = vignetteGo.GetComponent<RectTransform>();
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;

            var vignetteImage = vignetteGo.AddComponent<Image>();
            vignetteImage.sprite = CreateVignetteSprite();
            vignetteImage.color = new Color(0f, 0f, 0f, 0f); // Alpha = 0 to completely disable vignette
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.raycastTarget = false;
            vignetteImage.material = cloudsMaterial;
            vignetteGo.SetActive(false); // Disable vignette GameObject entirely

            starsGo.transform.SetSiblingIndex(0);
            bg.transform.SetSiblingIndex(1);
            largeStructureGo.transform.SetSiblingIndex(2);
            flowGo.transform.SetSiblingIndex(3);
            shapesGo.transform.SetSiblingIndex(4);
            bubblesGo.transform.SetSiblingIndex(5);
            detailGo.transform.SetSiblingIndex(6);
            vignetteGo.transform.SetSiblingIndex(7);

            return new BackgroundLayers
            {
                Base = baseImage,
                Detail = detailImage,
                Flow = flowImage,
                Shapes = shapesImage,
                Vignette = vignetteImage,
                Bubbles = bubblesImage,
                LargeStructure = largeImage,
                StarsRoot = starsGo
            };
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static HudView CreateHud(Transform parent, out RectTransform topHudRect, out RectTransform secondaryHudRect, out RectTransform brandLockupRect, out RectTransform bottomHudRect, out float layoutPadding)
        {
            var hudRoot = CreateUiChild(parent, "HUD");
            var hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 0);
            hudRect.anchorMax = new Vector2(1, 1);
            hudRect.pivot = new Vector2(0.5f, 0.5f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            var safeRoot = CreateUiChild(hudRoot.transform, "SafeAreaTop");
            var safeRect = safeRoot.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeRoot.AddComponent<Decantra.Presentation.View.SafeAreaInset>();

            var hudViewGo = CreateUiChild(hudRoot.transform, "HudView");
            var hudView = hudViewGo.GetComponent<HudView>() ?? hudViewGo.AddComponent<HudView>();

            var topShiftRoot = CreateUiChild(safeRoot.transform, "TopShiftRoot");
            var topShiftRect = topShiftRoot.GetComponent<RectTransform>();
            topShiftRect.anchorMin = Vector2.zero;
            topShiftRect.anchorMax = Vector2.one;
            topShiftRect.offsetMin = Vector2.zero;
            topShiftRect.offsetMax = Vector2.zero;

            var topHud = CreateUiChild(topShiftRoot.transform, "TopHud");
            var topRect = topHud.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0, -218);
            topRect.sizeDelta = new Vector2(1000, 150);

            var topLayout = topHud.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;
            topLayout.spacing = 16f;

            var levelText = CreateStatPanel(topHud.transform, "LevelPanel", "LEVEL", out var levelPanel);
            var movesText = CreateStatPanel(topHud.transform, "MovesPanel", "MOVES", out var movesPanel);
            var scoreText = CreateStatPanel(topHud.transform, "ScorePanel", "SCORE", out var scorePanel);

            _ = AddPanelButton(levelPanel);

            var brandGo = CreateUiChild(topShiftRoot.transform, "BrandLockup");
            var brandRect = brandGo.GetComponent<RectTransform>();
            brandRect.anchorMin = new Vector2(0.5f, 1f);
            brandRect.anchorMax = new Vector2(0.5f, 1f);
            brandRect.pivot = new Vector2(0.5f, 1f);
            brandRect.anchoredPosition = new Vector2(0, -18);

            var brandSprite = Resources.Load<Sprite>("Decantra");
            var brandImage = brandGo.AddComponent<Image>();
            brandImage.sprite = brandSprite;
            brandImage.preserveAspect = true;
            brandImage.color = Color.white;
            brandImage.raycastTarget = false;

            var brandLayout = brandGo.AddComponent<TopBannerLogoLayout>();
            SetPrivateField(brandLayout, "logoRect", brandRect);
            SetPrivateField(brandLayout, "logoImage", brandImage);
            SetPrivateField(brandLayout, "buttonRects", new[]
            {
                levelPanel.GetComponent<RectTransform>(),
                movesPanel.GetComponent<RectTransform>(),
                scorePanel.GetComponent<RectTransform>()
            });

            var secondaryHud = CreateUiChild(topShiftRoot.transform, "SecondaryHud");
            var secondaryRect = secondaryHud.GetComponent<RectTransform>();
            secondaryRect.anchorMin = new Vector2(0.5f, 1f);
            secondaryRect.anchorMax = new Vector2(0.5f, 1f);
            secondaryRect.pivot = new Vector2(0.5f, 1f);
            secondaryRect.anchoredPosition = new Vector2(0, -392);
            secondaryRect.sizeDelta = new Vector2(800, 150);

            var secondaryLayout = secondaryHud.AddComponent<HorizontalLayoutGroup>();
            secondaryLayout.childAlignment = TextAnchor.MiddleCenter;
            secondaryLayout.childForceExpandWidth = false;
            secondaryLayout.childForceExpandHeight = false;
            secondaryLayout.spacing = 32f;

            var resetButton = CreateResetButton(secondaryHud.transform);
            var optionsButton = CreateOptionsButton(secondaryHud.transform);
            var resetButtonRect = resetButton != null ? resetButton.GetComponent<RectTransform>() : null;
            if (resetButton != null)
            {
                SetPrivateField(brandLayout, "resetButtonRect", resetButtonRect);
                brandLayout.ForceLayout();
            }

            var bottomHud = CreateUiChild(hudRoot.transform, "BottomHud");
            var bottomRect = bottomHud.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector2(0f, 60f);
            // Width matches top HUD (1000px) for pixel-perfect horizontal alignment
            bottomRect.sizeDelta = new Vector2(1000, 150);

            var bottomLayout = bottomHud.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = false;
            bottomLayout.spacing = 16f;

            var bottomImage = bottomHud.GetComponent<Image>();
            if (bottomImage != null)
            {
                Object.Destroy(bottomImage);
            }

            // Use wider bottom stat panels for "MAX LEVEL" and "HIGH SCORE"
            // Each panel width = 458px to match combined width of top 3 panels (932px total)
            var maxLevelText = CreateBottomStatPanel(bottomHud.transform, "MaxLevelPanel", "MAX LEVEL", out _);
            var highScoreText = CreateBottomStatPanel(bottomHud.transform, "HighScorePanel", "HIGH SCORE", out _);

            SetPrivateField(hudView, "levelText", levelText);
            SetPrivateField(hudView, "movesText", movesText);
            SetPrivateField(hudView, "scoreText", scoreText);
            SetPrivateField(hudView, "highScoreText", highScoreText);
            SetPrivateField(hudView, "maxLevelText", maxLevelText);
            SetPrivateField<Text>(hudView, "titleText", null);

            float ResolveRectHeight(RectTransform rect, float fallback)
            {
                if (rect == null) return fallback;
                float height = rect.rect.height;
                if (height > 0f) return height;
                var layout = rect.GetComponent<LayoutElement>();
                if (layout != null && layout.minHeight > 0f) return layout.minHeight;
                return fallback;
            }

            float movesHeight = ResolveRectHeight(movesPanel.GetComponent<RectTransform>(), 140f);
            float resetHeight = ResolveRectHeight(resetButtonRect, 0f);

            topShiftRect.offsetMin = new Vector2(0f, -movesHeight + resetHeight);
            topShiftRect.offsetMax = new Vector2(0f, -movesHeight + resetHeight);

            layoutPadding = Mathf.Clamp(movesHeight * 0.18f, 18f, 32f);

            if (brandLayout != null)
            {
                brandLayout.ForceLayout();
            }

            topHudRect = topRect;
            secondaryHudRect = secondaryRect;
            brandLockupRect = brandRect;
            bottomHudRect = bottomRect;

            return hudView;
        }

        private static GameObject CreateGridRoot(Transform parent, out RectTransform bottleAreaRect)
        {
            var area = CreateUiChild(parent, "BottleArea");
            var areaRect = area.GetComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0, 0);
            areaRect.anchorMax = new Vector2(1, 1);
            areaRect.pivot = new Vector2(0.5f, 0.5f);
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;
            bottleAreaRect = areaRect;

            var gridRoot = CreateUiChild(area.transform, "BottleGrid");
            var gridRect = gridRoot.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.sizeDelta = new Vector2(820, 1300);

            var grid = gridRoot.GetComponent<GridLayoutGroup>() ?? gridRoot.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(220, 420);
            grid.spacing = new Vector2(20, 30);
            grid.childAlignment = TextAnchor.MiddleCenter;
            return gridRoot;
        }

        private static BottleView CreateBottle(Transform parent, int index, ColorPalette palette)
        {
            var bottleGo = CreateUiChild(parent, $"Bottle_{index}");
            var bottleRect = bottleGo.GetComponent<RectTransform>();
            bottleRect.sizeDelta = new Vector2(220, 420);

            var rounded = GetRoundedSprite();
            var inner = GetBottleInnerSprite();
            var liquidSprite = GetLiquidFillSprite();
            var surfaceSprite = GetLiquidSurfaceSprite();
            var highlightSprite = GetCurvedHighlightSprite();
            var softCircle = GetSoftCircleSprite();
            var topReflection = GetTopReflectionSprite();
            var reflectionStripSprite = GetReflectionStripSprite();

            var hitArea = bottleGo.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, 0);
            hitArea.raycastTarget = true;

            var shadowGo = CreateUiChild(bottleGo.transform, "Shadow");
            var shadow = shadowGo.AddComponent<Image>();
            shadow.sprite = softCircle;
            shadow.type = Image.Type.Simple;
            shadow.color = new Color(0f, 0f, 0f, 0f);
            shadow.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(130, 20);
            shadowRect.anchoredPosition = new Vector2(0, -192);
            shadowGo.SetActive(false);

            var glassBackGo = CreateUiChild(bottleGo.transform, "GlassBack");
            var glassBack = glassBackGo.AddComponent<Image>();
            glassBack.sprite = rounded;
            glassBack.type = Image.Type.Sliced;
            glassBack.color = new Color(0.3f, 0.4f, 0.55f, 0.12f);
            glassBack.raycastTarget = false;
            var glassBackRect = glassBackGo.GetComponent<RectTransform>();
            glassBackRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassBackRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassBackRect.pivot = new Vector2(0.5f, 0.5f);
            glassBackRect.sizeDelta = new Vector2(136, 350);
            glassBackRect.anchoredPosition = new Vector2(0, -8);

            var liquidMaskGo = CreateUiChild(bottleGo.transform, "LiquidMask");
            var liquidMask = liquidMaskGo.AddComponent<Image>();
            liquidMask.sprite = inner;
            liquidMask.type = Image.Type.Sliced;
            liquidMask.color = Color.white;
            liquidMask.raycastTarget = false;
            var mask = liquidMaskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var liquidMaskRect = liquidMaskGo.GetComponent<RectTransform>();
            liquidMaskRect.anchorMin = new Vector2(0.5f, 0.5f);
            liquidMaskRect.anchorMax = new Vector2(0.5f, 0.5f);
            liquidMaskRect.pivot = new Vector2(0.5f, 0.5f);
            liquidMaskRect.sizeDelta = new Vector2(128, 320);
            liquidMaskRect.anchoredPosition = new Vector2(0, -10);

            var liquidRoot = CreateUiChild(liquidMaskGo.transform, "LiquidRoot");
            var liquidRect = liquidRoot.GetComponent<RectTransform>();
            liquidRect.anchorMin = new Vector2(0.5f, 0f);
            liquidRect.anchorMax = new Vector2(0.5f, 0f);
            liquidRect.pivot = new Vector2(0.5f, 0f);
            liquidRect.sizeDelta = new Vector2(112, 300);
            liquidRect.anchoredPosition = new Vector2(0, -2);

            var liquidSurfaceGo = CreateUiChild(liquidMaskGo.transform, "LiquidSurface");
            var liquidSurface = liquidSurfaceGo.AddComponent<Image>();
            liquidSurface.sprite = surfaceSprite;
            liquidSurface.type = Image.Type.Simple;
            // FIXED: Reduced alpha and added cool tint to prevent surface washout
            liquidSurface.color = new Color(0.85f, 0.92f, 1f, 0.15f);
            liquidSurface.raycastTarget = false;
            var liquidSurfaceRect = liquidSurfaceGo.GetComponent<RectTransform>();
            liquidSurfaceRect.anchorMin = new Vector2(0.5f, 0f);
            liquidSurfaceRect.anchorMax = new Vector2(0.5f, 0f);
            liquidSurfaceRect.pivot = new Vector2(0.5f, 0.5f);
            liquidSurfaceRect.sizeDelta = new Vector2(112, 22);
            liquidSurfaceRect.anchoredPosition = new Vector2(0, 220);

            var glassFrontGo = CreateUiChild(bottleGo.transform, "GlassFront");
            var glassFront = glassFrontGo.AddComponent<Image>();
            glassFront.sprite = rounded;
            glassFront.type = Image.Type.Sliced;
            // FIXED: Reduced alpha and used cool tint instead of pure white to avoid bleaching liquid
            glassFront.color = new Color(0.7f, 0.8f, 0.9f, 0.01f);
            glassFront.raycastTarget = false;
            var glassFrontRect = glassFrontGo.GetComponent<RectTransform>();
            glassFrontRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassFrontRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassFrontRect.pivot = new Vector2(0.5f, 0.5f);
            glassFrontRect.sizeDelta = new Vector2(140, 356);
            glassFrontRect.anchoredPosition = new Vector2(0, -8);

            var reflectionStripGo = CreateUiChild(bottleGo.transform, "ReflectionStrip");
            var reflectionStrip = reflectionStripGo.AddComponent<Image>();
            reflectionStrip.sprite = reflectionStripSprite;
            reflectionStrip.type = Image.Type.Simple;
            reflectionStrip.color = new Color(0.96f, 0.98f, 1f, 0.16f);
            reflectionStrip.raycastTarget = false;
            var reflectionRect = reflectionStripGo.GetComponent<RectTransform>();
            reflectionRect.anchorMin = new Vector2(0.5f, 0.5f);
            reflectionRect.anchorMax = new Vector2(0.5f, 0.5f);
            reflectionRect.pivot = new Vector2(0.5f, 0.5f);
            reflectionRect.sizeDelta = new Vector2(24, 372);
            reflectionRect.anchoredPosition = new Vector2(62, -6);

            var topReflectionGo = CreateUiChild(bottleGo.transform, "TopReflection");
            var topReflectionImage = topReflectionGo.AddComponent<Image>();
            topReflectionImage.sprite = topReflection;
            topReflectionImage.type = Image.Type.Simple;
            // FIXED: Tinted cool white and reduced alpha
            topReflectionImage.color = new Color(0.85f, 0.92f, 1f, 0.05f);
            topReflectionImage.raycastTarget = false;
            var topReflectionRect = topReflectionGo.GetComponent<RectTransform>();
            topReflectionRect.anchorMin = new Vector2(0.5f, 0.5f);
            topReflectionRect.anchorMax = new Vector2(0.5f, 0.5f);
            topReflectionRect.pivot = new Vector2(0.5f, 0.5f);
            topReflectionRect.sizeDelta = new Vector2(100, 140);
            topReflectionRect.anchoredPosition = new Vector2(0, 115);

            var baseGo = CreateUiChild(bottleGo.transform, "BasePlate");
            var basePlate = baseGo.AddComponent<Image>();
            basePlate.sprite = rounded;
            basePlate.type = Image.Type.Sliced;
            basePlate.color = new Color(0.6f, 0.7f, 0.82f, 0.3f);
            basePlate.raycastTarget = false;
            var baseRect = baseGo.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(0.5f, 0f);
            baseRect.anchorMax = new Vector2(0.5f, 0f);
            baseRect.pivot = new Vector2(0.5f, 0f);
            baseRect.sizeDelta = new Vector2(120, 40);
            baseRect.anchoredPosition = new Vector2(0, 6);
            baseGo.SetActive(false);

            var rimGo = CreateUiChild(bottleGo.transform, "Rim");
            var rim = rimGo.AddComponent<Image>();
            rim.sprite = rounded;
            rim.type = Image.Type.Sliced;
            rim.color = new Color(0.75f, 0.85f, 0.96f, 0.9f);
            rim.raycastTarget = false;
            var rimRect = rimGo.GetComponent<RectTransform>();
            rimRect.anchorMin = new Vector2(0.5f, 0.5f);
            rimRect.anchorMax = new Vector2(0.5f, 0.5f);
            rimRect.pivot = new Vector2(0.5f, 0.5f);
            rimRect.sizeDelta = new Vector2(96, 18);
            rimRect.anchoredPosition = new Vector2(0, 237);

            var neckGo = CreateUiChild(bottleGo.transform, "BottleNeck");
            var neck = neckGo.AddComponent<Image>();
            neck.sprite = rounded;
            neck.type = Image.Type.Sliced;
            neck.color = new Color(0.55f, 0.68f, 0.85f, 0.6f);
            neck.raycastTarget = false;
            var neckRect = neckGo.GetComponent<RectTransform>();
            neckRect.anchorMin = new Vector2(0.5f, 0.5f);
            neckRect.anchorMax = new Vector2(0.5f, 0.5f);
            neckRect.pivot = new Vector2(0.5f, 0.5f);
            neckRect.sizeDelta = new Vector2(78, 56);
            neckRect.anchoredPosition = new Vector2(0, 211);

            // Inner neck shadow for 3D depth
            var neckInnerGo = CreateUiChild(bottleGo.transform, "NeckInnerShadow");
            var neckInner = neckInnerGo.AddComponent<Image>();
            neckInner.sprite = rounded;
            neckInner.type = Image.Type.Sliced;
            neckInner.color = new Color(0.15f, 0.2f, 0.35f, 0.45f);
            neckInner.raycastTarget = false;
            var neckInnerRect = neckInnerGo.GetComponent<RectTransform>();
            neckInnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            neckInnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            neckInnerRect.pivot = new Vector2(0.5f, 0.5f);
            neckInnerRect.sizeDelta = new Vector2(62, 48);
            neckInnerRect.anchoredPosition = new Vector2(0, 217);

            // Lip highlight at rim edge
            var lipHighlightGo = CreateUiChild(bottleGo.transform, "LipHighlight");
            var lipHighlight = lipHighlightGo.AddComponent<Image>();
            lipHighlight.sprite = rounded;
            lipHighlight.type = Image.Type.Sliced;
            // FIXED: Tinted and reduced alpha
            lipHighlight.color = new Color(0.85f, 0.92f, 1f, 0.15f);
            lipHighlight.raycastTarget = false;
            var lipHighlightRect = lipHighlightGo.GetComponent<RectTransform>();
            lipHighlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            lipHighlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            lipHighlightRect.pivot = new Vector2(0.5f, 0.5f);
            lipHighlightRect.sizeDelta = new Vector2(88, 10);
            lipHighlightRect.anchoredPosition = new Vector2(0, 243);

            var flangeGo = CreateUiChild(bottleGo.transform, "BottleFlange");
            var flange = flangeGo.AddComponent<Image>();
            flange.sprite = rounded;
            flange.type = Image.Type.Sliced;
            flange.fillCenter = false; // Fixed: Only draw edges, not solid overlay
            flange.color = new Color(0.65f, 0.75f, 0.9f, 0.75f);
            flange.raycastTarget = false;
            var flangeRect = flangeGo.GetComponent<RectTransform>();
            flangeRect.anchorMin = new Vector2(0.5f, 0.5f);
            flangeRect.anchorMax = new Vector2(0.5f, 0.5f);
            flangeRect.pivot = new Vector2(0.5f, 0.5f);
            flangeRect.sizeDelta = new Vector2(104, 14);
            flangeRect.anchoredPosition = new Vector2(0, 187);

            var highlightGo = CreateUiChild(bottleGo.transform, "CurvedHighlight");
            var highlight = highlightGo.AddComponent<Image>();
            highlight.sprite = highlightSprite;
            highlight.type = Image.Type.Simple;
            // FIXED: Significant alpha reduction to prevent color washout
            highlight.color = new Color(0.9f, 0.95f, 1f, 0.04f);
            highlight.raycastTarget = false;
            var highlightRect = highlightGo.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0.5f, 0.5f);
            highlightRect.sizeDelta = new Vector2(22, 280);
            highlightRect.anchoredPosition = new Vector2(48, 10);

            var outlineGo = CreateUiChild(bottleGo.transform, "Outline");
            var outline = outlineGo.AddComponent<Image>();
            outline.sprite = rounded;
            outline.type = Image.Type.Sliced;
            outline.fillCenter = false; // Fixed: Only draw edges, not solid overlay covering the liquid
            outline.color = new Color(0.65f, 0.75f, 0.88f, 0.75f);
            outline.raycastTarget = false;
            var outlineRect = outlineGo.GetComponent<RectTransform>();
            outlineRect.anchorMin = new Vector2(0.5f, 0.5f);
            outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            outlineRect.sizeDelta = new Vector2(150, 372);
            outlineRect.anchoredPosition = new Vector2(0, -6);

            var anchorCollarGo = CreateUiChild(bottleGo.transform, "AnchorCollar");
            var anchorCollar = anchorCollarGo.AddComponent<Image>();
            anchorCollar.sprite = rounded;
            anchorCollar.type = Image.Type.Sliced;
            anchorCollar.color = new Color(0.18f, 0.2f, 0.26f, 0.88f);
            anchorCollar.raycastTarget = false;
            var anchorCollarRect = anchorCollarGo.GetComponent<RectTransform>();
            anchorCollarRect.anchorMin = new Vector2(0.5f, 0.5f);
            anchorCollarRect.anchorMax = new Vector2(0.5f, 0.5f);
            anchorCollarRect.pivot = new Vector2(0.5f, 0.5f);
            anchorCollarRect.sizeDelta = new Vector2(128, 56);
            anchorCollarRect.anchoredPosition = new Vector2(0, -152);
            anchorCollarGo.SetActive(false);

            shadowGo.transform.SetAsFirstSibling();

            var stopperGo = CreateUiChild(bottleGo.transform, "Stopper");
            var stopper = stopperGo.AddComponent<Image>();
            stopper.sprite = rounded;
            stopper.type = Image.Type.Sliced;
            stopper.color = new Color(0.35f, 0.25f, 0.18f, 0.95f);
            stopper.raycastTarget = false;
            var stopperRect = stopperGo.GetComponent<RectTransform>();
            stopperRect.anchorMin = new Vector2(0.5f, 0.5f);
            stopperRect.anchorMax = new Vector2(0.5f, 0.5f);
            stopperRect.pivot = new Vector2(0.5f, 0.5f);
            stopperRect.sizeDelta = new Vector2(80, 24);
            stopperRect.anchoredPosition = new Vector2(0, 217);
            stopper.gameObject.SetActive(false);
            rimGo.transform.SetSiblingIndex(stopperGo.transform.GetSiblingIndex());

            var bottleView = bottleGo.GetComponent<BottleView>() ?? bottleGo.AddComponent<BottleView>();
            SetPrivateField(bottleView, "palette", palette);
            SetPrivateField(bottleView, "slotRoot", liquidRect);
            SetPrivateField(bottleView, "outline", outline);
            SetPrivateField(bottleView, "body", glassBack);
            SetPrivateField(bottleView, "basePlate", basePlate);
            SetPrivateField(bottleView, "stopper", stopper);
            SetPrivateField(bottleView, "outlineBaseColor", outline.color);
            SetPrivateField(bottleView, "glassBack", glassBack);
            SetPrivateField(bottleView, "glassFront", glassFront);
            SetPrivateField(bottleView, "reflectionStrip", reflectionStrip);
            SetPrivateField(bottleView, "rim", rim);
            SetPrivateField(bottleView, "baseAccent", basePlate);
            SetPrivateField(bottleView, "curvedHighlight", highlight);
            SetPrivateField(bottleView, "anchorCollar", anchorCollar);
            SetPrivateField(bottleView, "normalShadow", shadow);
            SetPrivateField(bottleView, "liquidSurface", liquidSurface);
            SetPrivateField(bottleView, "liquidSprite", liquidSprite);
            return bottleView;
        }

        private static Text CreateHudText(Transform parent, string name)
        {
            var go = CreateUiChild(parent, name);
            var text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 44;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            text.color = new Color(1f, 0.97f, 0.9f, 1f);
            text.supportRichText = true;
            text.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.7f));
            return text;
        }

        private static Text CreateTitleText(Transform parent, string name, string value)
        {
            var go = CreateUiChild(parent, name);
            var text = go.GetComponent<Text>() ?? go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 80;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.text = value;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.9f));

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 90);
            return text;
        }

        private static GameObject CreateSettingsPanel(Transform parent, GameController controller)
        {
            var panel = CreateUiChild(parent, "SettingsPanel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24, -24);
            rect.sizeDelta = new Vector2(200, 70);

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;

            var label = CreateHudText(panel.transform, "SfxLabel");
            label.fontSize = 24;
            label.text = "SFX";

            var toggleGo = CreateUiChild(panel.transform, "SfxToggle");
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = true;

            var toggleBg = CreateUiChild(toggleGo.transform, "Background");
            var toggleBgImage = toggleBg.AddComponent<Image>();
            toggleBgImage.sprite = GetRoundedSprite();
            toggleBgImage.type = Image.Type.Sliced;
            toggleBgImage.color = new Color(1f, 1f, 1f, 0.18f);
            var bgRect = toggleBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(60, 30);

            var toggleCheck = CreateUiChild(toggleBg.transform, "Check");
            var toggleCheckImage = toggleCheck.AddComponent<Image>();
            toggleCheckImage.color = new Color(1f, 0.98f, 0.92f, 0.95f);
            var checkRect = toggleCheck.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(20, 20);

            toggle.targetGraphic = toggleBgImage;
            toggle.graphic = toggleCheckImage;
            if (controller != null)
            {
                toggle.isOn = controller.IsSfxEnabled;
            }
            toggle.onValueChanged.AddListener(value =>
            {
                if (controller != null)
                {
                    controller.SetSfxEnabled(value);
                }
            });

            return panel;
        }

        private static IntroBanner CreateIntroBanner(Transform parent)
        {
            var root = CreateUiChild(parent, "IntroBanner");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backgroundGo = CreateUiChild(root.transform, "Background");
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = backgroundGo.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0f); // Start transparent; PrepareForIntro() sets alpha=1 when Play() is called
            backgroundImage.raycastTarget = false;
            backgroundGo.transform.SetAsFirstSibling();

            var logoSprite = Resources.Load<Sprite>("DecantraLogo");
            var logoGo = CreateUiChild(root.transform, "Logo");
            var logoImage = logoGo.AddComponent<Image>();
            logoImage.sprite = logoSprite;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
            logoImage.raycastTarget = false;

            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = Vector2.zero;

            var logoGroup = logoGo.AddComponent<CanvasGroup>();
            logoGroup.alpha = 0f;
            logoGroup.blocksRaycasts = false;
            logoGroup.interactable = false;

            var banner = root.AddComponent<IntroBanner>();
            SetPrivateField(banner, "root", rect);
            SetPrivateField(banner, "logoRect", logoRect);
            SetPrivateField(banner, "logoImage", logoImage);
            SetPrivateField(banner, "background", backgroundImage);
            SetPrivateField(banner, "logoGroup", logoGroup);
            root.transform.SetAsLastSibling();
            return banner;
        }

        private static OutOfMovesBanner CreateOutOfMovesBanner(Transform parent)
        {
            var root = CreateUiChild(parent, "OutOfMovesBanner");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720, 200);

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720, 200);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.16f);
            panelImage.raycastTarget = false;

            var text = CreateTitleText(panel.transform, "MessageText", "Out of moves. Try again.");
            text.fontSize = 44;
            text.color = new Color(1f, 0.95f, 0.85f, 1f);
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.9f));

            var banner = root.AddComponent<OutOfMovesBanner>();
            SetPrivateField(banner, "panel", panelRect);
            SetPrivateField(banner, "messageText", text);
            SetPrivateField(banner, "canvasGroup", group);
            return banner;
        }

        private static RestartGameDialog CreateRestartDialog(Transform parent)
        {
            var root = CreateUiChild(parent, "RestartDialog");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var overlay = root.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.45f);
            overlay.raycastTarget = true;

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(800, 450);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.18f);
            panelImage.raycastTarget = false;

            var title = CreateHudText(panel.transform, "TitleText");
            title.fontSize = 48;
            title.text = "Start New Game?";
            title.color = Color.white;
            title.alignment = TextAnchor.UpperCenter;
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -40);
            titleRect.sizeDelta = new Vector2(0, 80);

            var message = CreateHudText(panel.transform, "MessageText");
            message.fontSize = 32;
            message.text = "This will start the game from Level 1 and permanently clear your progress and high score.";
            message.color = new Color(1f, 0.95f, 0.7f, 1f);
            var msgRect = message.GetComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.1f, 0.25f);
            msgRect.anchorMax = new Vector2(0.9f, 0.75f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
            message.horizontalOverflow = HorizontalWrapMode.Wrap;

            var buttonsRoot = CreateUiChild(panel.transform, "Buttons");
            var buttonsRect = buttonsRoot.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.5f, 0f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0f);
            buttonsRect.pivot = new Vector2(0.5f, 0f);
            buttonsRect.anchoredPosition = new Vector2(0f, 30f);
            buttonsRect.sizeDelta = new Vector2(760, 100);

            var buttonsLayout = buttonsRoot.AddComponent<HorizontalLayoutGroup>();
            buttonsLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonsLayout.childForceExpandWidth = false;
            buttonsLayout.childForceExpandHeight = false;
            buttonsLayout.spacing = 24f;

            Button CreateDialogButton(string name, string label, Color color)
            {
                var buttonGo = CreateUiChild(buttonsRoot.transform, name);
                var image = buttonGo.AddComponent<Image>();
                image.sprite = GetRoundedSprite();
                image.type = Image.Type.Sliced;
                image.color = color;
                var rectTransform = buttonGo.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(280, 90);
                var element = buttonGo.AddComponent<LayoutElement>();
                element.minWidth = 220;
                element.minHeight = 90;
                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = image;
                var text = CreateHudText(buttonGo.transform, "Label");
                text.fontSize = 30;
                text.text = label;
                text.color = Color.white;
                return button;
            }

            var cancelButton = CreateDialogButton("CancelButton", "Cancel", new Color(1f, 1f, 1f, 0.25f));
            var restartButton = CreateDialogButton("ConfirmRestartButton", "Start New Game", new Color(1f, 0.3f, 0.3f, 0.85f));

            var dialog = root.AddComponent<RestartGameDialog>();
            SetPrivateField(dialog, "panel", panelRect);
            SetPrivateField(dialog, "messageText", message);
            SetPrivateField(dialog, "cancelButton", cancelButton);
            SetPrivateField(dialog, "restartButton", restartButton);
            SetPrivateField(dialog, "canvasGroup", group);
            dialog.Initialize();
            return dialog;
        }

        private static Text CreateStatPanel(Transform parent, string name, string label, out GameObject panel)
        {
            panel = CreateUiChild(parent, name);

            // Main panel background
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            image.raycastTarget = false;

            // Shadow effect (dark, slightly offset)
            var shadowGo = CreateUiChild(panel.transform, "Shadow");
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = GetRoundedSprite();
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(308, 148);
            shadowRect.anchoredPosition = new Vector2(4f, -4f);
            shadowGo.transform.SetAsFirstSibling();

            // Glass highlight effect (light, top portion)
            var glassGo = CreateUiChild(panel.transform, "GlassHighlight");
            var glassImage = glassGo.AddComponent<Image>();
            glassImage.sprite = GetRoundedSprite();
            glassImage.type = Image.Type.Sliced;
            glassImage.color = new Color(1f, 1f, 1f, 0.08f);
            glassImage.raycastTarget = false;
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassRect.pivot = new Vector2(0.5f, 0.5f);
            glassRect.sizeDelta = new Vector2(292, 64);
            glassRect.anchoredPosition = new Vector2(0f, 32f);

            // Panel size and layout
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 300;
            element.minHeight = 140;

            // Value text
            var text = CreateHudText(panel.transform, "Value");
            text.fontSize = 56;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = 56;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.color = new Color(1f, 0.98f, 0.92f, 1f);

            // Add horizontal padding so text fits within bounds (test expects 64px total padding)
            var textRect = text.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(32, 0);
            textRect.offsetMax = new Vector2(-32, 0);

            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));

            return text;
        }

        /// <summary>
        /// Creates a wider stat panel for the bottom HUD buttons (MAX LEVEL, HIGH SCORE).
        /// Width = 458px = (3 * 300 + 2 * 16) / 2 to match combined top panels width.
        /// Font size matches top panels (56px).
        /// </summary>
        private static Text CreateBottomStatPanel(Transform parent, string name, string label, out GameObject panel)
        {
            panel = CreateUiChild(parent, name);

            // Main panel background
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
            image.raycastTarget = false;

            // Shadow effect (dark, slightly offset)
            var shadowGo = CreateUiChild(panel.transform, "Shadow");
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = GetRoundedSprite();
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(466, 148);  // Wider shadow for bottom panel
            shadowRect.anchoredPosition = new Vector2(4f, -4f);
            shadowGo.transform.SetAsFirstSibling();

            // Glass highlight effect (light, top portion)
            var glassGo = CreateUiChild(panel.transform, "GlassHighlight");
            var glassImage = glassGo.AddComponent<Image>();
            glassImage.sprite = GetRoundedSprite();
            glassImage.type = Image.Type.Sliced;
            glassImage.color = new Color(1f, 1f, 1f, 0.08f);
            glassImage.raycastTarget = false;
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassRect.pivot = new Vector2(0.5f, 0.5f);
            glassRect.sizeDelta = new Vector2(442, 64);  // Wider glass for bottom panel
            glassRect.anchoredPosition = new Vector2(0f, 32f);

            // Panel size and layout - wider to fit "HIGH SCORE" on single line
            // Width = 458px = (3 * 300 + 2 * 16) / 2 = 932 / 2 = 466 each
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(458, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 458;
            element.minHeight = 140;

            // Value text - same font size as top panels (56px)
            var text = CreateHudText(panel.transform, "Value");
            text.fontSize = 56;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 32;  // Higher minimum for readability
            text.resizeTextMaxSize = 56;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.color = new Color(1f, 0.98f, 0.92f, 1f);

            // Add horizontal padding
            var textRect = text.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(32, 0);
            textRect.offsetMax = new Vector2(-32, 0);

            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));

            return text;
        }

        private static Button AddPanelButton(GameObject panel)
        {
            if (panel == null) return null;
            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }
            var button = panel.GetComponent<Button>() ?? panel.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Button CreateShareButton(Transform parent)
        {
            var shareRoot = CreateUiChild(parent, "ShareButtonRoot");
            var shareRootRect = shareRoot.GetComponent<RectTransform>();
            shareRootRect.anchorMin = new Vector2(0.5f, 0f);
            shareRootRect.anchorMax = new Vector2(0.5f, 0f);
            shareRootRect.pivot = new Vector2(0.5f, 1f);
            shareRootRect.anchoredPosition = new Vector2(0f, -18f);
            shareRootRect.sizeDelta = new Vector2(180, 64);

            var shareGo = CreateUiChild(shareRoot.transform, "ShareButton");
            var image = shareGo.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.22f);

            var rect = shareGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = shareGo.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateHudText(shareGo.transform, "Label");
            text.fontSize = 28;
            text.text = "EXPORT";
            text.color = new Color(1f, 0.98f, 0.92f, 1f);
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));

            shareRoot.SetActive(false);
            return button;
        }

        private static Button CreateResetButton(Transform parent)
        {
            var panel = CreateUiChild(parent, "ResetButton");
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

            var shadowGo = CreateUiChild(panel.transform, "Shadow");
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = GetRoundedSprite();
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(308, 148);
            shadowRect.anchoredPosition = new Vector2(4f, -4f);
            shadowGo.transform.SetAsFirstSibling();

            var glassGo = CreateUiChild(panel.transform, "GlassHighlight");
            var glassImage = glassGo.AddComponent<Image>();
            glassImage.sprite = GetRoundedSprite();
            glassImage.type = Image.Type.Sliced;
            glassImage.color = new Color(1f, 1f, 1f, 0.08f);
            glassImage.raycastTarget = false;
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassRect.pivot = new Vector2(0.5f, 0.5f);
            glassRect.sizeDelta = new Vector2(292, 64);
            glassRect.anchoredPosition = new Vector2(0f, 32f);

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 300;
            element.minHeight = 140;

            var button = panel.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateHudText(panel.transform, "Label");
            text.fontSize = 40;  // Increased from 32 for better visibility
            text.text = "RESET";
            text.color = new Color(1f, 0.98f, 0.92f, 1f);
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));
            return button;
        }

        private static Button CreateOptionsButton(Transform parent)
        {
            var panel = CreateUiChild(parent, "OptionsButton");
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

            var shadowGo = CreateUiChild(panel.transform, "Shadow");
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = GetRoundedSprite();
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(308, 148);
            shadowRect.anchoredPosition = new Vector2(4f, -4f);
            shadowGo.transform.SetAsFirstSibling();

            var glassGo = CreateUiChild(panel.transform, "GlassHighlight");
            var glassImage = glassGo.AddComponent<Image>();
            glassImage.sprite = GetRoundedSprite();
            glassImage.type = Image.Type.Sliced;
            glassImage.color = new Color(1f, 1f, 1f, 0.08f);
            glassImage.raycastTarget = false;
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassRect.pivot = new Vector2(0.5f, 0.5f);
            glassRect.sizeDelta = new Vector2(292, 64);
            glassRect.anchoredPosition = new Vector2(0f, 32f);

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 300;
            element.minHeight = 140;

            var button = panel.AddComponent<Button>();
            button.targetGraphic = image;

            var labelText = CreateHudText(panel.transform, "Label");
            labelText.fontSize = 40;
            labelText.text = "OPTIONS";
            labelText.color = new Color(1f, 0.98f, 0.92f, 1f);
            AddTextEffects(labelText, new Color(0f, 0f, 0f, 0.75f));
            return button;
        }

        private static void WireOptionsButton(GameController controller)
        {
            if (controller == null) return;
            var optionsGo = GameObject.Find("OptionsButton");
            if (optionsGo == null) return;
            var button = optionsGo.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(controller.ShowOptionsOverlay);
        }

        private static GameObject CreateOptionsOverlay(Transform parent, GameController controller)
        {
            // Root overlay (full-screen)
            var root = CreateUiChild(parent, "OptionsOverlay");
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Semi-transparent dimmer (tap to dismiss)
            var dimmer = CreateUiChild(root.transform, "Dimmer");
            var dimmerRect = dimmer.GetComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            var dimmerImage = dimmer.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.70f);
            var dimmerButton = dimmer.AddComponent<Button>();
            dimmerButton.transition = Selectable.Transition.None;
            dimmerButton.onClick.AddListener(() => { if (controller != null) controller.HideOptionsOverlay(); });

            // Center panel — dark space-themed card
            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820, 1020);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.06f, 0.07f, 0.11f, 0.95f);

            // Panel shadow
            var panelShadow = CreateUiChild(panel.transform, "Shadow");
            var panelShadowImage = panelShadow.AddComponent<Image>();
            panelShadowImage.sprite = GetRoundedSprite();
            panelShadowImage.type = Image.Type.Sliced;
            panelShadowImage.color = new Color(0f, 0f, 0f, 0.5f);
            panelShadowImage.raycastTarget = false;
            var panelShadowRect = panelShadow.GetComponent<RectTransform>();
            panelShadowRect.anchorMin = Vector2.zero;
            panelShadowRect.anchorMax = Vector2.one;
            panelShadowRect.offsetMin = new Vector2(-6, -6);
            panelShadowRect.offsetMax = new Vector2(6, 6);
            panelShadow.transform.SetAsFirstSibling();

            // Glass highlight at top of panel
            var panelGlass = CreateUiChild(panel.transform, "GlassHighlight");
            var panelGlassImage = panelGlass.AddComponent<Image>();
            panelGlassImage.sprite = GetRoundedSprite();
            panelGlassImage.type = Image.Type.Sliced;
            panelGlassImage.color = new Color(1f, 1f, 1f, 0.06f);
            panelGlassImage.raycastTarget = false;
            var panelGlassRect = panelGlass.GetComponent<RectTransform>();
            panelGlassRect.anchorMin = new Vector2(0f, 1f);
            panelGlassRect.anchorMax = new Vector2(1f, 1f);
            panelGlassRect.pivot = new Vector2(0.5f, 1f);
            panelGlassRect.sizeDelta = new Vector2(-20, 80);
            panelGlassRect.anchoredPosition = new Vector2(0f, -10f);

            // Vertical layout for content
            var content = CreateUiChild(panel.transform, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(48, 48);
            contentRect.offsetMax = new Vector2(-48, -48);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 40f;
            contentLayout.padding = new RectOffset(0, 0, 16, 0);

            // Title
            var title = CreateHudText(content.transform, "Title");
            title.fontSize = 48;
            title.text = "OPTIONS";
            title.color = new Color(1f, 0.98f, 0.92f, 1f);
            title.alignment = TextAnchor.MiddleCenter;
            AddTextEffects(title, new Color(0f, 0f, 0f, 0.6f));
            var titleElement = title.gameObject.AddComponent<LayoutElement>();
            titleElement.preferredHeight = 72;

            // Section header: Starfield
            var sectionHeader = CreateHudText(content.transform, "StarfieldHeader");
            sectionHeader.fontSize = 45;
            sectionHeader.text = "STARFIELD";
            sectionHeader.color = new Color(0.7f, 0.75f, 0.85f, 0.9f);
            sectionHeader.alignment = TextAnchor.MiddleLeft;
            var sectionElement = sectionHeader.gameObject.AddComponent<LayoutElement>();
            sectionElement.preferredHeight = 60;

            // Enable toggle
            var toggleRow = CreateOptionsRow(content.transform, "EnableRow");
            var toggleLabel = CreateHudText(toggleRow.transform, "Label");
            toggleLabel.fontSize = 39;
            toggleLabel.text = "Enabled";
            toggleLabel.color = new Color(1f, 0.98f, 0.92f, 0.9f);
            toggleLabel.alignment = TextAnchor.MiddleLeft;
            var toggleLabelElement = toggleLabel.gameObject.AddComponent<LayoutElement>();
            toggleLabelElement.flexibleWidth = 1;

            var toggleGo = CreateUiChild(toggleRow.transform, "StarfieldToggle");
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = true;
            var toggleElement = toggleGo.AddComponent<LayoutElement>();
            toggleElement.preferredWidth = 80;
            toggleElement.preferredHeight = 40;

            var toggleBg = CreateUiChild(toggleGo.transform, "Background");
            var toggleBgImage = toggleBg.AddComponent<Image>();
            toggleBgImage.sprite = GetRoundedSprite();
            toggleBgImage.type = Image.Type.Sliced;
            toggleBgImage.color = new Color(1f, 1f, 1f, 0.18f);
            var bgRect = toggleBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(70, 36);

            var toggleCheck = CreateUiChild(toggleBg.transform, "Check");
            var toggleCheckImage = toggleCheck.AddComponent<Image>();
            toggleCheckImage.color = new Color(1f, 0.98f, 0.92f, 0.95f);
            var checkRect = toggleCheck.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(24, 24);

            toggle.targetGraphic = toggleBgImage;
            toggle.graphic = toggleCheckImage;

            // Density slider
            var densitySlider = CreateOptionsSlider(content.transform, "DensityRow", "Density",
                StarfieldConfig.DensityMin, StarfieldConfig.DensityMax, StarfieldConfig.DensityDefault);

            // Speed slider
            var speedSlider = CreateOptionsSlider(content.transform, "SpeedRow", "Speed",
                StarfieldConfig.SpeedMin, StarfieldConfig.SpeedMax, StarfieldConfig.SpeedDefault);

            // Brightness slider
            var brightnessSlider = CreateOptionsSlider(content.transform, "BrightnessRow", "Brightness",
                StarfieldConfig.BrightnessMin, StarfieldConfig.BrightnessMax, StarfieldConfig.BrightnessDefault);

            // Close button
            var closeRow = CreateUiChild(content.transform, "CloseRow");
            var closeRowElement = closeRow.AddComponent<LayoutElement>();
            closeRowElement.preferredHeight = 110;
            var closeRowLayout = closeRow.AddComponent<HorizontalLayoutGroup>();
            closeRowLayout.childAlignment = TextAnchor.MiddleCenter;
            closeRowLayout.childForceExpandWidth = false;
            closeRowLayout.childForceExpandHeight = false;

            var closePanel = CreateUiChild(closeRow.transform, "CloseButton");
            var closeImage = closePanel.AddComponent<Image>();
            closeImage.sprite = GetRoundedSprite();
            closeImage.type = Image.Type.Sliced;
            closeImage.color = new Color(0.12f, 0.14f, 0.20f, 0.9f);
            var closePanelRect = closePanel.GetComponent<RectTransform>();
            closePanelRect.sizeDelta = new Vector2(336, 90);
            var closePanelElement = closePanel.AddComponent<LayoutElement>();
            closePanelElement.preferredWidth = 336;
            closePanelElement.preferredHeight = 90;

            var closeButton = closePanel.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.onClick.AddListener(() => { if (controller != null) controller.HideOptionsOverlay(); });

            var closeText = CreateHudText(closePanel.transform, "Label");
            closeText.fontSize = 45;
            closeText.text = "CLOSE";
            closeText.color = new Color(1f, 0.98f, 0.92f, 1f);
            closeText.alignment = TextAnchor.MiddleCenter;
            AddTextEffects(closeText, new Color(0f, 0f, 0f, 0.6f));

            var versionRow = CreateUiChild(content.transform, "VersionRow");
            var versionRowElement = versionRow.AddComponent<LayoutElement>();
            versionRowElement.preferredHeight = 54;
            var versionText = CreateHudText(versionRow.transform, "VersionText");
            versionText.fontSize = 24;
            versionText.alignment = TextAnchor.MiddleCenter;
            versionText.color = new Color(0.7f, 0.75f, 0.85f, 0.9f);
            versionText.text = BuildVersionFooterText();

            // Wire controls to controller
            if (controller != null)
            {
                var config = controller.StarfieldConfiguration ?? StarfieldConfig.Default;
                toggle.isOn = config.Enabled;
                densitySlider.value = config.Density;
                speedSlider.value = config.Speed;
                brightnessSlider.value = config.Brightness;

                toggle.onValueChanged.AddListener(value => controller.SetStarfieldEnabled(value));
                densitySlider.onValueChanged.AddListener(value => controller.SetStarfieldDensity(value));
                speedSlider.onValueChanged.AddListener(value => controller.SetStarfieldSpeed(value));
                brightnessSlider.onValueChanged.AddListener(value => controller.SetStarfieldBrightness(value));
            }

            // Start hidden
            root.SetActive(false);
            return root;
        }

        private static string BuildVersionFooterText()
        {
            string versionName = string.IsNullOrWhiteSpace(Application.version) ? "unknown" : Application.version;
            string versionNumber = GetRuntimeVersionNumber();
            return $"Version {versionName} ({versionNumber})";
        }

        private static string GetRuntimeVersionNumber()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    string packageName = activity.Call<string>("getPackageName");
                    using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, 0))
                    using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        int sdkInt = versionClass.GetStatic<int>("SDK_INT");
                        if (sdkInt >= 28)
                        {
                            long code = packageInfo.Call<long>("getLongVersionCode");
                            return code.ToString();
                        }

                        int legacyCode = packageInfo.Get<int>("versionCode");
                        return legacyCode.ToString();
                    }
                }
            }
            catch
            {
                return "unknown";
            }
#else
            return "editor";
#endif
        }

        private static GameObject CreateOptionsRow(Transform parent, string name)
        {
            var row = CreateUiChild(parent, name);
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 78;
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 16f;
            return row;
        }

        private static Slider CreateOptionsSlider(Transform parent, string rowName, string labelText, float min, float max, float defaultValue)
        {
            var row = CreateOptionsRow(parent, rowName);

            var label = CreateHudText(row.transform, "Label");
            label.fontSize = 39;
            label.text = labelText;
            label.color = new Color(1f, 0.98f, 0.92f, 0.9f);
            label.alignment = TextAnchor.MiddleLeft;
            var labelElement = label.gameObject.AddComponent<LayoutElement>();
            labelElement.preferredWidth = 200;

            var sliderGo = CreateUiChild(row.transform, labelText + "Slider");
            var sliderElement = sliderGo.AddComponent<LayoutElement>();
            sliderElement.flexibleWidth = 1;
            sliderElement.preferredHeight = 64;

            // Slider background track
            var trackGo = CreateUiChild(sliderGo.transform, "Background");
            var trackImage = trackGo.AddComponent<Image>();
            trackImage.sprite = GetRoundedSprite();
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(1f, 1f, 1f, 0.12f);
            var trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.3f);
            trackRect.anchorMax = new Vector2(1f, 0.7f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;

            // Full-height transparent raycast target so taps anywhere on the track row register
            var trackTouchGo = CreateUiChild(sliderGo.transform, "TrackTouch");
            var trackTouchImage = trackTouchGo.AddComponent<Image>();
            trackTouchImage.color = new Color(0f, 0f, 0f, 0f);
            var trackTouchRect = trackTouchGo.GetComponent<RectTransform>();
            trackTouchRect.anchorMin = Vector2.zero;
            trackTouchRect.anchorMax = Vector2.one;
            trackTouchRect.offsetMin = Vector2.zero;
            trackTouchRect.offsetMax = Vector2.zero;
            trackTouchGo.transform.SetAsFirstSibling();

            // Fill area
            var fillArea = CreateUiChild(sliderGo.transform, "Fill Area");
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.3f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.7f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fill = CreateUiChild(fillArea.transform, "Fill");
            var fillImage = fill.AddComponent<Image>();
            fillImage.sprite = GetRoundedSprite();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.5f, 0.6f, 0.9f, 0.65f);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Handle area
            var handleArea = CreateUiChild(sliderGo.transform, "Handle Slide Area");
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = CreateUiChild(handleArea.transform, "Handle");
            var handleImage = handle.AddComponent<Image>();
            handleImage.sprite = GetSoftCircleSprite();
            handleImage.color = new Color(1f, 0.98f, 0.92f, 0.95f);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(48, 48);

            // Invisible expanded touch target for reliable slider interaction
            var touchTarget = CreateUiChild(handle.transform, "TouchTarget");
            var touchImage = touchTarget.AddComponent<Image>();
            touchImage.color = new Color(0f, 0f, 0f, 0f);
            var touchRect = touchTarget.GetComponent<RectTransform>();
            touchRect.anchorMin = new Vector2(0.5f, 0.5f);
            touchRect.anchorMax = new Vector2(0.5f, 0.5f);
            touchRect.sizeDelta = new Vector2(96, 96);

            var slider = sliderGo.AddComponent<Slider>();
            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static Button CreateNewGameButton(Transform parent)
        {
            var panel = CreateUiChild(parent, "NewGameButton");
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);

            var shadowGo = CreateUiChild(panel.transform, "Shadow");
            var shadowImage = shadowGo.AddComponent<Image>();
            shadowImage.sprite = GetRoundedSprite();
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = new Color(0f, 0f, 0f, 0.45f);
            shadowImage.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(308, 148);
            shadowRect.anchoredPosition = new Vector2(4f, -4f);
            shadowGo.transform.SetAsFirstSibling();

            var glassGo = CreateUiChild(panel.transform, "GlassHighlight");
            var glassImage = glassGo.AddComponent<Image>();
            glassImage.sprite = GetRoundedSprite();
            glassImage.type = Image.Type.Sliced;
            glassImage.color = new Color(1f, 1f, 1f, 0.08f);
            glassImage.raycastTarget = false;
            var glassRect = glassGo.GetComponent<RectTransform>();
            glassRect.anchorMin = new Vector2(0.5f, 0.5f);
            glassRect.anchorMax = new Vector2(0.5f, 0.5f);
            glassRect.pivot = new Vector2(0.5f, 0.5f);
            glassRect.sizeDelta = new Vector2(292, 64);
            glassRect.anchoredPosition = new Vector2(0f, 32f);

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 300;
            element.minHeight = 140;

            var button = panel.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateHudText(panel.transform, "Label");
            text.fontSize = 32;
            text.text = "NEW GAME";
            text.color = new Color(1f, 0.98f, 0.92f, 1f);
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));
            return button;
        }

        private static void WireResetButton(GameController controller)
        {
            if (controller == null) return;
            var resetGo = GameObject.Find("ResetButton");
            if (resetGo == null) return;
            var button = resetGo.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(controller.ResetCurrentLevel);

            // Add long-press component for 8 second hold to trigger full game reset
            var longPress = resetGo.GetComponent<LongPressButton>() ?? resetGo.AddComponent<LongPressButton>();
            longPress.Configure(8f, controller.RequestRestartGame);
        }

        private static void WireRestartButton(GameController controller)
        {
            // Legacy method kept for compatibility - now handled by WireResetButton
        }

        private static void WireShareButton(GameController controller)
        {
            if (controller == null) return;
            var topHud = GameObject.Find("TopHud");
            if (topHud == null) return;

            var levelPanel = topHud.transform.Find("LevelPanel");
            if (levelPanel == null) return;
            var levelButton = levelPanel.GetComponent<Button>();
            if (levelButton == null) return;

            var shareRootTransform = topHud.transform.Find("ShareButtonRoot");
            if (shareRootTransform == null)
            {
                // ShareButtonRoot doesn't exist, create it
                _ = CreateShareButton(topHud.transform);
                shareRootTransform = topHud.transform.Find("ShareButtonRoot");
                if (shareRootTransform == null) return;
            }
            var shareRoot = shareRootTransform.gameObject;
            // Ensure ShareButtonRoot starts inactive (may have been left active by previous test)
            shareRoot.SetActive(false);

            var shareTransform = shareRoot.transform.Find("ShareButton");
            var shareGo = shareTransform != null ? shareTransform.gameObject : null;
            if (shareGo == null) return;
            // Ensure ShareButton itself is also inactive
            shareGo.SetActive(false);
            var shareButton = shareGo.GetComponent<Button>();
            if (shareButton == null) return;

            SetPrivateField(controller, "levelPanelButton", levelButton);
            SetPrivateField(controller, "shareButton", shareButton);
            SetPrivateField(controller, "shareButtonRoot", shareRoot);

            levelButton.onClick.RemoveAllListeners();
            levelButton.onClick.AddListener(controller.ToggleShareButton);

            shareButton.onClick.RemoveAllListeners();
            shareButton.onClick.AddListener(controller.ShareCurrentLevel);
        }

        private static void WireLevelJumpOverlay(GameController controller)
        {
            if (controller == null) return;
            EnsureLevelJumpOverlay(controller);

            var levelButton = GetPrivateField<Button>(controller, "levelPanelButton");
            if (levelButton == null)
            {
                var topHud = GameObject.Find("TopHud");
                var levelPanel = topHud != null ? topHud.transform.Find("LevelPanel") : null;
                levelButton = levelPanel != null ? levelPanel.GetComponent<Button>() : null;
                if (levelButton != null)
                {
                    SetPrivateField(controller, "levelPanelButton", levelButton);
                }
            }

            var overlayRoot = GetPrivateField<GameObject>(controller, "levelJumpOverlay");
            var input = GetPrivateField<InputField>(controller, "levelJumpInput");
            var goButton = GetPrivateField<Button>(controller, "levelJumpGoButton");
            var dismissButton = GetPrivateField<Button>(controller, "levelJumpDismissButton");

            if (goButton != null)
            {
                goButton.onClick.RemoveAllListeners();
                goButton.onClick.AddListener(controller.JumpToLevelFromOverlay);
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveAllListeners();
                dismissButton.onClick.AddListener(controller.HideLevelJumpOverlay);
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            if (levelButton != null)
            {
                var longPress = levelButton.GetComponent<LongPressButton>() ?? levelButton.gameObject.AddComponent<LongPressButton>();
                longPress.Configure(6f, controller.ShowLevelJumpOverlay);
            }

            if (input != null)
            {
                input.onEndEdit.RemoveAllListeners();
            }
        }

        private static GameObject CreateUiChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            if (parent != null)
            {
                go.layer = parent.gameObject.layer;
            }
            return go;
        }

        private static LevelCompleteBanner CreateLevelBanner(Transform parent)
        {
            var root = CreateUiChild(parent, "LevelBanner");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var dimmerGo = CreateUiChild(root.transform, "Dimmer");
            var dimmerRect = dimmerGo.GetComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            var dimmerImage = dimmerGo.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0f);
            dimmerImage.raycastTarget = false;

            var blurGo = CreateUiChild(root.transform, "BlurBackdrop");
            var blurRect = blurGo.GetComponent<RectTransform>();
            blurRect.anchorMin = Vector2.zero;
            blurRect.anchorMax = Vector2.one;
            blurRect.offsetMin = Vector2.zero;
            blurRect.offsetMax = Vector2.zero;
            var blurImage = blurGo.AddComponent<RawImage>();
            blurImage.color = new Color(1f, 1f, 1f, 0f);
            blurImage.raycastTarget = false;
            blurGo.transform.SetSiblingIndex(dimmerGo.transform.GetSiblingIndex());
            dimmerGo.transform.SetSiblingIndex(blurGo.transform.GetSiblingIndex() + 1);

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(780, 280);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.18f);
            panelImage.raycastTarget = false;

            var starsText = CreateTitleText(panel.transform, "StarsText", "★★★");
            starsText.fontSize = 104;
            starsText.color = new Color(1f, 0.95f, 0.75f, 1f);

            var scoreText = CreateTitleText(panel.transform, "ScoreText", "+0");
            scoreText.fontSize = 72;
            scoreText.color = new Color(0.5f, 1f, 0.5f, 1f);
            var scoreRect = scoreText.GetComponent<RectTransform>();
            scoreRect.anchoredPosition = new Vector2(0, -96);

            var levelText = CreateTitleText(panel.transform, "LevelText", "LEVEL 1");
            levelText.fontSize = 52;
            levelText.color = new Color(1f, 0.95f, 0.78f, 1f);
            levelText.gameObject.SetActive(false);

            var burstGo = CreateUiChild(panel.transform, "StarBurst");
            var burstRect = burstGo.GetComponent<RectTransform>();
            burstRect.anchorMin = new Vector2(0.5f, 0.5f);
            burstRect.anchorMax = new Vector2(0.5f, 0.5f);
            burstRect.pivot = new Vector2(0.5f, 0.5f);
            burstRect.sizeDelta = new Vector2(420, 420);
            var burstImage = burstGo.AddComponent<Image>();
            burstImage.sprite = CreateRadialBurstSprite();
            burstImage.color = new Color(1f, 0.95f, 0.8f, 0f);
            burstImage.raycastTarget = false;
            burstGo.transform.SetAsFirstSibling();

            var banner = root.AddComponent<LevelCompleteBanner>();
            SetPrivateField(banner, "panel", panelRect);
            SetPrivateField(banner, "starsText", starsText);
            SetPrivateField(banner, "scoreText", scoreText);
            SetPrivateField(banner, "levelText", levelText);
            SetPrivateField(banner, "starBurst", burstImage);
            SetPrivateField(banner, "dimmer", dimmerImage);
            SetPrivateField(banner, "blurBackdrop", blurImage);
            SetPrivateField(banner, "canvasGroup", group);
            return banner;
        }

        private struct LevelJumpOverlayElements
        {
            public GameObject Root;
            public InputField Input;
            public Button GoButton;
            public Button DismissButton;
        }

        private static LevelJumpOverlayElements CreateLevelJumpOverlay(Transform parent)
        {
            var root = CreateUiChild(parent, "LevelJumpOverlay");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var dimmerGo = CreateUiChild(root.transform, "DimmerButton");
            var dimmerRect = dimmerGo.GetComponent<RectTransform>();
            dimmerRect.anchorMin = Vector2.zero;
            dimmerRect.anchorMax = Vector2.one;
            dimmerRect.offsetMin = Vector2.zero;
            dimmerRect.offsetMax = Vector2.zero;
            var dimmerImage = dimmerGo.AddComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.78f);
            dimmerImage.raycastTarget = true;
            var dimmerButton = dimmerGo.AddComponent<Button>();
            dimmerButton.targetGraphic = dimmerImage;
            dimmerButton.transition = Selectable.Transition.None;

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720, 320);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            panelImage.raycastTarget = true;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 18f;
            layout.padding = new RectOffset(36, 36, 30, 30);

            var title = CreateHudText(panel.transform, "Title");
            title.fontSize = 44;
            title.text = "GO TO LEVEL";
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.98f, 0.92f, 1f);
            var titleElement = title.gameObject.AddComponent<LayoutElement>();
            titleElement.minHeight = 64;

            var inputGo = CreateUiChild(panel.transform, "LevelInput");
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(320, 84);
            var inputImage = inputGo.AddComponent<Image>();
            inputImage.sprite = GetRoundedSprite();
            inputImage.type = Image.Type.Sliced;
            inputImage.color = new Color(0.2f, 0.24f, 0.3f, 0.95f);
            var inputField = inputGo.AddComponent<InputField>();
            inputField.targetGraphic = inputImage;
            inputField.contentType = InputField.ContentType.IntegerNumber;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.keyboardType = TouchScreenKeyboardType.NumberPad;
            inputField.characterValidation = InputField.CharacterValidation.Integer;

            var inputTextGo = CreateUiChild(inputGo.transform, "Text");
            var inputText = inputTextGo.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 46;
            inputText.alignment = TextAnchor.MiddleCenter;
            inputText.color = Color.white;
            inputText.supportRichText = false;
            var inputTextRect = inputTextGo.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(16, 8);
            inputTextRect.offsetMax = new Vector2(-16, -8);

            var placeholderGo = CreateUiChild(inputGo.transform, "Placeholder");
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.fontSize = 38;
            placeholder.alignment = TextAnchor.MiddleCenter;
            placeholder.text = "Enter level";
            placeholder.color = new Color(1f, 1f, 1f, 0.55f);
            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(16, 8);
            placeholderRect.offsetMax = new Vector2(-16, -8);

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;

            var inputElement = inputGo.AddComponent<LayoutElement>();
            inputElement.minHeight = 84;
            inputElement.minWidth = 320;

            var goButtonGo = CreateUiChild(panel.transform, "GoButton");
            var goButtonRect = goButtonGo.GetComponent<RectTransform>();
            goButtonRect.sizeDelta = new Vector2(240, 80);
            var goButtonImage = goButtonGo.AddComponent<Image>();
            goButtonImage.sprite = GetRoundedSprite();
            goButtonImage.type = Image.Type.Sliced;
            goButtonImage.color = new Color(0.3f, 0.85f, 0.4f, 0.85f);
            var goButton = goButtonGo.AddComponent<Button>();
            goButton.targetGraphic = goButtonImage;

            var goText = CreateHudText(goButtonGo.transform, "Label");
            goText.fontSize = 34;
            goText.text = "GO TO LEVEL";
            goText.alignment = TextAnchor.MiddleCenter;
            goText.color = Color.white;

            var goElement = goButtonGo.AddComponent<LayoutElement>();
            goElement.minHeight = 80;
            goElement.minWidth = 240;

            root.SetActive(false);

            return new LevelJumpOverlayElements
            {
                Root = root,
                Input = inputField,
                GoButton = goButton,
                DismissButton = dimmerButton
            };
        }

        private static void AddTextEffects(Text text, Color shadowColor)
        {
            if (text == null) return;
            var shadow = text.gameObject.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = new Vector2(1f, -1f);

            var outline = text.gameObject.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static LevelPatternSprites GetLevelPatternSprites(int levelIndex, int globalSeed)
        {
            var key = new LevelPatternCacheKey(globalSeed, levelIndex);
            if (_levelPatternsByKey.TryGetValue(key, out var cached) && cached.Macro != null)
            {
                return cached;
            }

            var levelSeed = BackgroundRules.GetLevelSeed(globalSeed, levelIndex);
            var density = GetDensityForLevel(levelIndex, globalSeed);

            var organicRequest = new OrganicBackgroundGenerator.OrganicPatternRequest
            {
                LevelIndex = levelIndex,
                GlobalSeed = globalSeed,
                LevelSeed = levelSeed,
                Density = density
            };

            var organicGenerated = OrganicBackgroundGenerator.Generate(organicRequest);

            var generated = new LevelPatternSprites
            {
                Macro = organicGenerated.Macro,
                Meso = organicGenerated.Meso,
                Accent = organicGenerated.Accent,
                Micro = organicGenerated.Micro,
                GenerationMilliseconds = organicGenerated.GenerationMilliseconds
            };

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log($"Decantra OrganicBackground level={levelIndex} archetype={organicGenerated.Archetype} ms={organicGenerated.GenerationMilliseconds:0.0}");
#endif

            _levelPatternsByKey[key] = generated;
            return generated;
        }

        private static DensityProfile GetDensityForLevel(int levelIndex, int globalSeed)
        {
            if (levelIndex <= 3)
            {
                return DensityProfile.Sparse;
            }

            ulong levelSeed = BackgroundRules.GetLevelSeed(globalSeed, levelIndex);
            var rng = new DeterministicRng(levelSeed ^ 0x7E3D1F5Bu);
            float roll = rng.NextFloat();

            if (roll < 0.2f) return DensityProfile.Sparse;
            if (roll < 0.75f) return DensityProfile.Medium;
            return DensityProfile.Dense;
        }

        /// <summary>
        /// Updates background structural sprites for a given level.
        /// Call this from GameController when transitioning levels.
        /// NOTE: In version 0.0.2, sprites were not dynamically updated per-level.
        /// This method is now a no-op to preserve the original nebulous, cloudy background.
        /// </summary>
        public static void UpdateBackgroundSpritesForLevel(int levelIndex, int globalSeed, Image flowImage, Image shapesImage, Image bubblesImage, Image largeStructureImage, Image detailImage)
        {
            if (levelIndex <= 0) return;

            var patterns = GetLevelPatternSprites(levelIndex, globalSeed);

            if (largeStructureImage != null)
            {
                largeStructureImage.sprite = patterns.Macro;
            }

            if (flowImage != null)
            {
                flowImage.sprite = patterns.Meso;
            }

            if (shapesImage != null)
            {
                shapesImage.sprite = patterns.Accent;
            }

            if (detailImage != null)
            {
                detailImage.sprite = patterns.Micro;
            }

            if (bubblesImage != null)
            {
                bubblesImage.sprite = patterns.Micro;
            }
        }

        private static Sprite CreateVignetteSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(0.1f, 1f, t);
                    alpha = alpha * alpha;
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSoftCircleSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.46f;
            float feather = size * 0.08f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.InverseLerp(radius, radius + feather, dist);
                    float alpha = 1f - Mathf.Clamp01(t);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = size * 0.48f;
            float inner = size * 0.38f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = dist <= outer && dist >= inner ? 1f : 0f;
                    if (alpha > 0f)
                    {
                        float edge = Mathf.Min(Mathf.Abs(dist - inner), Mathf.Abs(outer - dist));
                        alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / (size * 0.02f)));
                    }
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateRadialBurstSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(0.35f, 0f, t);
                    alpha *= Mathf.SmoothStep(1f, 0.6f, t);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite GetBottleInnerSprite()
        {
            if (innerBottleSprite != null) return innerBottleSprite;
            innerBottleSprite = CreateRoundedRectSprite(64, 16);
            return innerBottleSprite;
        }

        private static Sprite GetLiquidFillSprite()
        {
            if (liquidFillSprite != null) return liquidFillSprite;
            liquidFillSprite = CreateLiquidFillSprite();
            return liquidFillSprite;
        }

        private static Sprite GetLiquidSurfaceSprite()
        {
            if (liquidSurfaceSprite != null) return liquidSurfaceSprite;
            liquidSurfaceSprite = CreateLiquidSurfaceSprite();
            return liquidSurfaceSprite;
        }

        private static Sprite GetCurvedHighlightSprite()
        {
            if (curvedHighlightSprite != null) return curvedHighlightSprite;
            curvedHighlightSprite = CreateCurvedHighlightSprite();
            return curvedHighlightSprite;
        }

        private static Sprite GetSoftCircleSprite()
        {
            if (softCircleSprite != null) return softCircleSprite;
            softCircleSprite = CreateSoftCircleSprite();
            return softCircleSprite;
        }

        private static Sprite GetTopReflectionSprite()
        {
            if (topReflectionSprite != null) return topReflectionSprite;
            topReflectionSprite = CreateTopReflectionSprite();
            return topReflectionSprite;
        }

        private static Sprite GetReflectionStripSprite()
        {
            if (reflectionStripSprite != null) return reflectionStripSprite;
            reflectionStripSprite = CreateReflectionStripSprite();
            return reflectionStripSprite;
        }

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            roundedSprite = CreateRoundedRectSprite(64, 12);
            return roundedSprite;
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite != null) return placeholderSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            placeholderSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return placeholderSprite;
        }

        private static Sprite CreateLiquidFillSprite()
        {
            const int width = 64;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float center = 1f - Mathf.Abs(nx - 0.5f) * 2f;
                    center = Mathf.SmoothStep(0f, 1f, center);
                    // Brightened to lift liquid value while staying saturated
                    float brightness = Mathf.Lerp(0.55f, 0.78f, center);
                    texture.SetPixel(x, y, new Color(brightness, brightness, brightness, 1f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateLiquidSurfaceSprite()
        {
            const int width = 128;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float curve = 0.55f + Mathf.Sin(nx * Mathf.PI) * 0.12f;
                    float dist = Mathf.Abs(ny - curve);
                    float band = Mathf.Clamp01(1f - dist / 0.18f);
                    float taper = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(nx - 0.5f) * 2f);
                    float alpha = band * taper;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateCurvedHighlightSprite()
        {
            const int width = 128;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                float centerX = 0.58f + Mathf.Sin(ny * Mathf.PI) * 0.08f;
                float thickness = Mathf.Lerp(0.24f, 0.08f, ny);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float dist = Mathf.Abs(nx - centerX);
                    float alpha = Mathf.Clamp01(1f - dist / thickness);
                    alpha *= Mathf.SmoothStep(0.85f, 0.15f, ny);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateTopReflectionSprite()
        {
            const int width = 128;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                float alpha = Mathf.SmoothStep(0f, 1f, ny);
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float edge = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(nx - 0.5f) * 2f);
                    float a = alpha * edge;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateReflectionStripSprite()
        {
            const int width = 64;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)(height - 1);
                float vFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.18f, ny))
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.18f, 1f - ny));
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float dist = Mathf.Abs(nx - 0.5f);
                    float band = Mathf.Clamp01(1f - dist / 0.45f);
                    float alpha = Mathf.SmoothStep(0f, 1f, band) * vFade;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateRoundedRectSprite(int size, int radius)
        {
            int clampedRadius = Mathf.Clamp(radius, 1, size / 2);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float r = clampedRadius - 0.5f;
            float rSquared = r * r;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int minX = Mathf.Min(x, size - 1 - x);
                    int minY = Mathf.Min(y, size - 1 - y);
                    float alpha = 1f;
                    if (minX < clampedRadius && minY < clampedRadius)
                    {
                        float dx = r - minX;
                        float dy = r - minY;
                        alpha = (dx * dx + dy * dy <= rSquared) ? 1f : 0f;
                    }
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            var border = new Vector4(clampedRadius, clampedRadius, clampedRadius, clampedRadius);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static ColorPalette CreatePalette()
        {
            var palette = ScriptableObject.CreateInstance<ColorPalette>();
            var entries = new List<ColorPalette.Entry>
            {
                new ColorPalette.Entry { ColorId = ColorId.Red, Color = new Color(0.92f, 0.25f, 0.22f) },
                new ColorPalette.Entry { ColorId = ColorId.Blue, Color = new Color(0.25f, 0.5f, 0.95f) },
                new ColorPalette.Entry { ColorId = ColorId.Green, Color = new Color(0.2f, 0.78f, 0.38f) },
                new ColorPalette.Entry { ColorId = ColorId.Yellow, Color = new Color(0.98f, 0.88f, 0.2f) },
                new ColorPalette.Entry { ColorId = ColorId.Purple, Color = new Color(0.65f, 0.38f, 0.85f) },
                new ColorPalette.Entry { ColorId = ColorId.Orange, Color = new Color(0.97f, 0.55f, 0.2f) },
                new ColorPalette.Entry { ColorId = ColorId.Cyan, Color = new Color(0.2f, 0.85f, 0.9f) },
                new ColorPalette.Entry { ColorId = ColorId.Magenta, Color = new Color(0.92f, 0.36f, 0.78f) }
            };

            SetPrivateField(palette, "entries", entries);
            return palette;
        }

        private static void SetPrivateField<T>(Object target, string fieldName, T value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return;
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(Object target, string fieldName) where T : class
        {
            if (target == null) return null;
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) return null;
            return field.GetValue(target) as T;
        }
    }
}
