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

        public bool IsRunning => _running;

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

        private void LateUpdate()
        {
            if (!_running) return;
            RefreshHighlight();
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

            ShowStep(_stepIndex);
        }

        private void BuildSteps()
        {
            _steps.Clear();
            _steps.Add(new TutorialStepData(
                "highlight-bottles",
                "Bottle_1",
                "These are your bottles. To pour, press and drag a source bottle. Tapping does not pour."
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
                    "Dark-base sink bottles can receive liquid but cannot be picked as sources."
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
                "reset",
                "ResetButton",
                "Use RESET to restart only the current level."
            ));
            _steps.Add(new TutorialStepData(
                "options",
                "OptionsButton",
                "Open OPTIONS to replay this tutorial, adjust sound, modify video effects, and read the documentation."
            ));
            _steps.Add(new TutorialStepData(
                "stars",
                "StarsButton",
                "Earn stars by solving levels.\nTrade them for help when needed."
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

            if (instructionText != null)
            {
                instructionText.text = step.Instruction;
            }

            RefreshHighlight();
        }

        private RectTransform ResolveTarget(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return null;
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            return go.GetComponent<RectTransform>();
        }

        private void RefreshHighlight()
        {
            if (highlightFrame == null)
            {
                return;
            }

            if (_activeTarget == null)
            {
                highlightFrame.gameObject.SetActive(false);
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

            float padding = 20f;
            var size = (max - min) + new Vector2(padding * 2f, padding * 2f);
            highlightFrame.anchoredPosition = (min + max) * 0.5f;
            highlightFrame.sizeDelta = size;
            highlightFrame.gameObject.SetActive(true);
            highlightFrame.SetAsLastSibling();
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
            if (highlightFrame != null)
            {
                highlightFrame.gameObject.SetActive(false);
            }

            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }
    }
}
