/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Decantra.Presentation.View
{
    public sealed class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float holdSeconds = 6f;

        private Action _onLongPress;
        private Coroutine _holdRoutine;
        private bool _pressed;
        private bool _triggered;
        private Button _button;

        public void Configure(float seconds, Action onLongPress)
        {
            holdSeconds = Mathf.Max(0.1f, seconds);
            _onLongPress = onLongPress;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled) return;
            if (_holdRoutine != null)
            {
                StopCoroutine(_holdRoutine);
            }
            _pressed = true;
            _triggered = false;
            _holdRoutine = StartCoroutine(WaitForLongPress());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelHold();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelHold();
        }

        private IEnumerator WaitForLongPress()
        {
            float elapsed = 0f;
            while (_pressed && elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_pressed && !_triggered)
            {
                _triggered = true;
                if (_button != null)
                {
                    _button.interactable = false;
                }
                _onLongPress?.Invoke();
            }

            _holdRoutine = null;
        }

        private void CancelHold()
        {
            _pressed = false;
            if (_holdRoutine != null)
            {
                StopCoroutine(_holdRoutine);
                _holdRoutine = null;
            }
        }
    }
}
