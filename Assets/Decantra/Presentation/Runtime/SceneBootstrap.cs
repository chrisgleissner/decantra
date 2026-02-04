/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.Reflection;
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

        private readonly struct ZonePatternCacheKey : System.IEquatable<ZonePatternCacheKey>
        {
            private readonly int _globalSeed;
            private readonly int _zoneIndex;

            public ZonePatternCacheKey(int globalSeed, int zoneIndex)
            {
                _globalSeed = globalSeed;
                _zoneIndex = zoneIndex;
            }

            public bool Equals(ZonePatternCacheKey other)
            {
                return _globalSeed == other._globalSeed && _zoneIndex == other._zoneIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ZonePatternCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_globalSeed * 397) ^ _zoneIndex;
                }
            }
        }

        private static readonly Dictionary<ZonePatternCacheKey, BackgroundPatternGenerator.PatternSprites> _zonePatternsByKey = new Dictionary<ZonePatternCacheKey, BackgroundPatternGenerator.PatternSprites>();
        private static int _lastZoneIndex = -1;
        private static int _lastZoneSeed = int.MinValue;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        public static void EnsureScene()
        {
            var existingController = Object.FindFirstObjectByType<GameController>();
            if (existingController != null && HasRequiredWiring(existingController))
            {
                EnsureRestartDialog(existingController);
                WireResetButton(existingController);
                WireShareButton(existingController);
                EnsureLevelJumpOverlay(existingController);
                WireLevelJumpOverlay(existingController);
                return;
            }

            Debug.Log("SceneBootstrap: building runtime UI");

            var canvas = CreateCanvas();
            var backgroundLayers = CreateBackground(canvas.transform);
            CreateEventSystem();

            var hudView = CreateHud(canvas.transform);
            var gridRoot = CreateGridRoot(canvas.transform);

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

            var banner = CreateLevelBanner(canvas.transform);
            SetPrivateField(controller, "levelBanner", banner);

            var levelJumpOverlay = CreateLevelJumpOverlay(canvas.transform);
            SetPrivateField(controller, "levelJumpOverlay", levelJumpOverlay.Root);
            SetPrivateField(controller, "levelJumpInput", levelJumpOverlay.Input);
            SetPrivateField(controller, "levelJumpGoButton", levelJumpOverlay.GoButton);
            SetPrivateField(controller, "levelJumpDismissButton", levelJumpOverlay.DismissButton);

            var intro = CreateIntroBanner(canvas.transform);
            SetPrivateField(controller, "introBanner", intro);

            var outOfMoves = CreateOutOfMovesBanner(canvas.transform);
            SetPrivateField(controller, "outOfMovesBanner", outOfMoves);

            var settings = CreateSettingsPanel(canvas.transform, controller);
            WireResetButton(controller);

            var restartDialog = CreateRestartDialog(canvas.transform);
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
            var banner = GetPrivateField<LevelCompleteBanner>(controller, "levelBanner");
            var outOfMoves = GetPrivateField<OutOfMovesBanner>(controller, "outOfMovesBanner");
            var levelPanelButton = GetPrivateField<Button>(controller, "levelPanelButton");
            var shareButton = GetPrivateField<Button>(controller, "shareButton");
            var shareRoot = GetPrivateField<GameObject>(controller, "shareButtonRoot");
            return bottleViews != null && bottleViews.Count > 0 && hud != null && background != null && banner != null && outOfMoves != null && levelPanelButton != null && shareButton != null && shareRoot != null;
        }

        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            return canvas;
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
        }

        private static BackgroundLayers CreateBackground(Transform parent)
        {
            var bg = CreateUiChild(parent, "Background");
            var rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var baseImage = bg.AddComponent<Image>();
            baseImage.sprite = CreateSunsetSprite();
            baseImage.color = Color.white;
            baseImage.type = Image.Type.Simple;
            baseImage.raycastTarget = false;

            var largeStructureGo = CreateUiChild(parent, "BackgroundLargeStructure");
            var largeRect = largeStructureGo.GetComponent<RectTransform>();
            largeRect.anchorMin = Vector2.zero;
            largeRect.anchorMax = Vector2.one;
            largeRect.offsetMin = Vector2.zero;
            largeRect.offsetMax = Vector2.zero;

            var largeImage = largeStructureGo.AddComponent<Image>();
            largeImage.sprite = CreateLargeStructureSprite();
            largeImage.color = new Color(1f, 1f, 1f, 0.35f);
            largeImage.type = Image.Type.Simple;
            largeImage.raycastTarget = false;
            var largeDrift = largeStructureGo.AddComponent<BackgroundDrift>();
            SetPrivateField(largeDrift, "driftAmplitude", new Vector2(45f, 60f));
            SetPrivateField(largeDrift, "driftSpeed", new Vector2(0.012f, 0.009f));
            SetPrivateField(largeDrift, "rotationAmplitude", 2.5f);
            SetPrivateField(largeDrift, "rotationSpeed", 0.015f);

            var flowGo = CreateUiChild(parent, "BackgroundFlow");
            var flowRect = flowGo.GetComponent<RectTransform>();
            flowRect.anchorMin = Vector2.zero;
            flowRect.anchorMax = Vector2.one;
            flowRect.offsetMin = Vector2.zero;
            flowRect.offsetMax = Vector2.zero;

            var flowImage = flowGo.AddComponent<Image>();
            flowImage.sprite = CreateFlowSprite();
            flowImage.color = new Color(1f, 1f, 1f, 0.45f);
            flowImage.type = Image.Type.Simple;
            flowImage.raycastTarget = false;
            var flowDrift = flowGo.AddComponent<BackgroundDrift>();
            SetPrivateField(flowDrift, "driftAmplitude", new Vector2(22f, 34f));
            SetPrivateField(flowDrift, "driftSpeed", new Vector2(0.02f, 0.017f));
            SetPrivateField(flowDrift, "rotationAmplitude", 1.1f);
            SetPrivateField(flowDrift, "rotationSpeed", 0.025f);

            var shapesGo = CreateUiChild(parent, "BackgroundShapes");
            var shapesRect = shapesGo.GetComponent<RectTransform>();
            shapesRect.anchorMin = Vector2.zero;
            shapesRect.anchorMax = Vector2.one;
            shapesRect.offsetMin = Vector2.zero;
            shapesRect.offsetMax = Vector2.zero;

            var shapesImage = shapesGo.AddComponent<Image>();
            shapesImage.sprite = CreateOrganicShapesSprite();
            shapesImage.color = new Color(1f, 1f, 1f, 0.32f);
            shapesImage.type = Image.Type.Simple;
            shapesImage.raycastTarget = false;
            var shapesDrift = shapesGo.AddComponent<BackgroundDrift>();
            SetPrivateField(shapesDrift, "driftAmplitude", new Vector2(12f, 18f));
            SetPrivateField(shapesDrift, "driftSpeed", new Vector2(0.028f, 0.022f));
            SetPrivateField(shapesDrift, "rotationAmplitude", 0.6f);
            SetPrivateField(shapesDrift, "rotationSpeed", 0.02f);

            var detailGo = CreateUiChild(parent, "BackgroundDetail");
            var detailRect = detailGo.GetComponent<RectTransform>();
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;

            var detailImage = detailGo.AddComponent<Image>();
            detailImage.sprite = CreateSoftNoiseSprite();
            detailImage.color = new Color(1f, 1f, 1f, 0.36f);
            detailImage.type = Image.Type.Tiled;
            detailImage.raycastTarget = false;
            var detailDrift = detailGo.AddComponent<BackgroundDrift>();
            SetPrivateField(detailDrift, "driftAmplitude", new Vector2(6f, 8f));
            SetPrivateField(detailDrift, "driftSpeed", new Vector2(0.06f, 0.05f));
            SetPrivateField(detailDrift, "rotationAmplitude", 0.2f);
            SetPrivateField(detailDrift, "rotationSpeed", 0.05f);

            var bubblesGo = CreateUiChild(parent, "BackgroundBubbles");
            var bubblesRect = bubblesGo.GetComponent<RectTransform>();
            bubblesRect.anchorMin = Vector2.zero;
            bubblesRect.anchorMax = Vector2.one;
            bubblesRect.offsetMin = Vector2.zero;
            bubblesRect.offsetMax = Vector2.zero;

            var bubblesImage = bubblesGo.AddComponent<Image>();
            bubblesImage.sprite = CreateBubblesSprite();
            bubblesImage.color = new Color(1f, 1f, 1f, 0.28f);
            bubblesImage.type = Image.Type.Simple;
            bubblesImage.raycastTarget = false;
            var bubblesDrift = bubblesGo.AddComponent<BackgroundDrift>();
            SetPrivateField(bubblesDrift, "driftAmplitude", new Vector2(8f, 12f));
            SetPrivateField(bubblesDrift, "driftSpeed", new Vector2(0.018f, 0.025f));
            SetPrivateField(bubblesDrift, "rotationAmplitude", 0.4f);
            SetPrivateField(bubblesDrift, "rotationSpeed", 0.012f);

            // Vignette effect completely disabled - creates dated egg-shaped spotlight appearance
            var vignetteGo = CreateUiChild(parent, "BackgroundVignette");
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
            vignetteGo.SetActive(false); // Disable vignette GameObject entirely

            bg.transform.SetAsFirstSibling();
            largeStructureGo.transform.SetSiblingIndex(1);
            flowGo.transform.SetSiblingIndex(2);
            shapesGo.transform.SetSiblingIndex(3);
            bubblesGo.transform.SetSiblingIndex(4);
            detailGo.transform.SetSiblingIndex(5);
            vignetteGo.transform.SetSiblingIndex(6);

            return new BackgroundLayers
            {
                Base = baseImage,
                Detail = detailImage,
                Flow = flowImage,
                Shapes = shapesImage,
                Vignette = vignetteImage,
                Bubbles = bubblesImage,
                LargeStructure = largeImage
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

        private static HudView CreateHud(Transform parent)
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

            var topHud = CreateUiChild(safeRoot.transform, "TopHud");
            var topRect = topHud.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0, -232);
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

            var brandGo = CreateUiChild(safeRoot.transform, "BrandLockup");
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

            var secondaryHud = CreateUiChild(safeRoot.transform, "SecondaryHud");
            var secondaryRect = secondaryHud.GetComponent<RectTransform>();
            secondaryRect.anchorMin = new Vector2(0.5f, 1f);
            secondaryRect.anchorMax = new Vector2(0.5f, 1f);
            secondaryRect.pivot = new Vector2(0.5f, 1f);
            secondaryRect.anchoredPosition = new Vector2(0, -406);
            secondaryRect.sizeDelta = new Vector2(800, 150);

            var secondaryLayout = secondaryHud.AddComponent<HorizontalLayoutGroup>();
            secondaryLayout.childAlignment = TextAnchor.MiddleCenter;
            secondaryLayout.childForceExpandWidth = false;
            secondaryLayout.childForceExpandHeight = false;
            secondaryLayout.spacing = 32f;

            var resetButton = CreateResetButton(secondaryHud.transform);
            if (resetButton != null)
            {
                SetPrivateField(brandLayout, "resetButtonRect", resetButton.GetComponent<RectTransform>());
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

            return hudView;
        }

        private static GameObject CreateGridRoot(Transform parent)
        {
            var area = CreateUiChild(parent, "BottleArea");
            var areaRect = area.GetComponent<RectTransform>();
            areaRect.anchorMin = new Vector2(0, 0);
            areaRect.anchorMax = new Vector2(1, 1);
            areaRect.pivot = new Vector2(0.5f, 0.5f);
            areaRect.offsetMin = new Vector2(0, 90);
            areaRect.offsetMax = new Vector2(0, -500);

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
            shadowRect.anchorMin = new Vector2(0.5f, 0f);
            shadowRect.anchorMax = new Vector2(0.5f, 0f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(140, 30);
            shadowRect.anchoredPosition = new Vector2(0, -180);
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
            reflectionRect.anchorMin = new Vector2(0.74f, 0.12f);
            reflectionRect.anchorMax = new Vector2(0.86f, 0.84f);
            reflectionRect.offsetMin = Vector2.zero;
            reflectionRect.offsetMax = Vector2.zero;

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
            rimRect.anchoredPosition = new Vector2(0, 188);

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
            neckRect.anchoredPosition = new Vector2(0, 162);

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
            neckInnerRect.anchoredPosition = new Vector2(0, 168);

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
            lipHighlightRect.anchoredPosition = new Vector2(0, 194);

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
            flangeRect.anchoredPosition = new Vector2(0, 138);

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
            stopperRect.anchoredPosition = new Vector2(0, 168);
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
            backgroundImage.color = new Color(0f, 0f, 0f, 1f);
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

        private static Sprite CreateSunsetSprite()
        {
            const int width = 128;
            const int height = 512;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            // Modern 2026 palette: luminous aqua-cyan to soft lavender-pink, warm and uplifting
            var top = new Color(0.42f, 0.82f, 0.92f, 1f);       // Bright aqua-cyan
            var midHigh = new Color(0.58f, 0.72f, 0.95f, 1f);   // Soft periwinkle-blue
            var midLow = new Color(0.72f, 0.62f, 0.88f, 1f);    // Gentle lavender
            var bottom = new Color(0.88f, 0.65f, 0.78f, 1f);    // Warm rose-pink

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                // Quintic smoothstep for ultra-smooth gradient
                float curve = t * t * t * (t * (t * 6f - 15f) + 10f);

                // Four-stop gradient with organic wavering
                Color baseColor;
                if (curve < 0.33f)
                {
                    baseColor = Color.Lerp(bottom, midLow, curve / 0.33f);
                }
                else if (curve < 0.55f)
                {
                    baseColor = Color.Lerp(midLow, midHigh, (curve - 0.33f) / 0.22f);
                }
                else
                {
                    baseColor = Color.Lerp(midHigh, top, (curve - 0.55f) / 0.45f);
                }

                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);

                    // Organic color wavering using multi-octave noise
                    float warp1 = Mathf.PerlinNoise(nx * 0.8f + t * 1.5f, t * 2.5f + 3.7f);
                    float warp2 = Mathf.PerlinNoise(nx * 1.6f + 7.2f, t * 3.2f + warp1 * 2f);
                    float colorWave = (warp1 * 0.7f + warp2 * 0.3f - 0.5f) * 0.08f;

                    // Subtle hue shift for living, breathing feel
                    float hueShift = Mathf.PerlinNoise(nx * 0.5f + t * 0.8f, 5.5f) * 0.03f - 0.015f;

                    Color finalColor = new Color(
                        Mathf.Clamp01(baseColor.r + colorWave + hueShift * 0.5f),
                        Mathf.Clamp01(baseColor.g + colorWave * 0.7f),
                        Mathf.Clamp01(baseColor.b + colorWave * 0.4f - hueShift),
                        1f);
                    texture.SetPixel(x, y, finalColor);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 96f);
        }

        private static Sprite CreateSoftNoiseSprite()
        {
            const int width = 192;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            // Ethereal mist with heavy turbulent flow - very soft and dreamy
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Three-layer cascading domain warp for turbulent organic flow
                    float w1x = Mathf.PerlinNoise(nx * 0.9f + 2.1f, ny * 0.9f + 8.4f);
                    float w1y = Mathf.PerlinNoise(nx * 0.9f + 5.7f, ny * 0.9f + 1.2f);
                    float wx = nx + (w1x - 0.5f) * 0.6f;
                    float wy = ny + (w1y - 0.5f) * 0.6f;

                    float w2x = Mathf.PerlinNoise(wx * 1.4f + 9.3f, wy * 1.4f + 3.6f);
                    float w2y = Mathf.PerlinNoise(wx * 1.4f + 4.8f, wy * 1.4f + 7.1f);
                    wx += (w2x - 0.5f) * 0.35f;
                    wy += (w2y - 0.5f) * 0.35f;

                    float w3x = Mathf.PerlinNoise(wx * 2.2f + 1.5f, wy * 2.2f + 6.9f);
                    float w3y = Mathf.PerlinNoise(wx * 2.2f + 8.2f, wy * 2.2f + 2.4f);
                    wx += (w3x - 0.5f) * 0.18f;
                    wy += (w3y - 0.5f) * 0.18f;

                    // Ultra-low frequency cloud formations
                    float n1 = Mathf.PerlinNoise(wx * 0.7f + 3.3f, wy * 0.7f + 5.8f);
                    float n2 = Mathf.PerlinNoise(wx * 1.3f + 7.6f, wy * 1.3f + 2.1f);

                    float cloud = n1 * 0.65f + n2 * 0.35f;

                    // Quintic smoothstep for butter-smooth transitions
                    cloud = cloud * cloud * cloud * (cloud * (cloud * 6f - 15f) + 10f);
                    float alpha = Mathf.Lerp(0.02f, 0.28f, cloud);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 128f);
        }

        private static Sprite CreateFlowSprite()
        {
            const int width = 320;
            const int height = 320;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            // Liquid ink diffusion - organic flowing wisps like watercolor bleeding
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Extreme cascading domain warp for liquid ink feel
                    float t1x = Mathf.PerlinNoise(nx * 0.6f + 1.8f, ny * 0.6f + 4.3f);
                    float t1y = Mathf.PerlinNoise(nx * 0.6f + 7.9f, ny * 0.6f + 2.1f);
                    float wx = nx + (t1x - 0.5f) * 0.8f;
                    float wy = ny + (t1y - 0.5f) * 0.8f;

                    // Second turbulence layer
                    float t2x = Mathf.PerlinNoise(wx * 1.1f + 5.4f, wy * 1.1f + 8.7f);
                    float t2y = Mathf.PerlinNoise(wx * 1.1f + 9.2f, wy * 1.1f + 3.5f);
                    wx += (t2x - 0.5f) * 0.45f;
                    wy += (t2y - 0.5f) * 0.45f;

                    // Third layer for micro-turbulence (ink feathering)
                    float t3x = Mathf.PerlinNoise(wx * 1.8f + 2.6f, wy * 1.8f + 6.1f);
                    float t3y = Mathf.PerlinNoise(wx * 1.8f + 8.3f, wy * 1.8f + 1.9f);
                    wx += (t3x - 0.5f) * 0.22f;
                    wy += (t3y - 0.5f) * 0.22f;

                    // Fourth layer - finest detail
                    float t4x = Mathf.PerlinNoise(wx * 2.8f + 4.1f, wy * 2.8f + 7.4f);
                    float t4y = Mathf.PerlinNoise(wx * 2.8f + 9.6f, wy * 2.8f + 2.8f);
                    wx += (t4x - 0.5f) * 0.12f;
                    wy += (t4y - 0.5f) * 0.12f;

                    // Large soft cloud formations
                    float n1 = Mathf.PerlinNoise(wx * 0.5f + 3.2f, wy * 0.5f + 5.9f);
                    float n2 = Mathf.PerlinNoise(wx * 0.9f + 6.8f, wy * 0.9f + 1.4f);
                    float n3 = Mathf.PerlinNoise(wx * 1.5f + 2.7f, wy * 1.5f + 8.2f);

                    float ink = n1 * 0.5f + n2 * 0.32f + n3 * 0.18f;

                    // Quintic ease for ultra-smooth blending
                    ink = ink * ink * ink * (ink * (ink * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.42f, ink);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        /// <summary>
        /// Creates a full-screen directional wave pattern sprite.
        /// Uses non-radial screen-space wave interference for even coverage.
        /// NO center bias - pattern strength is uniform across the texture.
        /// </summary>
        private static Sprite CreateDirectionalWaveSprite()
        {
            const int width = 256;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            // Directional wave parameters - NO radial/center components
            // Wave 1: diagonal from bottom-left to top-right
            float freq1 = 0.04f;
            float angle1 = 0.785f; // 45 degrees
            float kx1 = Mathf.Cos(angle1) * freq1;
            float ky1 = Mathf.Sin(angle1) * freq1;

            // Wave 2: diagonal from top-left to bottom-right
            float freq2 = 0.035f;
            float angle2 = -0.524f; // -30 degrees
            float kx2 = Mathf.Cos(angle2) * freq2;
            float ky2 = Mathf.Sin(angle2) * freq2;

            // Wave 3: near-horizontal for variety
            float freq3 = 0.028f;
            float angle3 = 0.175f; // 10 degrees
            float kx3 = Mathf.Cos(angle3) * freq3;
            float ky3 = Mathf.Sin(angle3) * freq3;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Screen-space wave interference - NO distance-from-center
                    float wave1 = Mathf.Sin(x * kx1 + y * ky1);
                    float wave2 = Mathf.Sin(x * kx2 + y * ky2 + 1.2f);
                    float wave3 = Mathf.Sin(x * kx3 + y * ky3 + 2.4f);

                    // Combine waves with different weights
                    float combined = wave1 * 0.4f + wave2 * 0.35f + wave3 * 0.25f;

                    // Normalize to 0-1 range
                    float v = (combined + 1f) * 0.5f;

                    // Create soft bands
                    float alpha = Mathf.SmoothStep(0.2f, 0.8f, v) * 0.6f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        /// <summary>
        /// Deterministic hash combining two integers for seed derivation.
        /// </summary>
        private static int HashSeed(string prefix, int value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in prefix)
                {
                    hash = hash * 31 + c;
                }
                hash = hash * 31 + value;
                return hash;
            }
        }

        /// <summary>
        /// When true, uses the new organic background generators (Phase 1 implementation).
        /// When false (default), uses the legacy BackgroundPatternGenerator.
        /// Set via SceneBootstrap.UseOrganicBackgrounds = true before level load.
        /// </summary>
        public static bool UseOrganicBackgrounds { get; set; } = false;

        private static BackgroundPatternGenerator.PatternSprites GetZonePatternSprites(int zoneIndex, int globalSeed)
        {
            var key = new ZonePatternCacheKey(globalSeed, zoneIndex);
            if (_zonePatternsByKey.TryGetValue(key, out var cached) && cached.Macro != null)
            {
                return cached;
            }

            var zoneTheme = BackgroundRules.GetZoneTheme(zoneIndex, globalSeed);

            BackgroundPatternGenerator.PatternSprites generated;

            if (UseOrganicBackgrounds)
            {
                // Use the new organic archetype-based generators
                var organicRequest = new OrganicBackgroundGenerator.OrganicPatternRequest
                {
                    ZoneIndex = zoneIndex,
                    ZoneSeed = BackgroundRules.GetZoneSeed(globalSeed, zoneIndex),
                    Density = zoneTheme.DensityProfile,
                    MacroCount = zoneTheme.MacroCount,
                    MesoCount = zoneTheme.MesoCount,
                    MicroCount = zoneTheme.MicroCount
                };

                var organicGenerated = OrganicBackgroundGenerator.Generate(organicRequest);

                // Convert to legacy PatternSprites format
                generated = new BackgroundPatternGenerator.PatternSprites
                {
                    Macro = organicGenerated.Macro,
                    Meso = organicGenerated.Meso,
                    Accent = organicGenerated.Accent,
                    Micro = organicGenerated.Micro,
                    GenerationMilliseconds = organicGenerated.GenerationMilliseconds
                };

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"Decantra OrganicBackground zone={zoneIndex} archetype={organicGenerated.Archetype} ms={organicGenerated.GenerationMilliseconds:0.0}");
#endif
            }
            else
            {
                // Use the legacy pattern generator
                var request = new BackgroundPatternGenerator.PatternRequest
                {
                    PrimaryFamily = zoneTheme.PrimaryGeneratorFamily,
                    SecondaryFamily = zoneTheme.SecondaryGeneratorFamily,
                    Density = zoneTheme.DensityProfile,
                    MacroCount = zoneTheme.MacroCount,
                    MesoCount = zoneTheme.MesoCount,
                    MicroCount = zoneTheme.MicroCount,
                    ZoneSeed = BackgroundRules.GetZoneSeed(globalSeed, zoneIndex)
                };

                generated = BackgroundPatternGenerator.Generate(request);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.Log($"Decantra BackgroundPattern zone={zoneIndex} family={zoneTheme.PrimaryGeneratorFamily} ms={generated.GenerationMilliseconds:0.0}");
#endif
            }

            _zonePatternsByKey[key] = generated;
            return generated;
        }

        /// <summary>
        /// Updates background structural sprites for a given level.
        /// Call this from GameController when transitioning levels.
        /// NOTE: In version 0.0.2, sprites were not dynamically updated per-level.
        /// This method is now a no-op to preserve the original nebulous, cloudy background.
        /// </summary>
        public static void UpdateBackgroundSpritesForLevel(int levelIndex, int globalSeed, Image flowImage, Image shapesImage, Image bubblesImage, Image largeStructureImage, Image detailImage)
        {
            // No-op: Preserve 0.0.2 behavior where background sprites are set once during CreateBackground()
            // and not dynamically replaced with procedural patterns.
        }

        private static Sprite CreateOrganicShapesSprite()
        {
            return CreateOrganicShapesSprite(HashSeed("bg-group", 0) ^ HashSeed("bg-level", 0));
        }

        private static Sprite CreateOrganicShapesSprite(int seed)
        {
            const int width = 288;
            const int height = 288;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);
            float offsetX = (float)rand.NextDouble() * 50f;
            float offsetY = (float)rand.NextDouble() * 50f;

            // Amorphous vapor clouds - extremely soft and dreamy
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Multi-layer turbulent domain warp for vapor/smoke effect
                    float tw1x = Mathf.PerlinNoise(nx * 0.5f + offsetX, ny * 0.5f + offsetY + 3.1f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.5f + offsetX + 7.8f, ny * 0.5f + offsetY);
                    float wx = nx + (tw1x - 0.5f) * 0.9f;
                    float wy = ny + (tw1y - 0.5f) * 0.9f;

                    // Second turbulence cascade
                    float tw2x = Mathf.PerlinNoise(wx * 0.8f + 4.6f, wy * 0.8f + 9.2f);
                    float tw2y = Mathf.PerlinNoise(wx * 0.8f + 2.3f, wy * 0.8f + 5.7f);
                    wx += (tw2x - 0.5f) * 0.5f;
                    wy += (tw2y - 0.5f) * 0.5f;

                    // Third cascade for fine turbulence
                    float tw3x = Mathf.PerlinNoise(wx * 1.3f + 8.4f, wy * 1.3f + 1.6f);
                    float tw3y = Mathf.PerlinNoise(wx * 1.3f + 6.1f, wy * 1.3f + 3.9f);
                    wx += (tw3x - 0.5f) * 0.25f;
                    wy += (tw3y - 0.5f) * 0.25f;

                    // Ultra-low frequency vapor formations
                    float n1 = Mathf.PerlinNoise(wx * 0.4f + offsetX + 2.5f, wy * 0.4f + offsetY + 6.8f);
                    float n2 = Mathf.PerlinNoise(wx * 0.7f + 5.2f, wy * 0.7f + 8.9f);
                    float n3 = Mathf.PerlinNoise(wx * 1.1f + 9.3f, wy * 1.1f + 4.1f);

                    float vapor = n1 * 0.55f + n2 * 0.3f + n3 * 0.15f;

                    // Quintic smoothstep for impossibly soft edges
                    vapor = vapor * vapor * vapor * (vapor * (vapor * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.32f, vapor);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        private static Sprite CreateBubblesSprite()
        {
            return CreateBubblesSprite(HashSeed("bg-group", 0) ^ HashSeed("bg-level", 0));
        }

        private static Sprite CreateBubblesSprite(int seed)
        {
            const int width = 320;
            const int height = 320;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);
            float offsetX = (float)rand.NextDouble() * 30f;
            float offsetY = (float)rand.NextDouble() * 30f;

            // Ethereal luminous nebulae - soft glowing clouds with no hard edges
            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Heavy turbulent flow for nebula-like shapes
                    float tw1x = Mathf.PerlinNoise(nx * 0.7f + offsetX, ny * 0.7f + offsetY + 2.4f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.7f + offsetX + 6.1f, ny * 0.7f + offsetY + 8.3f);
                    float wx = nx + (tw1x - 0.5f) * 0.7f;
                    float wy = ny + (tw1y - 0.5f) * 0.7f;

                    float tw2x = Mathf.PerlinNoise(wx * 1.1f + 3.7f, wy * 1.1f + 9.5f);
                    float tw2y = Mathf.PerlinNoise(wx * 1.1f + 8.2f, wy * 1.1f + 1.8f);
                    wx += (tw2x - 0.5f) * 0.4f;
                    wy += (tw2y - 0.5f) * 0.4f;

                    float tw3x = Mathf.PerlinNoise(wx * 1.7f + 5.9f, wy * 1.7f + 4.3f);
                    float tw3y = Mathf.PerlinNoise(wx * 1.7f + 2.1f, wy * 1.7f + 7.6f);
                    wx += (tw3x - 0.5f) * 0.2f;
                    wy += (tw3y - 0.5f) * 0.2f;

                    // Layered nebula with glow concentrations
                    float n1 = Mathf.PerlinNoise(wx * 0.6f + offsetX + 4.2f, wy * 0.6f + offsetY + 7.9f);
                    float n2 = Mathf.PerlinNoise(wx * 1.0f + 6.5f, wy * 1.0f + 2.8f);
                    float n3 = Mathf.PerlinNoise(wx * 1.6f + 1.3f, wy * 1.6f + 5.4f);

                    // Create glowing orb concentrations
                    float base_cloud = n1 * 0.5f + n2 * 0.33f + n3 * 0.17f;

                    // Threshold for glow spots while keeping everything soft
                    float glow = Mathf.SmoothStep(0.38f, 0.72f, base_cloud);

                    // Quintic for ultra-soft edges
                    glow = glow * glow * glow * (glow * (glow * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.0f, 0.35f, glow);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        private static Sprite CreateGeometricShapesSprite(int seed)
        {
            const int width = 192;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var centers = new List<Vector2>(10);
            var sizes = new List<Vector2>(10);
            var rand = new System.Random(seed);
            int shapeCount = 10 + rand.Next(0, 4);
            for (int i = 0; i < shapeCount; i++)
            {
                centers.Add(new Vector2((float)rand.NextDouble() * width, (float)rand.NextDouble() * height));
                sizes.Add(new Vector2(Mathf.Lerp(20f, 60f, (float)rand.NextDouble()), Mathf.Lerp(18f, 52f, (float)rand.NextDouble())));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = 0f;
                    for (int i = 0; i < centers.Count; i++)
                    {
                        float dx = Mathf.Abs(x - centers[i].x);
                        float dy = Mathf.Abs(y - centers[i].y);
                        float rx = sizes[i].x;
                        float ry = sizes[i].y;
                        float diamond = 1f - ((dx / rx) + (dy / ry));
                        if (diamond > 0f)
                        {
                            v = Mathf.Max(v, diamond * diamond);
                        }
                    }
                    float alpha = Mathf.SmoothStep(0f, 1f, v);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 192f);
        }

        private static Sprite CreateRibbonStreamsSprite(int seed)
        {
            const int width = 256;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);
            float phaseA = (float)rand.NextDouble() * Mathf.PI * 2f;
            float phaseB = (float)rand.NextDouble() * Mathf.PI * 2f;
            float freqA = Mathf.Lerp(0.035f, 0.065f, (float)rand.NextDouble());
            float freqB = Mathf.Lerp(0.025f, 0.055f, (float)rand.NextDouble());

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float primary = Mathf.Sin((x * freqA + y * freqB) + phaseA);
                    float secondary = Mathf.Sin((x * freqB - y * freqA) + phaseB);
                    float combined = (primary + secondary) * 0.5f;
                    float band = Mathf.Abs(combined);
                    float alpha = Mathf.SmoothStep(0.2f, 0.8f, 1f - band);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        private static Sprite CreateLargeStructureSprite()
        {
            return CreateLargeStructureSprite(HashSeed("bg-group", 0) ^ HashSeed("bg-level", 0));
        }

        private static Sprite CreateLargeStructureSprite(int seed)
        {
            const int width = 384;
            const int height = 384;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);

            // All themes are ultra-soft atmospheric phenomena
            int themeSelector = (seed >> 16) & 0xFF;
            int themeIndex = themeSelector % 4;

            switch (themeIndex)
            {
                case 0: // Cumulus dreamscape - massive soft cloud masses
                    RenderCumulusDreamscape(texture, width, height, rand);
                    break;
                case 1: // Stratospheric haze - gentle horizontal layers
                    RenderStratosphericHaze(texture, width, height, rand);
                    break;
                case 2: // Watercolor bloom - organic spreading pigment
                    RenderWatercolorBloom(texture, width, height, rand);
                    break;
                case 3: // Borealis flow - ethereal flowing ribbons
                    RenderBorealisFlow(texture, width, height, rand);
                    break;
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
        }

        /// <summary>
        /// Massive soft cloud formations like dreamy cumulus
        /// </summary>
        private static void RenderCumulusDreamscape(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 40f;
            float offsetY = (float)rand.NextDouble() * 40f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Extreme multi-layer turbulent warping
                    float tw1x = Mathf.PerlinNoise(nx * 0.4f + offsetX, ny * 0.4f + offsetY + 1.8f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.4f + offsetX + 5.3f, ny * 0.4f + offsetY + 9.1f);
                    float wx = nx + (tw1x - 0.5f) * 1.0f;
                    float wy = ny + (tw1y - 0.5f) * 1.0f;

                    float tw2x = Mathf.PerlinNoise(wx * 0.7f + 3.6f, wy * 0.7f + 7.2f);
                    float tw2y = Mathf.PerlinNoise(wx * 0.7f + 8.9f, wy * 0.7f + 2.4f);
                    wx += (tw2x - 0.5f) * 0.55f;
                    wy += (tw2y - 0.5f) * 0.55f;

                    float tw3x = Mathf.PerlinNoise(wx * 1.2f + 6.1f, wy * 1.2f + 4.5f);
                    float tw3y = Mathf.PerlinNoise(wx * 1.2f + 2.7f, wy * 1.2f + 8.3f);
                    wx += (tw3x - 0.5f) * 0.28f;
                    wy += (tw3y - 0.5f) * 0.28f;

                    // Ultra-low frequency for massive cloud forms
                    float n1 = Mathf.PerlinNoise(wx * 0.3f + offsetX + 4.4f, wy * 0.3f + offsetY + 7.7f);
                    float n2 = Mathf.PerlinNoise(wx * 0.55f + 9.2f, wy * 0.55f + 1.6f);
                    float n3 = Mathf.PerlinNoise(wx * 0.9f + 3.1f, wy * 0.9f + 5.8f);

                    float cumulus = n1 * 0.55f + n2 * 0.3f + n3 * 0.15f;

                    // Quintic smoothstep for impossibly soft edges
                    cumulus = cumulus * cumulus * cumulus * (cumulus * (cumulus * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.42f, cumulus);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Gentle horizontal stratospheric haze layers
        /// </summary>
        private static void RenderStratosphericHaze(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 40f;
            float offsetY = (float)rand.NextDouble() * 40f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Horizontal-biased turbulence
                    float tw1x = Mathf.PerlinNoise(nx * 0.3f + offsetX, ny * 1.2f + offsetY + 3.5f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.5f + offsetX + 7.8f, ny * 0.8f + offsetY);
                    float wx = nx + (tw1x - 0.5f) * 0.5f;
                    float wy = ny + (tw1y - 0.5f) * 0.25f;

                    float tw2x = Mathf.PerlinNoise(wx * 0.6f + 4.2f, wy * 1.5f + 8.1f);
                    float tw2y = Mathf.PerlinNoise(wx * 0.8f + 9.6f, wy * 1.0f + 2.3f);
                    wx += (tw2x - 0.5f) * 0.3f;
                    wy += (tw2y - 0.5f) * 0.12f;

                    // Horizontal banding with organic wavering
                    float haze1 = Mathf.PerlinNoise(wx * 0.4f + offsetX + 5.7f, wy * 2.0f + offsetY + 1.9f);
                    float haze2 = Mathf.PerlinNoise(wx * 0.6f + 2.1f, wy * 1.4f + 6.4f);
                    float haze3 = Mathf.PerlinNoise(wx * 0.9f + 8.3f, wy * 0.8f + 3.6f);

                    float combined = haze1 * 0.5f + haze2 * 0.33f + haze3 * 0.17f;
                    combined = combined * combined * combined * (combined * (combined * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.38f, combined);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Watercolor bloom - organic spreading pigment in water
        /// </summary>
        private static void RenderWatercolorBloom(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 40f;
            float offsetY = (float)rand.NextDouble() * 40f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Extreme turbulent warping for watercolor bleeding effect
                    float tw1x = Mathf.PerlinNoise(nx * 0.5f + offsetX, ny * 0.5f + offsetY + 2.7f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.5f + offsetX + 8.4f, ny * 0.5f + offsetY + 5.1f);
                    float wx = nx + (tw1x - 0.5f) * 0.95f;
                    float wy = ny + (tw1y - 0.5f) * 0.95f;

                    float tw2x = Mathf.PerlinNoise(wx * 0.8f + 6.2f, wy * 0.8f + 3.9f);
                    float tw2y = Mathf.PerlinNoise(wx * 0.8f + 1.5f, wy * 0.8f + 9.3f);
                    wx += (tw2x - 0.5f) * 0.5f;
                    wy += (tw2y - 0.5f) * 0.5f;

                    float tw3x = Mathf.PerlinNoise(wx * 1.4f + 4.8f, wy * 1.4f + 7.6f);
                    float tw3y = Mathf.PerlinNoise(wx * 1.4f + 9.7f, wy * 1.4f + 2.1f);
                    wx += (tw3x - 0.5f) * 0.25f;
                    wy += (tw3y - 0.5f) * 0.25f;

                    // Soft bloom formations
                    float n1 = Mathf.PerlinNoise(wx * 0.35f + offsetX + 3.3f, wy * 0.35f + offsetY + 6.6f);
                    float n2 = Mathf.PerlinNoise(wx * 0.6f + 8.1f, wy * 0.6f + 4.4f);
                    float n3 = Mathf.PerlinNoise(wx * 1.0f + 2.5f, wy * 1.0f + 9.8f);

                    float bloom = n1 * 0.52f + n2 * 0.32f + n3 * 0.16f;
                    bloom = bloom * bloom * bloom * (bloom * (bloom * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.4f, bloom);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Ethereal flowing aurora-like ribbons
        /// </summary>
        private static void RenderBorealisFlow(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 40f;
            float offsetY = (float)rand.NextDouble() * 40f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Vertical-biased turbulence for flowing ribbons
                    float tw1x = Mathf.PerlinNoise(nx * 1.5f + offsetX, ny * 0.4f + offsetY + 4.2f);
                    float tw1y = Mathf.PerlinNoise(nx * 0.6f + offsetX + 7.1f, ny * 0.8f + offsetY);
                    float wx = nx + (tw1x - 0.5f) * 0.35f;
                    float wy = ny + (tw1y - 0.5f) * 0.6f;

                    float tw2x = Mathf.PerlinNoise(wx * 2.0f + 5.8f, wy * 0.6f + 9.4f);
                    float tw2y = Mathf.PerlinNoise(wx * 0.9f + 2.6f, wy * 1.2f + 3.7f);
                    wx += (tw2x - 0.5f) * 0.2f;
                    wy += (tw2y - 0.5f) * 0.35f;

                    // Flowing vertical wisps
                    float wisp1 = Mathf.PerlinNoise(wx * 2.5f + offsetX + 1.4f, wy * 0.5f + offsetY + 8.9f);
                    float wisp2 = Mathf.PerlinNoise(wx * 1.8f + 6.3f, wy * 0.7f + 2.5f);
                    float wisp3 = Mathf.PerlinNoise(wx * 1.2f + 9.5f, wy * 1.0f + 5.2f);

                    float aurora = wisp1 * 0.48f + wisp2 * 0.32f + wisp3 * 0.2f;
                    aurora = aurora * aurora * aurora * (aurora * (aurora * 6f - 15f) + 10f);

                    float alpha = Mathf.Lerp(0.01f, 0.36f, aurora);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Soft cloud puffs - replaces hard-edged scattered circles
        /// </summary>
        private static void RenderScatteredCircles(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 100f;
            float offsetY = (float)rand.NextDouble() * 100f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Soft scattered cloud puffs using domain-warped noise
                    float warp1 = Mathf.PerlinNoise(nx * 2f + offsetX, ny * 2f + offsetY) * 0.35f;
                    float warp2 = Mathf.PerlinNoise(nx * 2f + offsetX + 6.2f, ny * 2f + offsetY + 3.8f) * 0.35f;
                    float warpedX = nx + warp1;
                    float warpedY = ny + warp2;

                    float n1 = Mathf.PerlinNoise(warpedX * 2f + offsetX, warpedY * 2f + offsetY);
                    float n2 = Mathf.PerlinNoise(warpedX * 3.5f + 4.1f, warpedY * 3.5f + 7.3f);

                    float cloud = n1 * 0.65f + n2 * 0.35f;
                    cloud = cloud * cloud * (3f - 2f * cloud);

                    float alpha = Mathf.Lerp(0.02f, 0.5f, cloud);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Soft flowing mist - replaces hard-edged geometric polygons
        /// </summary>
        private static void RenderGeometricPolygons(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 100f;
            float offsetY = (float)rand.NextDouble() * 100f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Multiple layers of soft flowing mist
                    float warp = Mathf.PerlinNoise(nx * 1.5f + offsetX, ny * 1.5f + offsetY) * 0.4f;
                    float warpedX = nx + warp;

                    float n1 = Mathf.PerlinNoise(warpedX * 1.8f + offsetX, ny * 2.5f + offsetY);
                    float n2 = Mathf.PerlinNoise(warpedX * 3f + 5.2f, ny * 1.2f + 8.4f);
                    float n3 = Mathf.PerlinNoise(warpedX * 2.2f + 9.7f, ny * 3.8f + 2.1f);

                    float mist = n1 * 0.5f + n2 * 0.3f + n3 * 0.2f;
                    mist = mist * mist * (3f - 2f * mist);

                    float alpha = Mathf.Lerp(0.03f, 0.45f, mist);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Soft flowing cloud bands - replaces hard-edged wave lines
        /// </summary>
        private static void RenderWaveLines(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 100f;
            float offsetY = (float)rand.NextDouble() * 100f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Soft flowing horizontal bands with domain warping
                    float warpX = Mathf.PerlinNoise(nx * 2f + offsetX, ny * 1.5f + offsetY) * 0.3f;
                    float warpY = Mathf.PerlinNoise(nx * 1.5f + offsetX + 4.5f, ny * 2f + offsetY + 7.2f) * 0.25f;
                    float warpedX = nx + warpX;
                    float warpedY = ny + warpY;

                    // Horizontal flow bias
                    float n1 = Mathf.PerlinNoise(warpedX * 1.2f + offsetX, warpedY * 3f + offsetY);
                    float n2 = Mathf.PerlinNoise(warpedX * 0.8f + 5.8f, warpedY * 2f + 2.3f);
                    float n3 = Mathf.PerlinNoise(warpedX * 2f + 9.1f, warpedY * 1.5f + 6.7f);

                    float flow = n1 * 0.5f + n2 * 0.3f + n3 * 0.2f;
                    flow = flow * flow * (3f - 2f * flow);

                    float alpha = Mathf.Lerp(0.03f, 0.5f, flow);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Soft nebula-like clouds - replaces hard-edged fractal shapes
        /// </summary>
        private static void RenderFractalShapes(Texture2D texture, int width, int height, System.Random rand)
        {
            float offsetX = (float)rand.NextDouble() * 100f;
            float offsetY = (float)rand.NextDouble() * 100f;

            for (int y = 0; y < height; y++)
            {
                float ny = y / (float)height;
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;

                    // Heavy domain warping for organic nebula effect
                    float warp1X = Mathf.PerlinNoise(nx * 1.2f + offsetX, ny * 1.2f + offsetY) * 0.5f;
                    float warp1Y = Mathf.PerlinNoise(nx * 1.2f + offsetX + 6.4f, ny * 1.2f + offsetY + 3.1f) * 0.5f;
                    float warp2X = Mathf.PerlinNoise((nx + warp1X) * 1.8f + 2.7f, (ny + warp1Y) * 1.8f + 8.3f) * 0.3f;
                    float warp2Y = Mathf.PerlinNoise((nx + warp1X) * 1.8f + 9.2f, (ny + warp1Y) * 1.8f + 1.6f) * 0.3f;

                    float warpedX = nx + warp1X + warp2X;
                    float warpedY = ny + warp1Y + warp2Y;

                    // Multiple soft layers for nebula depth
                    float n1 = Mathf.PerlinNoise(warpedX * 0.9f + offsetX, warpedY * 0.9f + offsetY);
                    float n2 = Mathf.PerlinNoise(warpedX * 1.7f + 4.8f, warpedY * 1.7f + 7.2f);
                    float n3 = Mathf.PerlinNoise(warpedX * 2.8f + 1.3f, warpedY * 2.8f + 5.9f);

                    float nebula = n1 * 0.55f + n2 * 0.3f + n3 * 0.15f;
                    nebula = nebula * nebula * nebula * (nebula * (6f * nebula - 15f) + 10f); // Extra smooth

                    float alpha = Mathf.Lerp(0.02f, 0.5f, nebula);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// Creates soft cloud structure sprite - renamed from geometric for clarity.
        /// All render methods now produce cloud-like patterns.
        /// </summary>
        private static Sprite CreateGeometricStructureSprite(int seed)
        {
            const int width = 256;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var rand = new System.Random(seed);

            // All themes now produce soft cloud variations
            int themeSelector = (seed >> 16) & 0xFF;
            int themeIndex = themeSelector % 4;

            switch (themeIndex)
            {
                case 0: // Soft cloud puffs
                    RenderScatteredCircles(texture, width, height, rand);
                    break;
                case 1: // Flowing mist
                    RenderGeometricPolygons(texture, width, height, rand);
                    break;
                case 2: // Cloud bands
                    RenderWaveLines(texture, width, height, rand);
                    break;
                case 3: // Nebula clouds
                    RenderFractalShapes(texture, width, height, rand);
                    break;
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 256f);
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
