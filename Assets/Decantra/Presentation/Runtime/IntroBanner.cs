/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class IntroBanner : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform logoRect;
        [SerializeField] private Image logoImage;
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup logoGroup;
        [SerializeField] private float enterDuration = 0.5f;
        [SerializeField] private float holdDuration = 1.0f;
        [SerializeField] private float exitDuration = 0.5f;

        private bool _dismissRequested;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        public void EnableScreenshotMode()
        {
            // Intentionally no-op: intro timing must remain exact for captures.
        }

        public float GetCaptureDelay()
        {
            return enterDuration + (holdDuration * 0.5f);
        }

        public void DismissEarly()
        {
            _dismissRequested = true;
        }

        public void PrepareForIntro()
        {
            EnsureReferences();
            ApplyLayout();
            _dismissRequested = false;
            SetBackgroundAlpha(1f);
            SetLogoAlpha(0f);
            SetLogoRaycasts(false);
        }

        public void HideImmediate()
        {
            EnsureReferences();
            _dismissRequested = false;
            _isPlaying = false;
            SetLogoAlpha(0f);
            SetBackgroundAlpha(0f);
            SetLogoRaycasts(false);
        }

        public IEnumerator Play()
        {
            EnsureReferences();
            if (logoRect == null || logoImage == null)
            {
                yield break;
            }
            _isPlaying = true;
            PrepareForIntro();

            float time = 0f;
            while (time < enterDuration && !_dismissRequested)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / enterDuration);
                SetLogoAlpha(t);
                yield return null;
            }

            SetLogoAlpha(1f);
            float held = 0f;
            while (!_dismissRequested && held < holdDuration)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            time = 0f;
            while (time < exitDuration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / exitDuration);
                SetLogoAlpha(1f - t);
                yield return null;
            }

            SetLogoAlpha(0f);
            SetBackgroundAlpha(0f);
            SetLogoRaycasts(false);
            _isPlaying = false;
        }

        private void EnsureReferences()
        {
            if (root == null)
            {
                root = GetComponent<RectTransform>();
            }
            if (logoRect == null && logoImage != null)
            {
                logoRect = logoImage.GetComponent<RectTransform>();
            }
        }

        private void ApplyLayout()
        {
            if (root == null || logoRect == null || logoImage == null || logoImage.sprite == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            float width = root.rect.width * 0.6f;
            float aspect = logoImage.sprite.rect.height / logoImage.sprite.rect.width;
            float height = width * aspect;

            logoRect.anchorMin = new Vector2(0.5f, 0.5f);
            logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = Vector2.zero;
            logoRect.sizeDelta = new Vector2(width, height);
        }

        private void SetBackgroundAlpha(float alpha)
        {
            if (background == null) return;
            var color = background.color;
            color.a = alpha;
            background.color = color;
        }

        private void SetLogoAlpha(float alpha)
        {
            if (logoGroup != null)
            {
                logoGroup.alpha = alpha;
                return;
            }

            if (logoImage == null) return;
            var color = logoImage.color;
            color.a = alpha;
            logoImage.color = color;
        }

        private void SetLogoRaycasts(bool enabled)
        {
            if (logoGroup == null) return;
            logoGroup.blocksRaycasts = enabled;
            logoGroup.interactable = enabled;
        }
    }
}
