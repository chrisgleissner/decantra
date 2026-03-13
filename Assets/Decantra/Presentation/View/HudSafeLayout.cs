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
        public static bool ForceLegacyThreeRowCaptureLayout { get; set; }

        private const int GridRows = 3;
        private const int TwoRowBottleThreshold = 6;
        private const int ExternalGridPadding = 0;

        // Minimum guaranteed pixel clearance between the ACTUAL bottle top and the HUD bar
        // (and between adjacent bottles).  This is expressed in canvas/reference pixels.
        private const float MinClearancePx = 48f;

        // The 3D bottle mesh is not vertically centred in its canvas cell.  Its local
        // origin is placed at the cell centre but the mesh top is at y = +1.735 (local),
        // so it extends above the cell top by:
        //   overhang = yMax * CellFitFraction / MeshFullHeight  -  0.5
        //            = 1.735 * 0.9 / 2.535  -  0.5  =  0.1162
        // This fraction must be subtracted from the raw gap to get the true HUD clearance.
        // Derivation: clearance = idealGap - BottleTopOverhang * cellH >= MinClearancePx
        //   with idealGap = (available - rows*cellH)/(rows+1):
        //   => cellH <= (available - (rows+1)*MinClearancePx) / (rows + (rows+1)*BottleTopOverhang)
        private const float BottleTopOverhang = 0.1162f;
        private const float ThreeRowInnerGapReductionPx = 56f;
        private const float ThreeRowTopGapBiasPx = 6f;
        private const float ThreeRowBottomGapReductionPx = 28f;
        private const float MinimumEdgeGapPx = MinClearancePx * 0.5f;
        private const float MinimumThreeRowBottomGapPx = 18f;
        private const float ThreeRowBottleFillRatio = 0.85f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float LayoutAssertTolerance = 0.5f;
