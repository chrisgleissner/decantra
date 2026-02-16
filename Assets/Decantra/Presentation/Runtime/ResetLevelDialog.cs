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
    public sealed class ResetLevelDialog : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text messageText;
        [SerializeField] private Button noButton;
        [SerializeField] private Button yesButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private Action _onCancel;
        private Action _onConfirm;
        private bool _initialized;

        public bool IsVisible => canvasGroup != null && canvasGroup.blocksRaycasts;

        public void Show(Action onConfirm, Action onCancel)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            if (messageText != null)
            {
                messageText.text = "This will reset the current level and lose unsaved moves.";
            }

            if (canvasGroup == null || panel == null)
            {
                _onConfirm?.Invoke();
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
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (noButton != null)
            {
                noButton.onClick.RemoveAllListeners();
                noButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onCancel?.Invoke();
                });
            }

            if (yesButton != null)
            {
                yesButton.onClick.RemoveAllListeners();
                yesButton.onClick.AddListener(() =>
                {
                    Hide();
                    _onConfirm?.Invoke();
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
