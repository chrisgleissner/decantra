/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    public sealed class TopBannerLogoLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform logoRect;
        [SerializeField] private Image logoImage;
        [SerializeField] private RectTransform[] buttonRects;

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _parent;
        private bool _dirty = true;

        private void Awake()
        {
            if (logoRect == null)
            {
                logoRect = GetComponent<RectTransform>();
            }
            if (logoImage == null)
            {
                logoImage = GetComponent<Image>();
            }
            _parent = logoRect != null ? logoRect.parent as RectTransform : null;
        }

        private void OnEnable()
        {
            _dirty = true;
        }

        private void Start()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (logoRect == null || logoImage == null || _parent == null || buttonRects == null || buttonRects.Length == 0)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            bool found = false;

            for (int i = 0; i < buttonRects.Length; i++)
            {
                var rect = buttonRects[i];
                if (rect == null) continue;
                rect.GetWorldCorners(_corners);
                for (int c = 0; c < _corners.Length; c++)
                {
                    var local = _parent.InverseTransformPoint(_corners[c]);
                    minX = Mathf.Min(minX, local.x);
                    maxX = Mathf.Max(maxX, local.x);
                    found = true;
                }
            }

            if (!found)
            {
                return;
            }

            float width = maxX - minX;
            if (width <= 0f)
            {
                return;
            }

            var sprite = logoImage.sprite;
            if (sprite == null)
            {
                return;
            }

            float aspect = sprite.rect.height / sprite.rect.width;
            float height = width * aspect;

            var pos = logoRect.anchoredPosition;
            pos.x = (minX + maxX) * 0.5f;
            logoRect.anchoredPosition = pos;
            logoRect.sizeDelta = new Vector2(width, height);
        }
    }
}
