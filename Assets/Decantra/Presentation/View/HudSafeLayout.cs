/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private GridLayoutGroup bottleGridLayout;
        [SerializeField] private float topPadding = 24f;
        [SerializeField] private float bottomPadding = 24f;

        private RectTransform _root;
        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _lastScreenSize;
        private bool _dirty = true;
        private bool _gridLayoutCached;
        private Vector2 _baseGridSpacing;
        private RectOffset _baseGridPadding;
        private Vector2 _baseGridSize;

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
            bottleGridLayout = bottleGrid != null ? bottleGrid.GetComponent<GridLayoutGroup>() : null;
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

            EnsureGridLayoutCache();

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

            bool appliedEqualGaps = false;
            if (bottleGridLayout != null && availableHeight > 0f)
            {
                int rows = ResolveGridRows();
                float cellHeight = bottleGridLayout.cellSize.y;
                if (rows > 0 && cellHeight > 0f)
                {
                    float idealGap = (availableHeight - (rows * cellHeight)) / (rows + 1f);
                    idealGap *= 2f;
                    if (idealGap > 0f)
                    {
                        int gapPx = Mathf.RoundToInt(idealGap);
                        bottleGridLayout.spacing = new Vector2(_baseGridSpacing.x, idealGap);
                        bottleGridLayout.padding = new RectOffset(
                            _baseGridPadding.left,
                            _baseGridPadding.right,
                            gapPx,
                            gapPx);
                        bottleGrid.sizeDelta = new Vector2(
                            bottleGrid.sizeDelta.x,
                            rows * cellHeight + (rows - 1f) * idealGap + 2f * idealGap);
                        gridHeight = bottleGrid.sizeDelta.y;
                        appliedEqualGaps = true;
                    }
                }
            }

            if (!appliedEqualGaps)
            {
                RestoreGridLayoutDefaults();
                if (availableHeight > 0f && gridHeight > 0f)
                {
                    scale = Mathf.Min(1f, availableHeight / gridHeight);
                }
            }

            bottleGrid.localScale = new Vector3(scale, scale, 1f);
            bottleGrid.anchoredPosition = Vector2.zero;
        }

        private void EnsureGridLayoutCache()
        {
            if (bottleGridLayout == null && bottleGrid != null)
            {
                bottleGridLayout = bottleGrid.GetComponent<GridLayoutGroup>();
            }

            if (bottleGridLayout != null && !_gridLayoutCached)
            {
                _gridLayoutCached = true;
                _baseGridSpacing = bottleGridLayout.spacing;
                _baseGridPadding = new RectOffset(
                    bottleGridLayout.padding.left,
                    bottleGridLayout.padding.right,
                    bottleGridLayout.padding.top,
                    bottleGridLayout.padding.bottom);
                _baseGridSize = bottleGrid.sizeDelta;
            }
        }

        private void RestoreGridLayoutDefaults()
        {
            if (bottleGridLayout == null || !_gridLayoutCached) return;
            bottleGridLayout.spacing = _baseGridSpacing;
            bottleGridLayout.padding = new RectOffset(
                _baseGridPadding.left,
                _baseGridPadding.right,
                _baseGridPadding.top,
                _baseGridPadding.bottom);
            bottleGrid.sizeDelta = _baseGridSize;
        }

        private int ResolveGridRows()
        {
            if (bottleGrid == null) return 3;
            int columns = 3;
            if (bottleGridLayout != null && bottleGridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = Mathf.Max(1, bottleGridLayout.constraintCount);
            }
            int childCount = bottleGrid.childCount;
            if (childCount <= 0) return 3;
            return Mathf.Max(1, Mathf.CeilToInt(childCount / (float)columns));
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
