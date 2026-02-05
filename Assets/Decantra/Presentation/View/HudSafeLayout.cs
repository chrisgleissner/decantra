/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;

namespace Decantra.Presentation.View
{
    /// <summary>
    /// Ensures HUD and bottle layout respect safe areas and avoid overlaps on modern devices.
    /// </summary>
    public sealed class HudSafeLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform topHud;
        [SerializeField] private RectTransform secondaryHud;
        [SerializeField] private RectTransform brandLockup;
        [SerializeField] private RectTransform bottomHud;
        [SerializeField] private RectTransform bottleArea;
        [SerializeField] private RectTransform bottleGrid;
        [SerializeField] private float topPadding = 24f;
        [SerializeField] private float bottomPadding = 24f;

        private RectTransform _root;
        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _lastScreenSize;
        private bool _dirty = true;

        public void Configure(RectTransform topHudRect, RectTransform secondaryHudRect, RectTransform brandLockupRect,
            RectTransform bottomHudRect, RectTransform bottleAreaRect, RectTransform bottleGridRect,
            float topPaddingPx, float bottomPaddingPx)
        {
            topHud = topHudRect;
            secondaryHud = secondaryHudRect;
            brandLockup = brandLockupRect;
            bottomHud = bottomHudRect;
            bottleArea = bottleAreaRect;
            bottleGrid = bottleGridRect;
            topPadding = Mathf.Max(0f, topPaddingPx);
            bottomPadding = Mathf.Max(0f, bottomPaddingPx);

            _root = bottleArea != null ? bottleArea.parent as RectTransform : null;
            _dirty = true;
            ApplyLayout();
        }

        private void Awake()
        {
            _root = bottleArea != null ? bottleArea.parent as RectTransform : null;
        }

        private void OnEnable()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (CheckLayoutDirty() || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
            {
                _dirty = false;
                _lastScreenSize = new Vector2(Screen.width, Screen.height);
                ApplyLayout();
            }
        }

        private bool CheckLayoutDirty()
        {
            if (_dirty) return true;
            bool changed = false;

            changed |= HasChanged(topHud);
            changed |= HasChanged(secondaryHud);
            changed |= HasChanged(brandLockup);
            changed |= HasChanged(bottomHud);
            changed |= HasChanged(bottleArea);
            changed |= HasChanged(bottleGrid);

            _dirty = changed;
            return changed;
        }

        private static bool HasChanged(RectTransform rect)
        {
            if (rect == null) return false;
            bool changed = rect.hasChanged;
            rect.hasChanged = false;
            return changed;
        }

        private void ApplyLayout()
        {
            if (_root == null || bottleArea == null || bottleGrid == null || topHud == null || secondaryHud == null || bottomHud == null)
            {
                return;
            }

            float topBottom = Mathf.Min(GetMinY(topHud), GetMinY(secondaryHud));
            if (brandLockup != null)
            {
                topBottom = Mathf.Min(topBottom, GetMinY(brandLockup));
            }

            float bottomTop = GetMaxY(bottomHud);

            float desiredTop = topBottom - topPadding;
            float desiredBottom = bottomTop + bottomPadding;

            var rootRect = _root.rect;
            float topOffset = rootRect.yMax - desiredTop;
            float bottomOffset = desiredBottom - rootRect.yMin;

            topOffset = Mathf.Max(0f, topOffset);
            bottomOffset = Mathf.Max(0f, bottomOffset);

            bottleArea.anchorMin = new Vector2(0f, 0f);
            bottleArea.anchorMax = new Vector2(1f, 1f);
            bottleArea.offsetMin = new Vector2(0f, bottomOffset);
            bottleArea.offsetMax = new Vector2(0f, -topOffset);

            float availableHeight = desiredTop - desiredBottom;
            float gridHeight = bottleGrid.rect.height;
            float scale = 1f;
            if (availableHeight > 0f && gridHeight > 0f)
            {
                scale = Mathf.Min(1f, availableHeight / gridHeight);
            }

            bottleGrid.localScale = new Vector3(scale, scale, 1f);
            bottleGrid.anchoredPosition = Vector2.zero;
        }

        private float GetMinY(RectTransform rect)
        {
            rect.GetWorldCorners(_corners);
            float min = float.MaxValue;
            for (int i = 0; i < _corners.Length; i++)
            {
                var local = _root.InverseTransformPoint(_corners[i]);
                min = Mathf.Min(min, local.y);
            }
            return min;
        }

        private float GetMaxY(RectTransform rect)
        {
            rect.GetWorldCorners(_corners);
            float max = float.MinValue;
            for (int i = 0; i < _corners.Length; i++)
            {
                var local = _root.InverseTransformPoint(_corners[i]);
                max = Mathf.Max(max, local.y);
            }
            return max;
        }
    }
}
