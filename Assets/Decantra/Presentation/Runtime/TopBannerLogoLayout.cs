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
        [SerializeField] private RectTransform resetButtonRect;

        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _parent;
        private bool _dirty = true;
        private float _lastMinX;
        private float _lastMaxX;
        private float _lastMinY;
        private float _lastMaxY;
        private bool _hasBounds;

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
                if (TryUpdateBounds(out float minX, out float maxX, out float minY, out float maxY))
                {
                    if (!_hasBounds ||
                        Mathf.Abs(minX - _lastMinX) > 0.01f ||
                        Mathf.Abs(maxX - _lastMaxX) > 0.01f ||
                        Mathf.Abs(minY - _lastMinY) > 0.01f ||
                        Mathf.Abs(maxY - _lastMaxY) > 0.01f)
                    {
                        _dirty = true;
                        _lastMinX = minX;
                        _lastMaxX = maxX;
                        _lastMinY = minY;
                        _lastMaxY = maxY;
                        _hasBounds = true;
                    }
                }
            }

            if (_dirty)
            {
                _dirty = false;
                ApplyLayout();
            }
        }

        public void ForceLayout()
        {
            _dirty = true;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (logoRect == null || logoImage == null || _parent == null || buttonRects == null || buttonRects.Length == 0)
            {
                return;
            }
            if (!TryUpdateBounds(out float minX, out float maxX, out float minY, out float maxY))
            {
                return;
            }

            _lastMinX = minX;
            _lastMaxX = maxX;
            _lastMinY = minY;
            _lastMaxY = maxY;
            _hasBounds = true;

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

            float scaledWidth = width * 1.03f;
            float aspect = sprite.rect.height / sprite.rect.width;
            float height = scaledWidth * aspect;

            float gapBelow = 0f;
            if (resetButtonRect != null)
            {
                resetButtonRect.GetWorldCorners(_corners);
                float resetMaxY = float.MinValue;
                for (int c = 0; c < _corners.Length; c++)
                {
                    var local = _parent.InverseTransformPoint(_corners[c]);
                    resetMaxY = Mathf.Max(resetMaxY, local.y);
                }

                gapBelow = Mathf.Max(0f, minY - resetMaxY);
            }

            float gapAbove = gapBelow;
            float logoBottom = maxY + gapAbove;
            float logoTop = logoBottom + height;

            float targetX = (minX + maxX) * 0.5f;
            float targetY = logoTop;

            var parentRect = _parent.rect;
            var anchorMin = logoRect.anchorMin;
            var anchorLocal = new Vector2(
                parentRect.xMin + parentRect.width * anchorMin.x,
                parentRect.yMin + parentRect.height * anchorMin.y);

            var pos = logoRect.anchoredPosition;
            pos.x = targetX - anchorLocal.x;
            pos.y = targetY - anchorLocal.y;
            logoRect.anchoredPosition = pos;
            logoRect.sizeDelta = new Vector2(scaledWidth, height);
        }

        private bool TryUpdateBounds(out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minY = float.MaxValue;
            maxY = float.MinValue;
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
                    minY = Mathf.Min(minY, local.y);
                    maxY = Mathf.Max(maxY, local.y);
                    found = true;
                }
            }

            return found;
        }
    }
}
