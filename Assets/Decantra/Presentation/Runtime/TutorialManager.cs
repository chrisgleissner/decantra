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
        private TutorialStepData _activeStep;
        private TutorialFocusPulse _activePulse;

        private Vector2 _smoothCenter;
        private Vector2 _smoothCenterVelocity;
        private Vector2 _smoothSize;
        private Vector2 _smoothSizeVelocity;
        private bool _hasSmoothState;

        public bool IsRunning => _running;
        public int StepCount => _steps.Count;
        public int CurrentStepIndex => _stepIndex;

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

        public bool TryGetCurrentStepSnapshot(out int stepIndex, out string targetName)
        {
            stepIndex = _stepIndex;
            targetName = _activeStep?.TargetObjectName;
            return _running && _activeStep != null;
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

        private void LateUpdate()
        {
            if (!_running)
            {
                return;
            }

            RefreshHighlight(animated: true);
            _activePulse?.Tick(Time.unscaledTime);
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

            ShowStep(_stepIndex);
        }

        private void BuildSteps()
        {
            _steps.Clear();
            _steps.Add(new TutorialStepData(
                "highlight-bottles",
                "Bottle_1",
                "These are your bottles. To pour, press and drag a bottle.",
                optional: false,
                focusShape: TutorialFocusShape.Circle
            ));
            _steps.Add(new TutorialStepData(
                "drag-pour",
                "Bottle_2",
                "Drag a bottle onto another and release. It pours only if space is available and the top color matches or the target is empty.",
                optional: false,
                focusShape: TutorialFocusShape.Circle
            ));

            if (_controller != null && _controller.TryGetSinkBottleObjectName(out string sinkBottleName))
            {
                _steps.Add(new TutorialStepData(
                    "sink-only",
                    sinkBottleName,
                    "Black bottles use heavier, darker glass. They can receive liquid but cannot be picked as sources.",
                    optional: false,
                    focusShape: TutorialFocusShape.Circle
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
                "logo",
                "BrandLockup",
                "The Decantra logo anchors your active game session and HUD state."
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
            _activeStep = step;
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
                return;
            }

            var corners = new Vector3[4];
            _activeTarget.GetWorldCorners(corners);
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

            float padding = 18f;
            var targetCenter = (min + max) * 0.5f;
            var targetSize = (max - min) + new Vector2(padding * 2f, padding * 2f);

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

            if (raycastBlocker != null)
            {
                raycastBlocker.Configure(_canvas, highlightFrame);
            }

            if (highlightMask != null)
            {
                highlightMask.gameObject.SetActive(true);
                UpdateMaskMaterial(canvasRect, _smoothCenter, _smoothSize, _activeStep != null && _activeStep.FocusShape == TutorialFocusShape.Circle);
            }

            if (root != null)
            {
                root.SetAsLastSibling();
            }
        }

        private void UpdateMaskMaterial(RectTransform canvasRect, Vector2 center, Vector2 size, bool useCircle)
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
            material.SetFloat("_UseCircle", useCircle ? 1f : 0f);
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

            bool inside = RectTransformUtility.RectangleContainsScreenPoint(passThroughRect, screenPoint, eventCamera);
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
        }
    }
}
