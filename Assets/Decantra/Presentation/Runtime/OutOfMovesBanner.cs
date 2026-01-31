/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class OutOfMovesBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text messageText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float enterDuration = 0.5f;
        [SerializeField] private float holdDuration = 1.0f;
        [SerializeField] private float exitDuration = 0.5f;

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.01f;

        public void Show(string message, bool sfxEnabled, Action onComplete)
        {
            if (panel == null || messageText == null || canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAllCoroutines();
            messageText.text = message;
            StartCoroutine(Animate(onComplete));
        }

        private IEnumerator Animate(Action onComplete)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            panel.anchoredPosition = new Vector2(0, -500);

            float time = 0f;
            while (time < enterDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / enterDuration);
                canvasGroup.alpha = t;
                panel.anchoredPosition = Vector2.Lerp(new Vector2(0, -500), Vector2.zero, t);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            panel.anchoredPosition = Vector2.zero;
            if (holdDuration > 0f)
            {
                yield return new WaitForSeconds(holdDuration);
            }

            time = 0f;
            while (time < exitDuration)
            {
                time += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, time / exitDuration);
                canvasGroup.alpha = 1f - t;
                panel.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0, 400), t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            onComplete?.Invoke();
        }
    }
}
