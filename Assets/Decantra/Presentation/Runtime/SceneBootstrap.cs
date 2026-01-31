/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using System.Reflection;
using Decantra.Domain.Model;
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        public static void EnsureScene()
        {
            var existingController = Object.FindFirstObjectByType<GameController>();
            if (existingController != null && HasRequiredWiring(existingController))
            {
                EnsureRestartDialog(existingController);
                WireResetButton(existingController);
                WireRestartButton(existingController);
                WireShareButton(existingController);
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

            var banner = CreateLevelBanner(canvas.transform);
            SetPrivateField(controller, "levelBanner", banner);

            var intro = CreateIntroBanner(canvas.transform);
            SetPrivateField(controller, "introBanner", intro);

            var outOfMoves = CreateOutOfMovesBanner(canvas.transform);
            SetPrivateField(controller, "outOfMovesBanner", outOfMoves);

            var settings = CreateSettingsPanel(canvas.transform, controller);
            WireResetButton(controller);

            var restartDialog = CreateRestartDialog(canvas.transform);
            SetPrivateField(controller, "restartDialog", restartDialog);
            WireRestartButton(controller);
            WireShareButton(controller);

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
            largeImage.color = new Color(1f, 1f, 1f, 0.25f);
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
            flowImage.color = new Color(1f, 1f, 1f, 0.38f);
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
            shapesImage.color = new Color(1f, 1f, 1f, 0.22f);
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
            detailImage.color = new Color(1f, 1f, 1f, 0.2f);
            detailImage.type = Image.Type.Tiled;
            detailImage.raycastTarget = false;
            var detailDrift = detailGo.AddComponent<BackgroundDrift>();
            SetPrivateField(detailDrift, "driftAmplitude", new Vector2(6f, 8f));
            SetPrivateField(detailDrift, "driftSpeed", new Vector2(0.06f, 0.05f));
            SetPrivateField(detailDrift, "rotationAmplitude", 0.2f);
            SetPrivateField(detailDrift, "rotationSpeed", 0.05f);

            var vignetteGo = CreateUiChild(parent, "BackgroundVignette");
            var vignetteRect = vignetteGo.GetComponent<RectTransform>();
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;

            var vignetteImage = vignetteGo.AddComponent<Image>();
            vignetteImage.sprite = CreateVignetteSprite();
            vignetteImage.color = new Color(0f, 0f, 0f, 0.3f);
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.raycastTarget = false;

            bg.transform.SetAsFirstSibling();
            largeStructureGo.transform.SetSiblingIndex(1);
            flowGo.transform.SetSiblingIndex(2);
            shapesGo.transform.SetSiblingIndex(3);
            detailGo.transform.SetSiblingIndex(4);
            vignetteGo.transform.SetSiblingIndex(5);

            return new BackgroundLayers
            {
                Base = baseImage,
                Detail = detailImage,
                Flow = flowImage,
                Shapes = shapesImage,
                Vignette = vignetteImage
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

            var brandGo = CreateUiChild(safeRoot.transform, "BrandLockup");
            var brandRect = brandGo.GetComponent<RectTransform>();
            brandRect.anchorMin = new Vector2(0.5f, 1f);
            brandRect.anchorMax = new Vector2(0.5f, 1f);
            brandRect.pivot = new Vector2(0.5f, 1f);
            brandRect.anchoredPosition = new Vector2(0, -20);
            brandRect.sizeDelta = new Vector2(600, 100);

            var brandImage = brandGo.AddComponent<Image>();
            brandImage.sprite = GetRoundedSprite();
            brandImage.type = Image.Type.Sliced;
            brandImage.color = new Color(1f, 1f, 1f, 0.15f);
            brandImage.raycastTarget = false;

            var brandLayout = brandGo.AddComponent<HorizontalLayoutGroup>();
            brandLayout.childAlignment = TextAnchor.MiddleCenter;
            brandLayout.childForceExpandHeight = false;
            brandLayout.childForceExpandWidth = false;
            brandLayout.spacing = 12f;
            brandLayout.padding = new RectOffset(16, 16, 8, 8);

            var logoSprite = Resources.Load<Sprite>("DecantraLogo");
            var logoGo = CreateUiChild(brandGo.transform, "TitleLogo");
            var logoImage = logoGo.AddComponent<Image>();
            logoImage.sprite = logoSprite;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.sizeDelta = new Vector2(72, 72);

            var titleText = CreateTitleText(brandGo.transform, "TitleText", "Decantra");
            titleText.fontSize = 56;
            titleText.color = new Color(1f, 0.97f, 0.9f, 1f);

            var hudViewGo = CreateUiChild(hudRoot.transform, "HudView");
            var hudView = hudViewGo.GetComponent<HudView>() ?? hudViewGo.AddComponent<HudView>();

            var topHud = CreateUiChild(safeRoot.transform, "TopHud");
            var topRect = topHud.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.5f, 1f);
            topRect.anchorMax = new Vector2(0.5f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0, -140);
            topRect.sizeDelta = new Vector2(1000, 150);

            var topLayout = topHud.AddComponent<HorizontalLayoutGroup>();
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;
            topLayout.spacing = 16f;

            var levelText = CreateStatPanel(topHud.transform, "LevelPanel", "LEVEL", out var levelPanel);
            var movesText = CreateStatPanel(topHud.transform, "MovesPanel", "MOVES", out _);
            var scoreText = CreateStatPanel(topHud.transform, "ScorePanel", "SCORE", out _);

            _ = AddPanelButton(levelPanel);
            _ = CreateShareButton(topHud.transform);

            var bottomHud = CreateUiChild(hudRoot.transform, "BottomHud");
            var bottomRect = bottomHud.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector2(0f, 60f);
            bottomRect.sizeDelta = new Vector2(980, 150);

            var bottomLayout = bottomHud.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = false;
            bottomLayout.spacing = 16f;

            var highScoreText = CreateStatPanel(bottomHud.transform, "HighScorePanel", "HIGH", out _);
            var maxLevelText = CreateStatPanel(bottomHud.transform, "MaxLevelPanel", "MAX LV", out _);
            CreateResetButton(bottomHud.transform);
            CreateRestartButton(bottomHud.transform);

            SetPrivateField(hudView, "levelText", levelText);
            SetPrivateField(hudView, "movesText", movesText);
            SetPrivateField(hudView, "scoreText", scoreText);
            SetPrivateField(hudView, "highScoreText", highScoreText);
            SetPrivateField(hudView, "maxLevelText", maxLevelText);
            SetPrivateField(hudView, "titleText", titleText);

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
            areaRect.offsetMax = new Vector2(0, -300);

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

            var hitArea = bottleGo.AddComponent<Image>();
            hitArea.color = new Color(0, 0, 0, 0);
            hitArea.raycastTarget = true;

            var shadowGo = CreateUiChild(bottleGo.transform, "Shadow");
            var shadow = shadowGo.AddComponent<Image>();
            shadow.sprite = rounded;
            shadow.type = Image.Type.Sliced;
            shadow.color = new Color(0f, 0f, 0f, 0.15f);
            shadow.raycastTarget = false;
            var shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(150, 370);
            shadowRect.anchoredPosition = new Vector2(8, -6);

            var outlineGo = CreateUiChild(bottleGo.transform, "Outline");
            var outline = outlineGo.AddComponent<Image>();
            outline.sprite = rounded;
            outline.type = Image.Type.Sliced;
            outline.color = new Color(0.55f, 0.6f, 0.68f, 0.85f);
            outline.raycastTarget = false;
            var outlineRect = outlineGo.GetComponent<RectTransform>();
            outlineRect.anchorMin = new Vector2(0.5f, 0.5f);
            outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            outlineRect.sizeDelta = new Vector2(140, 360);

            var bodyGo = CreateUiChild(bottleGo.transform, "Body");
            var body = bodyGo.AddComponent<Image>();
            body.sprite = rounded;
            body.type = Image.Type.Sliced;
            body.color = new Color(0.5f, 0.55f, 0.65f, 0.12f);
            body.raycastTarget = false;
            var bodyRect = bodyGo.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.sizeDelta = new Vector2(124, 330);
            bodyRect.anchoredPosition = new Vector2(0, -8);

            var baseGo = CreateUiChild(bottleGo.transform, "BasePlate");
            var basePlate = baseGo.AddComponent<Image>();
            basePlate.sprite = rounded;
            basePlate.type = Image.Type.Sliced;
            basePlate.color = new Color(0.12f, 0.12f, 0.16f, 0.75f);
            basePlate.raycastTarget = false;
            var baseRect = baseGo.GetComponent<RectTransform>();
            baseRect.anchorMin = new Vector2(0.5f, 0f);
            baseRect.anchorMax = new Vector2(0.5f, 0f);
            baseRect.pivot = new Vector2(0.5f, 0f);
            baseRect.sizeDelta = new Vector2(120, 40);
            baseRect.anchoredPosition = new Vector2(0, 6);
            baseGo.SetActive(false);

            var highlightGo = CreateUiChild(bodyGo.transform, "Highlight");
            var highlight = highlightGo.AddComponent<Image>();
            highlight.sprite = rounded;
            highlight.type = Image.Type.Sliced;
            highlight.color = new Color(1f, 1f, 1f, 0.08f);
            highlight.raycastTarget = false;
            var highlightRect = highlightGo.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0.5f, 0.5f);
            highlightRect.sizeDelta = new Vector2(70, 240);
            highlightRect.anchoredPosition = new Vector2(24, 30);

            var neckGo = CreateUiChild(bottleGo.transform, "Neck");
            var neck = neckGo.AddComponent<Image>();
            neck.sprite = rounded;
            neck.type = Image.Type.Sliced;
            neck.color = new Color(0.5f, 0.55f, 0.65f, 0.18f);
            neck.raycastTarget = false;
            var neckRect = neckGo.GetComponent<RectTransform>();
            neckRect.anchorMin = new Vector2(0.5f, 0.5f);
            neckRect.anchorMax = new Vector2(0.5f, 0.5f);
            neckRect.pivot = new Vector2(0.5f, 0.5f);
            neckRect.sizeDelta = new Vector2(60, 50);
            neckRect.anchoredPosition = new Vector2(0, 178);

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

            var liquidRoot = CreateUiChild(bodyGo.transform, "LiquidRoot");
            var liquidRect = liquidRoot.GetComponent<RectTransform>();
            liquidRect.anchorMin = new Vector2(0.5f, 0);
            liquidRect.anchorMax = new Vector2(0.5f, 0);
            liquidRect.pivot = new Vector2(0.5f, 0);
            liquidRect.sizeDelta = new Vector2(100, 280);
            liquidRect.anchoredPosition = new Vector2(0, 10);

            var bottleView = bottleGo.GetComponent<BottleView>() ?? bottleGo.AddComponent<BottleView>();
            SetPrivateField(bottleView, "palette", palette);
            SetPrivateField(bottleView, "slotRoot", liquidRect);
            SetPrivateField(bottleView, "outline", outline);
            SetPrivateField(bottleView, "body", body);
            SetPrivateField(bottleView, "basePlate", basePlate);
            SetPrivateField(bottleView, "stopper", stopper);
            SetPrivateField(bottleView, "outlineBaseColor", outline.color);
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
            text.fontSize = 96;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.text = value;
            text.supportRichText = true;
            text.raycastTarget = false;
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.9f));

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(900, 90);
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860, 260);

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(860, 260);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.14f);
            panelImage.raycastTarget = false;

            var content = CreateUiChild(panel.transform, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(720, 200);

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 22f;

            var logoSprite = Resources.Load<Sprite>("DecantraLogo");
            var logoGo = CreateUiChild(content.transform, "Logo");
            var logoImage = logoGo.AddComponent<Image>();
            logoImage.sprite = logoSprite;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
            var logoRect = logoGo.GetComponent<RectTransform>();
            logoRect.sizeDelta = new Vector2(110, 110);

            var text = CreateTitleText(content.transform, "IntroTitle", "DECANTRA");
            text.fontSize = 62;
            text.color = new Color(1f, 0.96f, 0.85f, 1f);

            var banner = root.AddComponent<IntroBanner>();
            SetPrivateField(banner, "panel", panelRect);
            SetPrivateField(banner, "titleText", text);
            SetPrivateField(banner, "canvasGroup", group);
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
            panelRect.sizeDelta = new Vector2(760, 300);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.18f);
            panelImage.raycastTarget = false;

            var message = CreateHudText(panel.transform, "MessageText");
            message.fontSize = 38;
            message.text = "Are you sure? This will reset all progress.";
            message.color = new Color(1f, 0.95f, 0.7f, 1f);

            var buttonsRoot = CreateUiChild(panel.transform, "Buttons");
            var buttonsRect = buttonsRoot.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.5f, 0f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0f);
            buttonsRect.pivot = new Vector2(0.5f, 0f);
            buttonsRect.anchoredPosition = new Vector2(0f, 20f);
            buttonsRect.sizeDelta = new Vector2(600, 100);

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
                rectTransform.sizeDelta = new Vector2(220, 90);
                var element = buttonGo.AddComponent<LayoutElement>();
                element.minWidth = 220;
                element.minHeight = 90;
                var button = buttonGo.AddComponent<Button>();
                button.targetGraphic = image;
                var text = CreateHudText(buttonGo.transform, "Label");
                text.fontSize = 34;
                text.text = label;
                text.color = Color.white;
                return button;
            }

            var cancelButton = CreateDialogButton("CancelButton", "CANCEL", new Color(1f, 1f, 1f, 0.25f));
            var restartButton = CreateDialogButton("ConfirmRestartButton", "RESTART", new Color(1f, 0.5f, 0.4f, 0.72f));

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
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.18f);
            image.raycastTarget = false;

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 300;
            element.minHeight = 140;
            element.flexibleWidth = 1f;

            var text = CreateHudText(panel.transform, "Value");
            text.fontSize = 38;
            text.text = label;
            text.color = new Color(1f, 0.98f, 0.92f, 1f);
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
            image.color = new Color(1f, 1f, 1f, 0.22f);

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 200;
            element.minHeight = 140;

            var button = panel.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateHudText(panel.transform, "Label");
            text.fontSize = 32;
            text.text = "RESET";
            text.color = new Color(1f, 0.98f, 0.92f, 1f);
            AddTextEffects(text, new Color(0f, 0f, 0f, 0.75f));
            return button;
        }

        private static Button CreateRestartButton(Transform parent)
        {
            var panel = CreateUiChild(parent, "RestartButton");
            var image = panel.AddComponent<Image>();
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.18f);

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 140);
            var element = panel.AddComponent<LayoutElement>();
            element.minWidth = 240;
            element.minHeight = 140;

            var button = panel.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateHudText(panel.transform, "Label");
            text.fontSize = 30;
            text.text = "RESTART";
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
        }

        private static void WireRestartButton(GameController controller)
        {
            if (controller == null) return;
            var restartGo = GameObject.Find("RestartButton");
            if (restartGo == null) return;
            var button = restartGo.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(controller.RequestRestartGame);
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760, 240);

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var panel = CreateUiChild(root.transform, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760, 240);

            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GetRoundedSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(1f, 1f, 1f, 0.18f);
            panelImage.raycastTarget = false;

            var starsText = CreateTitleText(panel.transform, "StarsText", "★★★");
            starsText.fontSize = 72;
            starsText.color = new Color(1f, 0.95f, 0.75f, 1f);

            var scoreText = CreateTitleText(panel.transform, "ScoreText", "+0");
            scoreText.fontSize = 48;
            scoreText.color = new Color(0.5f, 1f, 0.5f, 1f);
            var scoreRect = scoreText.GetComponent<RectTransform>();
            scoreRect.anchoredPosition = new Vector2(0, -60);

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
            SetPrivateField(banner, "canvasGroup", group);
            return banner;
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
            const int width = 2;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var sky = new Color(0.68f, 0.74f, 0.84f, 1f);
            var mid = new Color(0.32f, 0.42f, 0.55f, 1f);
            var deep = new Color(0.16f, 0.2f, 0.28f, 1f);

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float curve = Mathf.SmoothStep(0f, 1f, t);
                Color color = t < 0.55f
                    ? Color.Lerp(deep, mid, curve / 0.55f)
                    : Color.Lerp(mid, sky, (curve - 0.55f) / 0.45f);

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 96f);
        }

        private static Sprite CreateSoftNoiseSprite()
        {
            const int width = 96;
            const int height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            float scale = 9.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;
                    float n1 = Mathf.PerlinNoise(nx * scale, ny * scale);
                    float n2 = Mathf.PerlinNoise(nx * scale * 1.6f + 11.3f, ny * scale * 1.6f + 6.7f);
                    float noise = Mathf.Lerp(n1, n2, 0.6f);
                    float v = Mathf.Lerp(0.7f, 1.2f, noise);
                    float alpha = Mathf.Lerp(0.2f, 1f, noise);
                    texture.SetPixel(x, y, new Color(v, v, v, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 96f);
        }

        private static Sprite CreateFlowSprite()
        {
            const int width = 192;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width;
                    float ny = y / (float)height;
                    float n1 = Mathf.PerlinNoise(nx * 3.2f, ny * 3.2f);
                    float n2 = Mathf.PerlinNoise(nx * 6.5f + 0.7f, ny * 6.5f + 1.1f);
                    float n3 = Mathf.PerlinNoise(nx * 1.4f + 3.9f, ny * 1.4f + 2.4f);
                    float mix = Mathf.Lerp(n1, n2, 0.55f);
                    mix = Mathf.Lerp(mix, n3, 0.35f);
                    float alpha = Mathf.SmoothStep(0.25f, 0.95f, mix);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 192f);
        }

        private static Sprite CreateOrganicShapesSprite()
        {
            const int width = 192;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var centers = new List<Vector2>(8);
            var radii = new List<float>(8);
            var rand = new System.Random(1337);
            for (int i = 0; i < 8; i++)
            {
                centers.Add(new Vector2((float)rand.NextDouble() * width, (float)rand.NextDouble() * height));
                radii.Add(Mathf.Lerp(28f, 64f, (float)rand.NextDouble()));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = 0f;
                    for (int i = 0; i < centers.Count; i++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), centers[i]);
                        float t = Mathf.Clamp01(1f - dist / radii[i]);
                        v = Mathf.Max(v, t * t);
                    }
                    float alpha = Mathf.SmoothStep(0f, 1f, v);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 192f);
        }

        private static Sprite CreateLargeStructureSprite()
        {
            const int width = 256;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            var centers = new List<Vector2>(4);
            var radii = new List<float>(4);
            var rand = new System.Random(7331);
            for (int i = 0; i < 4; i++)
            {
                centers.Add(new Vector2((float)rand.NextDouble() * width, (float)rand.NextDouble() * height));
                radii.Add(Mathf.Lerp(80f, 140f, (float)rand.NextDouble()));
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = 0f;
                    for (int i = 0; i < centers.Count; i++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), centers[i]);
                        float t = Mathf.Clamp01(1f - dist / radii[i]);
                        float eased = t * t * (3f - 2f * t);
                        v = Mathf.Max(v, eased);
                    }

                    float nx = x / (float)width;
                    float ny = y / (float)height;
                    float noise = Mathf.PerlinNoise(nx * 2.5f + 5.2f, ny * 2.5f + 3.7f);
                    v = Mathf.Lerp(v, v * noise, 0.35f);

                    float alpha = Mathf.SmoothStep(0f, 1f, v);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
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

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            roundedSprite = CreateRoundedRectSprite(64, 12);
            return roundedSprite;
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
