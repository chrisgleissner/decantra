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
    public sealed class RestartGameDialog : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text messageText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private Action _onCancel;
        private Action _onRestart;
        private bool _initialized;

        public void Show(Action onRestart, Action onCancel)
        {
            _onRestart = onRestart;
            _onCancel = onCancel;
            if (panel == null || canvasGroup == null)
            {
                _onRestart?.Invoke();
                return;
            }

            if (messageText != null)
            {
                messageText.text = "This will start a new game from Level 1.\nCurrent score and stars will reset.\nHigh score and max level reached will be preserved.";
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
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onCancel?.Invoke();
                });
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onRestart?.Invoke();
                });
            }

            Hide();
        }

        private void Awake()
        {
            Hide();
        }
    }
}