#endif
        [SerializeField] private RectTransform topHud;
        [SerializeField] private RectTransform secondaryHud;
        [SerializeField] private RectTransform brandLockup;
        [SerializeField] private RectTransform bottomHud;
        [SerializeField] private RectTransform bottleArea;
        [SerializeField] private RectTransform bottleGrid;
        [SerializeField] private GridLayoutGroup bottleGridLayout;
        [SerializeField] private float topPadding = 0f;
        [SerializeField] private float bottomPadding = 0f;

        private RectTransform _root;
        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _lastScreenSize;
        private bool _dirty = true;
        private int _lastActiveBottleCount = -1;
        private bool _gridLayoutCached;
        private Vector2 _baseGridSpacing;
        private RectOffset _baseGridPadding;
        private Vector2 _baseGridSize;
        private Vector2 _baseGridCellSize;

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
            int activeCount = CountActiveBottles();

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
            float gridAnchoredY = 0f;
            if (bottleGridLayout != null && availableHeight > 0f)
            {
                int rows = ResolveGridRows();
                if (rows > 0)
                {
                    // Maximum cell height where the ACTUAL bottle top (which overshoots the
                    // cell top by BottleTopOverhang * cellH) stays MinClearancePx below the HUD.
                    // See BottleTopOverhang comment above for the derivation.
                    float maxFillCellHeight = (availableHeight - (rows + 1f) * MinClearancePx)
                                             / (rows + (rows + 1f) * BottleTopOverhang);
                    float baseCellH = _gridLayoutCached ? _baseGridCellSize.y : bottleGridLayout.cellSize.y;
                    float cellHeight = Mathf.Max(baseCellH, maxFillCellHeight);

                    // With the final cell height, compute equal gaps that exhaust available space.
                    float idealGap = (availableHeight - rows * cellHeight) / (rows + 1f);
                    if (idealGap >= MinimumEdgeGapPx)
                    {
                        float spacingY = idealGap;
                        float topPaddingY = idealGap;
                        float bottomPaddingY = idealGap;

                        if (rows == GridRows)
                        {
                            if (ForceLegacyThreeRowCaptureLayout)
                            {
                                // Screenshot pipeline uses this branch to capture the pre-refresh
                                // three-row layout as an explicit before image.
                                spacingY = Mathf.Max(0f, idealGap - ThreeRowInnerGapReductionPx);
                                bottomPaddingY = Mathf.Max(MinimumThreeRowBottomGapPx, idealGap - ThreeRowBottomGapReductionPx);
                                topPaddingY = Mathf.Max(MinimumEdgeGapPx + ThreeRowTopGapBiasPx, idealGap + ThreeRowTopGapBiasPx);

                                float compactedCellHeight = (availableHeight - topPaddingY - bottomPaddingY - (rows - 1f) * spacingY) / rows;
                                float maxCellHeightForHudClearance = (topPaddingY - MinClearancePx) / BottleTopOverhang;
                                compactedCellHeight = Mathf.Min(compactedCellHeight, maxCellHeightForHudClearance);
                                cellHeight = Mathf.Max(cellHeight, compactedCellHeight);
                            }
                            else
                            {
                                ApplyTopAnchoredThreeRowLayout(
                                    availableHeight,
                                    ref cellHeight,
                                    out spacingY,
                                    out topPaddingY,
                                    out bottomPaddingY);
                            }
                        }

                        bottleGridLayout.cellSize = new Vector2(bottleGridLayout.cellSize.x, cellHeight);
                        bottleGridLayout.spacing = new Vector2(_baseGridSpacing.x, spacingY);
                        bottleGridLayout.padding = new RectOffset(
                            _baseGridPadding.left,
                            _baseGridPadding.right,
                            Mathf.RoundToInt(topPaddingY),
                            Mathf.RoundToInt(bottomPaddingY));
                        // sizeDelta spans the full available height so the grid fills the area.
                        bottleGrid.sizeDelta = new Vector2(
                            bottleGrid.sizeDelta.x,
                            rows * cellHeight + (rows - 1f) * spacingY + topPaddingY + bottomPaddingY);
                        gridHeight = bottleGrid.sizeDelta.y;
                        if (rows == GridRows && !ForceLegacyThreeRowCaptureLayout)
                        {
                            gridAnchoredY = Mathf.Max(0f, (availableHeight - gridHeight) * 0.5f);
                        }
                        appliedEqualGaps = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (!IsRunningUnityTests())
                        {
                            AssertGapModel(desiredTop, desiredBottom, rows, cellHeight, spacingY, topPaddingY, bottomPaddingY, topBottom);
                        }
#endif
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
            bottleGrid.anchoredPosition = new Vector2(0f, gridAnchoredY);
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottleGrid);
        }

        private float ResolveMaxBottleHeight()
        {
            if (bottleGridLayout != null && bottleGridLayout.cellSize.y > 0f)
            {
                return bottleGridLayout.cellSize.y;
            }

            if (_gridLayoutCached && _baseGridSize.y > 0f)
            {
                return _baseGridSize.y / GridRows;
            }

            return 0f;
        }

        private static void ApplyTopAnchoredThreeRowLayout(
            float availableHeight,
            ref float cellHeight,
            out float spacingY,
            out float topPaddingY,
            out float bottomPaddingY)
        {
            float topSafetyMargin = MinClearancePx;
            float bottomSafetyMargin = MinimumEdgeGapPx;
            float safePlayableHeight = Mathf.Max(0f, availableHeight - topSafetyMargin - bottomSafetyMargin);
            float preferredCellHeight = safePlayableHeight > 0f
                ? safePlayableHeight / GridRows * ThreeRowBottleFillRatio
                : 0f;
            float maxVerticalCellHeight = (availableHeight - topSafetyMargin - bottomSafetyMargin)
                / (GridRows + BottleTopOverhang);

            cellHeight = Mathf.Max(0f, Mathf.Min(preferredCellHeight, maxVerticalCellHeight));

            topPaddingY = topSafetyMargin + BottleTopOverhang * cellHeight;
            bottomPaddingY = bottomSafetyMargin;

            float usableHeight = Mathf.Max(0f, availableHeight - topPaddingY - bottomPaddingY - GridRows * cellHeight);
            spacingY = GridRows > 1
                ? Mathf.Max(0f, usableHeight / (GridRows - 1f))
                : 0f;
        }

        private int ResolveGridRows()
        {
            int activeBottleCount = CountActiveBottles();
            if (activeBottleCount <= 0)
            {
                // Keep canonical board height when no bottles are active (startup/transient state).
                return GridRows;
            }

            return activeBottleCount <= TwoRowBottleThreshold ? 2 : GridRows;
        }

        private int CountActiveBottles()
        {
            if (bottleGrid == null) return 0;
            int activeCount = 0;
            for (int i = 0; i < bottleGrid.childCount; i++)
            {
                if (bottleGrid.GetChild(i).gameObject.activeSelf)
                {
                    activeCount++;
                }
            }

            return activeCount;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void AssertGapModel(float screenTopY, float screenBottomY, int rows, float maxBottleHeight, float gapHeight, float topGapHeight, float bottomGapHeight, float hudBottomY)
        {
            Debug.Assert(gapHeight > 0f, $"HudSafeLayout gapHeight must be positive. gap={gapHeight}");
            Debug.Assert(topGapHeight > 0f, $"HudSafeLayout topGapHeight must be positive. gap={topGapHeight}");
            Debug.Assert(bottomGapHeight > 0f, $"HudSafeLayout bottomGapHeight must be positive. gap={bottomGapHeight}");

            float row1Top = screenTopY - topGapHeight;
            float currentTop = row1Top;
            float rowBottom = currentTop;
            for (int i = 0; i < rows; i++)
            {
                rowBottom = currentTop - maxBottleHeight;
                currentTop = rowBottom - gapHeight;
            }

            float bottomGap = rowBottom - screenBottomY;

            Debug.Assert(Mathf.Abs(bottomGap - bottomGapHeight) <= LayoutAssertTolerance,
                $"HudSafeLayout bottom gap mismatch. expected={bottomGapHeight} actual={bottomGap}");

            Debug.Assert(row1Top < hudBottomY - LayoutAssertTolerance,
                $"HudSafeLayout row1 touches/intersects HUD. row1Top={row1Top}, hudBottom={hudBottomY}");

            if (bottleGrid == null || _root == null) return;
            for (int i = 0; i < bottleGrid.childCount; i++)
            {
                var child = bottleGrid.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;

                var canvasGroup = child.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha <= 0.001f)
                    continue;

                child.GetWorldCorners(_corners);
                float childTop = float.NegativeInfinity;
                for (int c = 0; c < _corners.Length; c++)
                {
                    var local = _root.InverseTransformPoint(_corners[c]);
                    childTop = Mathf.Max(childTop, local.y);
                }

                Debug.Assert(childTop <= hudBottomY - LayoutAssertTolerance,
                    $"HudSafeLayout bottle intersects HUD. top={childTop}, hudBottom={hudBottomY}");
            }
        }
#endif

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
                _baseGridCellSize = bottleGridLayout.cellSize;
            }
        }

        private static bool IsRunningUnityTests()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-runTests", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void RestoreGridLayoutDefaults()
        {
            if (bottleGridLayout == null || !_gridLayoutCached) return;
            bottleGridLayout.spacing = _baseGridSpacing;
            bottleGridLayout.cellSize = _baseGridCellSize;
            bottleGridLayout.padding = new RectOffset(
                _baseGridPadding.left,
                _baseGridPadding.right,
                _baseGridPadding.top,
                _baseGridPadding.bottom);
            bottleGrid.sizeDelta = _baseGridSize;
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
