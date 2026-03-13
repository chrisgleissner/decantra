/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.App.Services;
using Decantra.Presentation.Controller;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class TutorialManager : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform highlightFrame;
        [SerializeField] private Image highlightMask;
        [SerializeField] private Graphic dimLayer;
        [SerializeField] private TutorialRaycastBlocker raycastBlocker;
        [SerializeField] private Text instructionText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;

        private readonly List<TutorialStepData> _steps = new List<TutorialStepData>();
        private int _stepIndex;
        private GameController _controller;
        private SettingsStore _settingsStore;
        private Canvas _canvas;
        private bool _initialized;
        private bool _running;
        private RectTransform _activeTarget;
        private TutorialFocusPulse _activePulse;
        private Image _highlightFrameImage;
        private Shadow _highlightFrameShadow;

        private Vector2 _smoothCenter;
        private Vector2 _smoothCenterVelocity;
        private Vector2 _smoothSize;
        private Vector2 _smoothSizeVelocity;
        private bool _hasSmoothState;
        private bool _lastFocusVisible;

        public bool IsRunning => _running;
        public int StepCount => _steps.Count;
        public int CurrentStepIndex => _stepIndex;

        [Preserve]
        private readonly struct RenderDiagnostics
        {
            public RenderDiagnostics(
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

            [Preserve] public string RenderMode { get; }
            [Preserve] public string ScaleMode { get; }
            [Preserve] public Vector2 ReferenceResolution { get; }
            [Preserve] public float MatchWidthOrHeight { get; }
            [Preserve] public bool SpotlightVisible { get; }
            [Preserve] public bool SpotlightMaskActive { get; }
            [Preserve] public Rect SpotlightRectLocal { get; }
            [Preserve] public Rect CanvasRectLocal { get; }
        }

        public void Initialize(GameController controller, SettingsStore settingsStore)
        {
            _controller = controller;
            _settingsStore = settingsStore;
            _canvas = GetComponentInParent<Canvas>();

            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(NextStep);
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipTutorial);
            }

            _initialized = true;
            HideImmediate();
        }

        public void BeginIfFirstLaunch()
        {
            if (!_initialized || _settingsStore == null) return;
            if (_settingsStore.LoadTutorialCompleted()) return;
            BeginTutorial();
        }

        public void BeginReplay()
        {
            if (!_initialized) return;
            BeginTutorial();
        }

        public bool AdvanceStepForAutomation()
        {
            if (!_running)
            {
                return false;
            }

            NextStep();
            return _running;
        }

        public void SuppressForAutomation(bool markCompleted = false)
        {
            _running = false;
            if (markCompleted && _settingsStore != null)
            {
                _settingsStore.SaveTutorialCompleted(true);
            }

            HideImmediate();
        }

        [Preserve]
        private bool TryGetRenderDiagnostics(out object diagnostics)
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            var scaler = _canvas != null ? _canvas.GetComponent<CanvasScaler>() : null;
            string renderMode = _canvas != null ? _canvas.renderMode.ToString() : "unknown";
            string scaleMode = scaler != null ? scaler.uiScaleMode.ToString() : "unknown";
            Vector2 referenceResolution = scaler != null ? scaler.referenceResolution : default;
            float matchWidthOrHeight = scaler != null ? scaler.matchWidthOrHeight : 0f;

            bool spotlightVisible = highlightFrame != null
                && highlightFrame.gameObject.activeSelf
                && _lastFocusVisible;

            bool spotlightMaskActive = highlightMask != null
                && highlightMask.gameObject.activeSelf
                && highlightMask.material != null;

            Rect spotlightRectLocal = new Rect(
                _smoothCenter.x - (_smoothSize.x * 0.5f),
                _smoothCenter.y - (_smoothSize.y * 0.5f),
                _smoothSize.x,
                _smoothSize.y);

            Rect canvasRectLocal = default;
            if (_canvas != null)
            {
                var canvasRectTransform = _canvas.GetComponent<RectTransform>();
                if (canvasRectTransform != null)
                {
                    canvasRectLocal = canvasRectTransform.rect;
                }
            }

            diagnostics = new RenderDiagnostics(
                renderMode,
                scaleMode,
                referenceResolution,
                matchWidthOrHeight,
                spotlightVisible,
                spotlightMaskActive,
                spotlightRectLocal,
                canvasRectLocal);

            return true;
        }

        [Preserve]
        private bool TryGetCurrentStepSnapshot(out int stepIndex, out string targetName)
        {
            stepIndex = _stepIndex;
            targetName = string.Empty;

            if (_stepIndex >= 0 && _stepIndex < _steps.Count)
            {
                targetName = _steps[_stepIndex].TargetObjectName ?? string.Empty;
            }

            return true;
        }

        private void LateUpdate()
        {
            if (!_running) return;
            RefreshHighlight(animated: true);
            _activePulse?.Tick(Time.unscaledTime);
            AnimateHighlightFrame(Time.unscaledTime);
        }

        private void BeginTutorial()
        {
            BuildSteps();
            if (_steps.Count == 0)
            {
                CompleteTutorial();
                return;
            }

            _stepIndex = 0;
            _running = true;
            if (root != null)
            {
                root.gameObject.SetActive(true);
                root.SetAsLastSibling();
            }

            if (dimLayer != null)
            {
                dimLayer.gameObject.SetActive(true);
            }

            CacheHighlightFrameVisuals();

            ShowStep(_stepIndex);
        }

        private void BuildSteps()
        {
            _steps.Clear();
            _steps.Add(new TutorialStepData(
                "highlight-bottles",
                "Bottle_1",
                "These are your bottles. To pour, press and drag a bottle."
            ));
            _steps.Add(new TutorialStepData(
                "drag-pour",
                "Bottle_2",
                "Drag a bottle onto another and release. It pours only if space is available and the top color matches or the target is empty."
            ));

            if (_controller != null && _controller.TryGetSinkBottleObjectName(out string sinkBottleName))
            {
                _steps.Add(new TutorialStepData(
                    "sink-only",
                    sinkBottleName,
                    "Black bottles use heavier, darker glass. They can receive liquid but cannot be picked as sources."
                ));
            }

            _steps.Add(new TutorialStepData(
                "goal",
                "LevelPanel",
                "LEVEL & Difficulty\nDisplays the current level.\nThe three circles show difficulty. The more filled they are, the harder the level."
            ));
            _steps.Add(new TutorialStepData(
                "moves",
                "MovesPanel",
                "MOVES\nCurrent moves versus allowed moves.\nFewer moves give a higher score."
            ));
            _steps.Add(new TutorialStepData(
                "score",
                "ScorePanel",
                "SCORE\nPress SCORE to view your high score and maximum level reached."
            ));
            _steps.Add(new TutorialStepData(
                "reset",
                "ResetButton",
                "Use RESET to restart only the current level."
            ));
            _steps.Add(new TutorialStepData(
                "options",
                "OptionsButton",
                "OPTIONS\nStart a new game from OPTIONS.\nThis resets current score and stars, but preserves high score and maximum level reached."
            ));
            _steps.Add(new TutorialStepData(
                "stars",
                "StarsButton",
                "Earn stars by solving levels.\nOpen STAR SOLUTIONS for help when needed."
            ));
        }

        private void ShowStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
            {
                CompleteTutorial();
                return;
            }

            var step = _steps[index];
            _activeTarget = ResolveTarget(step.TargetObjectName);

            if (_activeTarget == null && step.Optional)
            {
                NextStep();
                return;
            }

            _activePulse?.Dispose();
            _activePulse = _activeTarget != null ? new TutorialFocusPulse(_activeTarget) : null;
            _hasSmoothState = false;

            if (instructionText != null)
            {
                instructionText.text = step.Instruction;
            }

            RefreshHighlight(animated: false);
        }

        private RectTransform ResolveTarget(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return null;
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            return go.GetComponent<RectTransform>();
        }

        private void RefreshHighlight(bool animated)
        {
            if (highlightFrame == null)
            {
                return;
            }

            if (_activeTarget == null)
            {
                highlightFrame.gameObject.SetActive(false);
                if (highlightMask != null)
                {
                    highlightMask.gameObject.SetActive(false);
                }

                // No spotlight — restore plain dim layer so the instruction panel
                // remains readable against a darkened background.
                if (dimLayer != null) dimLayer.gameObject.SetActive(true);
                _lastFocusVisible = false;
                return;
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            var canvasRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null)
            {
                highlightFrame.gameObject.SetActive(false);
                _lastFocusVisible = false;
                return;
            }

            if (!TryCalculateTargetRectInCanvasSpace(canvasRect, _activeTarget, out var targetRect))
            {
                highlightFrame.gameObject.SetActive(false);
                _lastFocusVisible = false;
                return;
            }

            float padding = 20f;
            var targetCenter = targetRect.center;
            var targetSize = targetRect.size + new Vector2(padding * 2f, padding * 2f);

            // 3D bottles extend visually above their canvas RectTransform bounds
            // (the neck and cork peek above the canvas element area).  For bottle
            // targets, shift the spotlight centre upward so the highlight fully
            // surrounds the rendered bottle rather than cropping its top.
            var step = _stepIndex >= 0 && _stepIndex < _steps.Count ? _steps[_stepIndex] : null;
            bool isBottleTarget = step != null
                && step.TargetObjectName != null
                && step.TargetObjectName.StartsWith("Bottle", System.StringComparison.Ordinal);
            if (isBottleTarget)
                targetCenter.y += targetRect.size.y * 0.20f;

            if (!_hasSmoothState || !animated)
            {
                _smoothCenter = targetCenter;
                _smoothSize = targetSize;
                _hasSmoothState = true;
            }
            else
            {
                _smoothCenter = Vector2.SmoothDamp(_smoothCenter, targetCenter, ref _smoothCenterVelocity, 0.1f, Mathf.Infinity, Time.unscaledDeltaTime);
                _smoothSize = Vector2.SmoothDamp(_smoothSize, targetSize, ref _smoothSizeVelocity, 0.1f, Mathf.Infinity, Time.unscaledDeltaTime);
            }

            highlightFrame.anchoredPosition = _smoothCenter;
            highlightFrame.sizeDelta = _smoothSize;
            highlightFrame.gameObject.SetActive(true);
            highlightFrame.SetAsLastSibling();
            _lastFocusVisible = true;

            if (raycastBlocker != null)
            {
                raycastBlocker.Configure(_canvas, highlightFrame);
            }

            if (highlightMask != null)
            {
                bool hasMaskMaterial = highlightMask.material != null;
                highlightMask.gameObject.SetActive(hasMaskMaterial);
                if (hasMaskMaterial)
                {
                    UpdateMaskMaterial(canvasRect, _smoothCenter, _smoothSize);
                    // The spotlight shader draws the dark overlay with a transparent
                    // hole around the focus target.  If the dim layer is also active it
                    // darkens the entire canvas uniformly — including the hole — so the
                    // highlighted element appears just as dim as the rest of the screen.
                    // Hide the dim layer while the spotlight mask is active so that only
                    // the area OUTSIDE the hole is darkened and the target stays bright.
                    if (dimLayer != null) dimLayer.gameObject.SetActive(false);
                }
                else
                {
                    // No spotlight shader available (e.g. shader not found at runtime).
                    // Fall back to the plain dim layer for uniform darkening.
                    if (dimLayer != null) dimLayer.gameObject.SetActive(true);
                }
            }

            CacheHighlightFrameVisuals();

            if (root != null)
            {
                root.SetAsLastSibling();
            }
        }

        private void CacheHighlightFrameVisuals()
        {
            if (highlightFrame == null)
            {
                _highlightFrameImage = null;
                _highlightFrameShadow = null;
                return;
            }

            if (_highlightFrameImage == null)
            {
                _highlightFrameImage = highlightFrame.GetComponent<Image>();
            }

            if (_highlightFrameShadow == null)
            {
                _highlightFrameShadow = highlightFrame.GetComponent<Shadow>();
            }
        }

        private void AnimateHighlightFrame(float time)
        {
            if (!_running || !_lastFocusVisible)
            {
                return;
            }

            CacheHighlightFrameVisuals();
            float wave = 0.5f + 0.5f * Mathf.Sin(time * 3.4f + 0.4f);

            if (_highlightFrameImage != null)
            {
                _highlightFrameImage.color = new Color(
                    0.78f,
                    0.9f,
                    1f,
                    Mathf.Lerp(0.16f, 0.28f, wave));
            }

            if (_highlightFrameShadow != null)
            {
                _highlightFrameShadow.effectColor = new Color(
                    0.58f,
                    0.78f,
                    1f,
                    Mathf.Lerp(0.26f, 0.48f, wave));
                _highlightFrameShadow.effectDistance = Vector2.Lerp(new Vector2(0f, -10f), new Vector2(0f, -18f), wave);
            }
        }

        private bool TryCalculateTargetRectInCanvasSpace(RectTransform canvasRect, RectTransform target, out Rect rect)
        {
            rect = default;
            if (canvasRect == null || target == null)
            {
                return false;
            }

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, target);
            if (bounds.size.x > 0.01f && bounds.size.y > 0.01f)
            {
                rect = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
                return true;
            }

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < corners.Length; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    RectTransformUtility.WorldToScreenPoint(null, corners[i]),
                    null,
                    out var localPoint);

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            if (max.x <= min.x || max.y <= min.y)
            {
                return false;
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void UpdateMaskMaterial(RectTransform canvasRect, Vector2 center, Vector2 size)
        {
            if (highlightMask == null)
            {
                return;
            }

            Material material = highlightMask.material;
            if (material == null)
            {
                return;
            }

            Rect rect = canvasRect.rect;
            Vector2 uvCenter = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, center.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, center.y));

            Vector2 uvSize = new Vector2(
                Mathf.Clamp01(size.x / Mathf.Max(1f, rect.width)),
                Mathf.Clamp01(size.y / Mathf.Max(1f, rect.height)));

            material.SetVector("_HoleCenter", new Vector4(uvCenter.x, uvCenter.y, 0f, 0f));
            material.SetVector("_HoleSize", new Vector4(uvSize.x, uvSize.y, 0f, 0f));
            material.SetFloat("_UseCircle", 0f);
            material.SetFloat("_CornerRadius", 0.06f);
            material.SetFloat("_Feather", 0.03f);
        }

        private void NextStep()
        {
            if (!_running) return;
            _stepIndex++;
            if (_stepIndex >= _steps.Count)
            {
                CompleteTutorial();
                return;
            }

            ShowStep(_stepIndex);
        }

        private void SkipTutorial()
        {
            if (!_running) return;
            CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            _running = false;
            if (_settingsStore != null)
            {
                _settingsStore.SaveTutorialCompleted(true);
            }

            HideImmediate();
        }

        private void HideImmediate()
        {
            _activePulse?.Dispose();
            _activePulse = null;

            if (highlightFrame != null)
            {
                highlightFrame.gameObject.SetActive(false);
            }

            if (highlightMask != null)
            {
                highlightMask.gameObject.SetActive(false);
            }

            if (dimLayer != null)
            {
                dimLayer.gameObject.SetActive(false);
            }

            if (root != null)
            {
                root.gameObject.SetActive(false);
            }

            _lastFocusVisible = false;

            if (_highlightFrameImage != null)
            {
                _highlightFrameImage.color = new Color(0.78f, 0.9f, 1f, 0.2f);
            }

            if (_highlightFrameShadow != null)
            {
                _highlightFrameShadow.effectColor = new Color(0.58f, 0.78f, 1f, 0.42f);
                _highlightFrameShadow.effectDistance = new Vector2(0f, -14f);
            }
        }
    }

    [RequireComponent(typeof(RectTransform))]
    public sealed class TutorialRaycastBlocker : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private RectTransform passThroughRect;

        private Canvas _canvas;

        public void Configure(Canvas canvas, RectTransform focusedRect)
        {
            _canvas = canvas;
            passThroughRect = focusedRect;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled)
            {
                return true;
            }

            if (passThroughRect == null || _canvas == null)
            {
                return true;
            }

            Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (_canvas.worldCamera != null ? _canvas.worldCamera : eventCamera);
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(passThroughRect, screenPoint, camera);
            return !inside;
        }
    }

    public sealed class TutorialFocusPulse
    {
        private readonly RectTransform _target;
        private readonly Vector3 _baseScale;
        private readonly Shadow _shadow;
        private readonly bool _shadowCreatedByUs;
        private readonly Color _baseShadowColor;
        private readonly Vector2 _baseShadowDistance;
        private readonly bool _baseUseGraphicAlpha;
        private readonly Graphic[] _graphics;
        private readonly Color[] _baseGraphicColors;
        private readonly SpriteRenderer[] _spriteRenderers;
        private readonly Color[] _baseSpriteColors;

        public TutorialFocusPulse(RectTransform target)
        {
            _target = target;
            _baseScale = target.localScale;
            _shadow = target.GetComponent<Shadow>();
            _shadowCreatedByUs = _shadow == null;
            if (_shadowCreatedByUs)
            {
                _shadow = target.gameObject.AddComponent<Shadow>();
            }
            _baseShadowColor = _shadow.effectColor;
            _baseShadowDistance = _shadow.effectDistance;
            _baseUseGraphicAlpha = _shadow.useGraphicAlpha;
            _shadow.useGraphicAlpha = true;

            _graphics = target.GetComponentsInChildren<Graphic>(includeInactive: false);
            _baseGraphicColors = new Color[_graphics.Length];
            for (int i = 0; i < _graphics.Length; i++)
            {
                _baseGraphicColors[i] = _graphics[i] != null ? _graphics[i].color : Color.white;
            }

            _spriteRenderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            _baseSpriteColors = new Color[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                _baseSpriteColors[i] = _spriteRenderers[i] != null ? _spriteRenderers[i].color : Color.white;
            }
        }

        public void Tick(float time)
        {
            if (_target == null)
            {
                return;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            float scale = Mathf.Lerp(1.03f, 1.06f, wave);
            _target.localScale = _baseScale * scale;
            float brightness = Mathf.Lerp(1.0f, 1.1f, wave);

            for (int i = 0; i < _graphics.Length; i++)
            {
                Graphic graphic = _graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color baseColor = _baseGraphicColors[i];
                graphic.color = new Color(
                    Mathf.Clamp01(baseColor.r * brightness),
                    Mathf.Clamp01(baseColor.g * brightness),
                    Mathf.Clamp01(baseColor.b * brightness),
                    baseColor.a);
            }

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color baseColor = _baseSpriteColors[i];
                spriteRenderer.color = new Color(
                    Mathf.Clamp01(baseColor.r * brightness),
                    Mathf.Clamp01(baseColor.g * brightness),
                    Mathf.Clamp01(baseColor.b * brightness),
                    baseColor.a);
            }

            Color glow = new Color(0.85f, 0.93f, 1f, Mathf.Lerp(0.18f, 0.3f, wave));
            _shadow.effectColor = glow;
            _shadow.effectDistance = Vector2.Lerp(new Vector2(0f, -6f), new Vector2(0f, -10f), wave);
        }

        public void Dispose()
        {
            if (_target != null)
            {
                _target.localScale = _baseScale;
            }

            if (_shadow != null)
            {
                if (_shadowCreatedByUs)
                {
                    Object.Destroy(_shadow);
                }
                else
                {
                    _shadow.effectColor = _baseShadowColor;
                    _shadow.effectDistance = _baseShadowDistance;
                    _shadow.useGraphicAlpha = _baseUseGraphicAlpha;
                }
            }

            for (int i = 0; i < _graphics.Length; i++)
            {
                if (_graphics[i] != null)
                {
                    _graphics[i].color = _baseGraphicColors[i];
                }
            }

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _spriteRenderers[i].color = _baseSpriteColors[i];
                }
            }
        }
    }
}
