/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class StarTradeInDialog : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text currentStarsText;
        [SerializeField] private Text messageText;
        [SerializeField] private Button convertButton;
        [SerializeField] private Text convertLabel;
        [SerializeField] private Button autoSolveButton;
        [SerializeField] private Text autoSolveLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onClose;
        private Action _pendingConfirmAction;
        private bool _initialized;

        public bool IsVisible => canvasGroup != null && canvasGroup.blocksRaycasts;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onClose?.Invoke();
                });
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() =>
                {
                    var pending = _pendingConfirmAction;
                    _pendingConfirmAction = null;
                    if (pending != null)
                    {
                        pending.Invoke();
                    }

                    Hide();
                });
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(ResetConfirmation);
            }

            Hide();
        }

        public void Show(
            int currentStars,
            int convertCost,
            bool canConvert,
            int autoSolveCost,
            bool canAutoSolve,
            Action onConvert,
            Action onAutoSolve,
            Action onClose)
        {
            _onClose = onClose;
            _pendingConfirmAction = null;

            if (currentStarsText != null)
            {
                currentStarsText.text = $"Current Stars: {Mathf.Max(0, currentStars)}";
            }

            if (convertLabel != null)
            {
                convertLabel.text = $"Convert All Sinks ({convertCost})";
            }

            if (autoSolveLabel != null)
            {
                autoSolveLabel.text = $"Auto-Solve Level ({autoSolveCost})";
            }

            if (convertButton != null)
            {
                convertButton.interactable = canConvert;
                convertButton.onClick.RemoveAllListeners();
                convertButton.onClick.AddListener(() => BeginConfirm("Convert all sinks for 10 stars?", onConvert));
            }

            if (autoSolveButton != null)
            {
                autoSolveButton.interactable = canAutoSolve;
                autoSolveButton.onClick.RemoveAllListeners();
                autoSolveButton.onClick.AddListener(() => BeginConfirm($"Auto-solve this level for {autoSolveCost} stars?", onAutoSolve));
            }

            ResetConfirmation();

            if (canvasGroup == null || panel == null)
            {
                return;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            panel.anchoredPosition = Vector2.zero;
        }

        public void Hide()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            _pendingConfirmAction = null;
        }

        private void BeginConfirm(string message, Action confirmAction)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            _pendingConfirmAction = confirmAction;

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = _pendingConfirmAction != null;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(true);
            }
        }

        private void ResetConfirmation()
        {
            if (messageText != null)
            {
                messageText.text = "Choose an option below. Confirmation is required before spending stars.";
            }

            _pendingConfirmAction = null;

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = false;
            }

            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(true);
            }
        }

        private void Awake()
        {
            Initialize();
        }
    }
}
