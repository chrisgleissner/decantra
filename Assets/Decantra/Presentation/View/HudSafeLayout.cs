/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Decantra.Presentation.View
{
    /// <summary>
    /// Ensures HUD and bottle layout respect safe areas and avoid overlaps on modern devices.
    /// </summary>
    public sealed class HudSafeLayout : MonoBehaviour
    {
        private const float TopRowsDownwardOffsetPx = 65f;
        private const float TopRowsDownwardReferenceHeightPx = 2400f;
        private const float TopHudClearanceExtraPx = 30f;
        private const int ShiftedTopRowCount = 2;
        private const float RowAnchoredMergeTolerance = 8f;

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
        private readonly Vector3[] _childCorners = new Vector3[4];
        private Vector2 _lastScreenSize;
        private bool _dirty = true;
        private int _lastActiveBottleCount = -1;
        private bool _gridLayoutCached;
        private Vector2 _baseGridSpacing;
        private RectOffset _baseGridPadding;
        private Vector2 _baseGridSize;

        public void MarkLayoutDirty()
        {
            _dirty = true;
        }

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

        private void OnDisable()
        {
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
            changed |= HasBottleActivationChanged();

            _dirty = changed;
            return changed;
        }

        private bool HasBottleActivationChanged()
        {
            if (bottleGrid == null) return false;

            int activeCount = 0;
            for (int i = 0; i < bottleGrid.childCount; i++)
            {
                if (bottleGrid.GetChild(i).gameObject.activeSelf)
                {
                    activeCount++;
                }
            }

            if (activeCount == _lastActiveBottleCount)
            {
                return false;
            }

            _lastActiveBottleCount = activeCount;
            return true;
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

            float desiredTop = topBottom - topPadding - TopHudClearanceExtraPx;
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
                float scaleGridH = _gridLayoutCached ? _baseGridSize.y : gridHeight;
                float scaleGridW = _gridLayoutCached ? _baseGridSize.x : bottleGrid.rect.width;
                float heightScale = scaleGridH > 0f && availableHeight > 0f ? availableHeight / scaleGridH : 1f;
                float widthScale = scaleGridW > 0f && rootRect.width > 0f ? rootRect.width / scaleGridW : 1f;
                scale = Mathf.Min(1f, Mathf.Min(heightScale, widthScale));
            }

            bottleGrid.localScale = new Vector3(scale, scale, 1f);
            bottleGrid.anchoredPosition = Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottleGrid);
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

        private void ApplyTopRowsDownwardOffset()
        {
            if (bottleGrid == null || secondaryHud == null) return;
            if (bottleGridLayout != null && !bottleGridLayout.enabled) return;
            var rows = new List<RowInfo>(3);
            for (int i = 0; i < bottleGrid.childCount; i++)
            {
                if (!(bottleGrid.GetChild(i) is RectTransform childRect)) continue;
                if (!childRect.gameObject.activeSelf) continue;
                AddChildToRows(rows, childRect);
            }

            if (rows.Count < 2) return;
            // Row order follows anchored Y so all bottles in the same visual grid row
            // stay grouped even when their rendered heights differ by capacity.
            rows.Sort((a, b) => b.RowY.CompareTo(a.RowY));

            int maxShiftedRow = Mathf.Min(ShiftedTopRowCount, rows.Count) - 1;
            if (maxShiftedRow < 0) return;

            float screenHeight = Mathf.Max(1f, Screen.height);
            float offset = TopRowsDownwardOffsetPx * (screenHeight / TopRowsDownwardReferenceHeightPx);
            if (offset <= 0f) return;

            for (int rowIndex = 0; rowIndex <= maxShiftedRow; rowIndex++)
            {
                var row = rows[rowIndex];
                for (int childIndex = 0; childIndex < row.Children.Count; childIndex++)
                {
                    var child = row.Children[childIndex];
                    var anchored = child.anchoredPosition;
                    child.anchoredPosition = new Vector2(anchored.x, anchored.y - offset);
                }
            }
        }

        private void AddChildToRows(List<RowInfo> rows, RectTransform childRect)
        {
            childRect.GetWorldCorners(_childCorners);
            float top = float.MinValue;
            float bottom = float.MaxValue;
            for (int i = 0; i < _childCorners.Length; i++)
            {
                var local = bottleGrid.InverseTransformPoint(_childCorners[i]);
                top = Mathf.Max(top, local.y);
                bottom = Mathf.Min(bottom, local.y);
            }

            float rowY = childRect.anchoredPosition.y;
            for (int i = 0; i < rows.Count; i++)
            {
                if (Mathf.Abs(rows[i].RowY - rowY) <= RowAnchoredMergeTolerance)
                {
                    rows[i].Children.Add(childRect);
                    rows[i].RowY = (rows[i].RowY * (rows[i].Children.Count - 1) + rowY) / rows[i].Children.Count;
                    rows[i].TopY = Mathf.Max(rows[i].TopY, top);
                    rows[i].BottomY = Mathf.Min(rows[i].BottomY, bottom);
                    rows[i].CenterY = (rows[i].TopY + rows[i].BottomY) * 0.5f;
                    return;
                }
            }

            rows.Add(new RowInfo(rowY, top, bottom, childRect));
        }

        private sealed class RowInfo
        {
            public float RowY;
            public float CenterY;
            public float TopY;
            public float BottomY;
            public readonly List<RectTransform> Children;

            public RowInfo(float rowY, float topY, float bottomY, RectTransform child)
            {
                RowY = rowY;
                CenterY = (topY + bottomY) * 0.5f;
                TopY = topY;
                BottomY = bottomY;
                Children = new List<RectTransform>(3) { child };
            }
        }
    }
}
