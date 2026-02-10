/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections;
using System.Collections.Generic;
using Decantra.Domain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation.View
{
    public sealed class BottleView : MonoBehaviour
    {
        private const float BaseHeightRatio = 0.3f;
        private const float BaseWidthRatio = 1.1f;

        // Reference bottle dimensions from SceneBootstrap (for the "default" bottle)
        private const float RefOutlineHeight = 372f;
        private const float RefSlotRootHeight = 300f;
        // Y threshold: elements with default Y above this are "top-fixed" (rim, neck, flange, etc.)
        // Elements at or below are "body" elements whose height stretches.
        private const float TopFixedThreshold = 120f;

        [SerializeField] private RectTransform slotRoot;
        [SerializeField] private List<Image> slots = new List<Image>();
        [SerializeField] private ColorPalette palette;
        [SerializeField] private Image outline;
        [SerializeField] private Image body;
        [SerializeField] private Image basePlate;
        [SerializeField] private Image stopper;
        [SerializeField] private Image glassBack;
        [SerializeField] private Image glassFront;
        [SerializeField] private Image rim;
        [SerializeField] private Image baseAccent;
        [SerializeField] private Image curvedHighlight;
        [SerializeField] private Image reflectionStrip;
        [SerializeField] private Image anchorCollar;
        [SerializeField] private Image normalShadow;
        [SerializeField] private Image liquidSurface;
        [SerializeField] private Sprite liquidSprite;

        private readonly List<int> segmentUnits = new List<int>();
        private readonly List<Image> incomingSlots = new List<Image>();
        private int previewPourCount;
        private Color outlineBaseColor;
        private Color outlineDefaultColor;
        private Color bodyDefaultColor;
        private Color baseDefaultColor;
        private Vector3 baseScale = Vector3.one;
        private Bottle lastBottle;
        private bool isSink;
        private Coroutine resistanceRoutine;
        private int _levelMaxCapacity = 4;
        private bool _originalLayoutCaptured;
        private readonly List<ChildLayoutInfo> _originalChildLayouts = new List<ChildLayoutInfo>();

        /// <summary>Cached original layout of a child RectTransform for capacity-based resizing.</summary>
        private struct ChildLayoutInfo
        {
            public RectTransform Rect;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public bool IsTopFixed; // true = above top-fixed threshold, position shifts but height unchanged
            public bool IsBody;     // true = stretchable body element (outline, glassBack, etc.)
        }

        public int Index { get; private set; }
        public bool IsSink => isSink;
        public RectTransform SlotRoot => slotRoot;

        /// <summary>
        /// Set by GameController at level load. Used to compute canonical liquid heights.
        /// </summary>
        public void SetLevelMaxCapacity(int maxCapacity)
        {
            _levelMaxCapacity = Mathf.Max(1, maxCapacity);
        }

        private void Awake()
        {
            if (outline != null)
            {
                outlineBaseColor = outline.color;
                outlineDefaultColor = outline.color;
            }
            if (body != null)
            {
                bodyDefaultColor = body.color;
            }
            if (basePlate != null)
            {
                baseDefaultColor = basePlate.color;
            }
            baseScale = transform.localScale;
            ConfigureGlassVisuals();
        }

        private void ConfigureGlassVisuals()
        {
            // Reduce overlay opacity to prevent color washout
            if (glassFront != null)
            {
                // Disable completely to verify
                glassFront.gameObject.SetActive(false);
            }

            if (reflectionStrip != null)
            {
                reflectionStrip.gameObject.SetActive(true);
                reflectionStrip.color = new Color(0.96f, 0.98f, 1f, 0.16f);
                reflectionStrip.raycastTarget = false;

                var rect = reflectionStrip.rectTransform;
                // Right-side reflection strip: center-anchored to match outline body scaling
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(24, 372);
                rect.anchoredPosition = new Vector2(62, -6);
            }

            if (glassBack != null)
            {
                var c = glassBack.color;
                glassBack.color = new Color(c.r, c.g, c.b, 0.05f);
                glassBack.raycastTarget = false;
            }
        }

        public void Initialize(int index)
        {
            Index = index;
        }

        public void Render(Bottle bottle)
        {
            if (bottle == null) return;
            lastBottle = bottle;
            if (slotRoot == null)
            {
                var found = transform.Find("LiquidRoot");
                if (found != null)
                {
                    slotRoot = found.GetComponent<RectTransform>();
                }
            }

            if (slotRoot == null) return;

            EnsureColorsInitialized();
            ApplyCapacityScale(bottle.Capacity);
            ApplySinkStyle(bottle);

            EnsureSlots(bottle.Capacity);

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            int refCap = _levelMaxCapacity;

            int segmentIndex = 0;
            segmentUnits.Clear();
            int unitsBefore = 0;

            int runCount = 0;
            ColorId? runColor = null;
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var color = bottle.Slots[i];
                if (!color.HasValue)
                {
                    if (runCount > 0)
                    {
                        RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, refCap, segmentUnits, unitsBefore);
                        unitsBefore += runCount;
                        runCount = 0;
                        runColor = null;
                    }
                    continue;
                }

                if (runCount == 0)
                {
                    runColor = color;
                    runCount = 1;
                }
                else if (runColor == color)
                {
                    runCount++;
                }
                else
                {
                    RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, refCap, segmentUnits, unitsBefore);
                    unitsBefore += runCount;
                    runColor = color;
                    runCount = 1;
                }
            }

            if (runCount > 0)
            {
                RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, refCap, segmentUnits, unitsBefore);
                unitsBefore += runCount;
            }

            for (int i = segmentIndex; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }

            ApplyPreview();
            UpdateLiquidSurfaceForFill(bottle.Count, bottle.TopColor);

            if (stopper != null)
            {
                bool isSealed = bottle.IsSolvedBottle();
                stopper.gameObject.SetActive(isSealed);
                if (isSealed && palette != null)
                {
                    var top = bottle.TopColor;
                    if (top.HasValue)
                    {
                        var color = palette.GetColor(top.Value);
                        stopper.color = Color.Lerp(color, Color.black, 0.25f);
                    }
                }
            }
        }

        public void SetOutlineColor(Color color)
        {
            if (outline == null) return;
            outlineBaseColor = color;
            outline.color = color;
        }

        public void SetHighlight(bool isHighlighted)
        {
            if (outline == null) return;
            outline.color = isHighlighted ? new Color(0.4f, 0.8f, 1f, 0.9f) : outlineBaseColor;
        }

        private void ApplyCapacityScale(int capacity)
        {
            // No transform scaling — all bottles have identical width and identical
            // top/bottom decorations. Only the liquid-holding body section varies.
            transform.localScale = baseScale;

            CaptureOriginalLayout();

            float ratio = _levelMaxCapacity > 0 ? (float)capacity / _levelMaxCapacity : 1f;
            // Full height reduction of the reference body (outline). Top-fixed elements
            // must shift down by this amount to track the new body top edge.
            float bodyHeightDelta = RefOutlineHeight * (1f - ratio);

            for (int i = 0; i < _originalChildLayouts.Count; i++)
            {
                var info = _originalChildLayouts[i];
                if (info.Rect == null) continue;

                if (info.IsTopFixed)
                {
                    // Shift top-fixed elements down by the FULL body height reduction
                    // so they track the new top of the body (bottom-anchored shrink).
                    info.Rect.anchoredPosition = new Vector2(
                        info.AnchoredPosition.x,
                        info.AnchoredPosition.y - bodyHeightDelta);
                }
                else if (info.IsBody)
                {
                    // Bottom-anchored shrink: each body element keeps its bottom edge
                    // fixed and shrinks upward. Center shifts by its own height delta.
                    float elementHalfDelta = info.SizeDelta.y * (1f - ratio) * 0.5f;
                    info.Rect.sizeDelta = new Vector2(
                        info.SizeDelta.x,
                        info.SizeDelta.y * ratio);
                    info.Rect.anchoredPosition = new Vector2(
                        info.AnchoredPosition.x,
                        info.AnchoredPosition.y - elementHalfDelta);
                }
                // Bottom elements (shadow, basePlate) stay at their original positions
            }

            // Resize slotRoot and its parent liquidMask proportionally (bottom-anchored)
            if (slotRoot != null)
            {
                slotRoot.sizeDelta = new Vector2(slotRoot.sizeDelta.x, RefSlotRootHeight * ratio);

                var liquidMask = slotRoot.parent as RectTransform;
                if (liquidMask != null)
                {
                    var origMask = FindOriginalLayout(liquidMask);
                    if (origMask.Rect != null)
                    {
                        float maskHalfDelta = origMask.SizeDelta.y * (1f - ratio) * 0.5f;
                        liquidMask.sizeDelta = new Vector2(origMask.SizeDelta.x, origMask.SizeDelta.y * ratio);
                        liquidMask.anchoredPosition = new Vector2(
                            origMask.AnchoredPosition.x,
                            origMask.AnchoredPosition.y - maskHalfDelta);
                    }
                }
            }
        }

        /// <summary>
        /// Captures the original layout of all child RectTransforms on first call.
        /// Classifies each as "top-fixed" (position shifts, height unchanged) or
        /// "body" (height scales with capacity ratio).
        /// </summary>
        private void CaptureOriginalLayout()
        {
            if (_originalLayoutCaptured) return;
            _originalLayoutCaptured = true;

            _originalChildLayouts.Clear();
            // Capture direct children of the bottle
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i) as RectTransform;
                if (child == null) continue;

                var info = new ChildLayoutInfo
                {
                    Rect = child,
                    AnchoredPosition = child.anchoredPosition,
                    SizeDelta = child.sizeDelta,
                    IsTopFixed = child.anchoredPosition.y > TopFixedThreshold,
                    IsBody = child.anchoredPosition.y <= TopFixedThreshold
                             && child.sizeDelta.y > 100f // Only tall elements are body
                };

                _originalChildLayouts.Add(info);
            }

            // Also capture the liquidMask (child of bottle, parent of slotRoot)
            if (slotRoot != null && slotRoot.parent != null && slotRoot.parent != transform)
            {
                var liquidMask = slotRoot.parent as RectTransform;
                if (liquidMask != null)
                {
                    _originalChildLayouts.Add(new ChildLayoutInfo
                    {
                        Rect = liquidMask,
                        AnchoredPosition = liquidMask.anchoredPosition,
                        SizeDelta = liquidMask.sizeDelta,
                        IsTopFixed = false,
                        IsBody = true
                    });
                }
            }
        }

        private ChildLayoutInfo FindOriginalLayout(RectTransform rect)
        {
            for (int i = 0; i < _originalChildLayouts.Count; i++)
            {
                if (_originalChildLayouts[i].Rect == rect)
                    return _originalChildLayouts[i];
            }
            return default;
        }

        private void ApplySinkStyle(Bottle bottle)
        {
            if (bottle == null) return;

            isSink = bottle.IsSink;

            // Sink marker: pure black for maximum visual distinction
            Color sinkMarkerColor = new Color(0f, 0f, 0f, 1f);

            if (bottle.IsSink)
            {
                if (basePlate != null)
                {
                    basePlate.gameObject.SetActive(true);
                    basePlate.color = sinkMarkerColor;
                }

                if (body != null)
                {
                    body.color = Color.Lerp(bodyDefaultColor, new Color(0.05f, 0.05f, 0.08f, bodyDefaultColor.a), 0.35f);
                }

                if (outline != null)
                {
                    var sinkOutline = Color.Lerp(outlineDefaultColor, Color.black, 0.2f);
                    outlineBaseColor = sinkOutline;
                    outline.color = sinkOutline;
                }

                if (anchorCollar != null)
                {
                    anchorCollar.gameObject.SetActive(true);
                    anchorCollar.color = sinkMarkerColor;
                    UpdateCollarLayout(bottle.Capacity);
                }

                if (normalShadow != null)
                {
                    normalShadow.gameObject.SetActive(false);
                }
            }
            else
            {
                if (basePlate != null)
                {
                    basePlate.gameObject.SetActive(false);
                }

                if (body != null)
                {
                    body.color = bodyDefaultColor;
                }

                if (outline != null)
                {
                    outlineBaseColor = outlineDefaultColor;
                    outline.color = outlineDefaultColor;
                }

                if (anchorCollar != null)
                {
                    anchorCollar.gameObject.SetActive(false);
                }

                // Subtle 3D shadow beneath the bottle base
                if (normalShadow != null && outline != null)
                {
                    normalShadow.gameObject.SetActive(true);
                    normalShadow.color = new Color(0f, 0f, 0f, 0.15f);
                    var shadowRect = normalShadow.rectTransform;
                    shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
                    shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
                    shadowRect.pivot = new Vector2(0.5f, 0.5f);
                    float outlineBottom = outline.rectTransform.anchoredPosition.y
                                          - outline.rectTransform.sizeDelta.y * 0.5f;
                    shadowRect.sizeDelta = new Vector2(130f, 20f);
                    shadowRect.anchoredPosition = new Vector2(0f, outlineBottom);
                }
            }

            UpdateBaseLayout();
        }

        /// <summary>
        /// Positions the anchorCollar (marker band) so that it is vertically centered
        /// within the first slot of liquid. When 1 unit of liquid is poured in, the
        /// liquid above and below the band will have equal height.
        /// Re-parents the collar into slotRoot and places it last in sibling order
        /// so it renders on top of any liquid (it represents a marking in the glass).
        /// </summary>
        private void UpdateCollarLayout(int capacity)
        {
            if (anchorCollar == null || slotRoot == null) return;

            // Re-parent into slotRoot if not already there, so coordinates match liquid
            if (anchorCollar.rectTransform.parent != slotRoot)
            {
                anchorCollar.rectTransform.SetParent(slotRoot, false);
            }

            // Ensure collar renders on top of liquid segments (glass marking)
            anchorCollar.rectTransform.SetAsLastSibling();

            float height = slotRoot.rect.height;
            if (height <= 0f) height = RefSlotRootHeight;
            float width = slotRoot.rect.width;
            if (width <= 0f) width = 112f;

            // Height of 1 slot in local space
            float oneSlotHeight = capacity > 0 ? height / capacity : height;

            // Collar height is ~40% of one slot so liquid is visible above and below
            float collarHeight = oneSlotHeight * 0.4f;
            // Center the collar vertically within the first slot
            float collarCenterY = oneSlotHeight * 0.5f;

            var collarRect = anchorCollar.rectTransform;
            collarRect.anchorMin = new Vector2(0.5f, 0f);
            collarRect.anchorMax = new Vector2(0.5f, 0f);
            collarRect.pivot = new Vector2(0.5f, 0.5f);
            collarRect.sizeDelta = new Vector2(width, collarHeight);
            collarRect.anchoredPosition = new Vector2(0f, collarCenterY);
        }

        /// <summary>
        /// Positions the basePlate as a subtle foot beneath the bottle for sink bottles.
        /// Directly attached (no gap), slightly wider than the bottle, height ≤ 10% of bottle.
        /// For non-sink bottles, resets basePlate to default.
        /// </summary>
        private void UpdateBaseLayout()
        {
            if (basePlate == null || outline == null) return;
            if (!isSink) return; // Non-sink bottles keep default basePlate

            var outlineRect = outline.rectTransform;
            float outlineHeight = outlineRect.rect.height;
            float outlineWidth = outlineRect.rect.width;
            if (outlineHeight <= 0f || outlineWidth <= 0f) return;

            float baseHeight = outlineHeight * BaseHeightRatio;
            float baseWidth = outlineWidth * BaseWidthRatio;

            var baseRect = basePlate.rectTransform;
            baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            baseRect.pivot = new Vector2(0.5f, 1f);
            baseRect.sizeDelta = new Vector2(baseWidth, baseHeight);

            float outlineBottom = outlineRect.anchoredPosition.y - outlineHeight * outlineRect.pivot.y;
            baseRect.anchoredPosition = new Vector2(0f, outlineBottom);
        }

        private void EnsureColorsInitialized()
        {
            if (outline != null && outlineDefaultColor == default)
            {
                outlineBaseColor = outline.color;
                outlineDefaultColor = outline.color;
            }
            if (body != null && bodyDefaultColor == default)
            {
                bodyDefaultColor = body.color;
            }
            if (basePlate != null && baseDefaultColor == default)
            {
                baseDefaultColor = basePlate.color;
            }
        }

        public void PreviewPour(int amount)
        {
            previewPourCount = Mathf.Max(0, amount);
            ApplyPreview();
        }

        public void ClearPreview()
        {
            previewPourCount = 0;
            ApplyPreview();
        }

        public void PreviewIncoming(ColorId color, int amount)
        {
            if (slotRoot == null || lastBottle == null) return;
            if (amount <= 0) return;

            EnsureIncomingSlots();

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            int cap = lastBottle.Capacity;
            int refCap = _levelMaxCapacity;
            int filledUnits = lastBottle.Count;
            float startY = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, filledUnits);
            float incomingHeight = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, amount);

            var image = incomingSlots[0];
            image.gameObject.SetActive(true);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, incomingHeight);
            rect.anchoredPosition = new Vector2(0, startY);

            if (palette != null)
            {
                var c = palette.GetColor(color);
                c.a = 0.6f;
                image.color = c;
            }

            UpdateLiquidSurfaceForFill(lastBottle.Count + amount, color);
        }

        public void AnimateIncoming(ColorId color, int amount, float t)
        {
            if (slotRoot == null || lastBottle == null) return;
            if (amount <= 0) return;
            t = Mathf.Clamp01(t);

            EnsureIncomingSlots();

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            int cap = lastBottle.Capacity;
            int refCap = _levelMaxCapacity;
            int filledUnits = lastBottle.Count;
            float startY = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, filledUnits);
            float incomingHeight = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, amount * t);

            var image = incomingSlots[0];
            image.gameObject.SetActive(true);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, incomingHeight);
            rect.anchoredPosition = new Vector2(0, startY);

            if (palette != null)
            {
                var c = palette.GetColor(color);
                c.a = 0.75f;
                image.color = c;
            }

            UpdateLiquidSurfaceForFill(lastBottle.Count + amount * t, color);
        }

        public void AnimateOutgoing(int amount, float t)
        {
            if (slotRoot == null || lastBottle == null) return;
            if (amount <= 0) return;
            t = Mathf.Clamp01(t);

            if (segmentUnits.Count == 0 || slots == null) return;

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            float unitHeight = height / lastBottle.Capacity; // unused but kept for API compat
            int topIndex = segmentUnits.Count - 1;
            int topUnits = Mathf.Max(0, segmentUnits[topIndex]);
            float removedUnits = Mathf.Clamp(amount * t, 0f, topUnits);
            float remainingUnits = Mathf.Max(0f, topUnits - removedUnits);

            int cap = lastBottle.Capacity;
            int refCap = _levelMaxCapacity;

            float unitsBefore = 0f;
            for (int i = 0; i < topIndex; i++)
            {
                if (i < segmentUnits.Count)
                {
                    unitsBefore += segmentUnits[i];
                }
            }

            float yOffset = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, unitsBefore);

            var image = slots[topIndex];
            if (image == null) return;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, remainingUnits));
            rect.anchoredPosition = new Vector2(0, yOffset);

            UpdateLiquidSurfaceForFill(Mathf.Max(0f, lastBottle.Count - amount * t), lastBottle.TopColor);
        }

        public void ClearIncoming()
        {
            for (int i = 0; i < incomingSlots.Count; i++)
            {
                incomingSlots[i].gameObject.SetActive(false);
            }
            UpdateLiquidSurfaceForFill(lastBottle != null ? lastBottle.Count : 0, lastBottle != null ? lastBottle.TopColor : null);
        }

        public void ClearOutgoing()
        {
            ResetOutgoingVisuals();
            UpdateLiquidSurfaceForFill(lastBottle != null ? lastBottle.Count : 0, lastBottle != null ? lastBottle.TopColor : null);
        }

        private void EnsureSlots(int capacity)
        {
            if (slots == null)
            {
                slots = new List<Image>();
            }

            for (int i = slots.Count; i < capacity; i++)
            {
                var go = new GameObject($"Segment_{i + 1}", typeof(RectTransform));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(slotRoot, false);
                rect.anchorMin = new Vector2(0.5f, 0);
                rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0);

                var image = go.AddComponent<Image>();
                image.raycastTarget = false;
                if (liquidSprite != null)
                {
                    image.sprite = liquidSprite;
                    image.type = Image.Type.Simple;
                }
                slots.Add(image);
            }
        }

        private void EnsureIncomingSlots()
        {
            if (incomingSlots.Count > 0) return;
            var go = new GameObject("Incoming", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(slotRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            if (liquidSprite != null)
            {
                image.sprite = liquidSprite;
                image.type = Image.Type.Simple;
            }
            incomingSlots.Add(image);
        }

        private void ResetOutgoingVisuals()
        {
            if (slotRoot == null || lastBottle == null) return;
            if (segmentUnits.Count == 0 || slots == null) return;

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            int topIndex = segmentUnits.Count - 1;
            int topUnits = Mathf.Max(0, segmentUnits[topIndex]);

            int cap = lastBottle.Capacity;
            int refCap = _levelMaxCapacity;
            float unitsBefore = 0f;
            for (int i = 0; i < topIndex; i++)
            {
                if (i < segmentUnits.Count)
                {
                    unitsBefore += segmentUnits[i];
                }
            }

            float yOffset = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, unitsBefore);

            var image = slots[topIndex];
            if (image == null) return;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, topUnits));
            rect.anchoredPosition = new Vector2(0, yOffset);
        }

        private void RenderSegment(ColorId? color, int units, int index, float width, float height, int capacity, int refCapacity, List<int> unitList, int unitsBefore)
        {
            if (index < 0 || index >= slots.Count) return;
            var image = slots[index];
            image.gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, BottleVisualMapping.LocalHeightForUnits(height, capacity, refCapacity, units));

            float yOffset = BottleVisualMapping.LocalHeightForUnits(height, capacity, refCapacity, unitsBefore);
            rect.anchoredPosition = new Vector2(0, yOffset);

            if (color.HasValue && palette != null)
            {
                var c = palette.GetColor(color.Value);

                // Boost brightness while preserving saturation
                Color.RGBToHSV(c, out float h, out float s, out float v);
                v = Mathf.Clamp01(v * 1.35f + 0.08f);
                s = Mathf.Clamp01(Mathf.Lerp(s, 1f, 0.12f));
                c = Color.HSVToRGB(h, s, v);

                c.a = 1f;
                image.color = c;
            }
            else
            {
                image.color = Color.clear;
            }

            unitList.Add(units);
        }

        private void ApplyPreview()
        {
            if (slots == null || segmentUnits.Count == 0) return;

            int remaining = previewPourCount;
            for (int i = segmentUnits.Count - 1; i >= 0; i--)
            {
                var image = slots[i];
                if (image == null) continue;

                var color = image.color;
                if (remaining > 0)
                {
                    color.a = 0.4f;
                    remaining -= segmentUnits[i];
                }
                else
                {
                    color.a = 1f;
                }
                image.color = color;
            }
        }

        public void PlayResistanceFeedback()
        {
            if (!isSink) return;
            if (resistanceRoutine != null)
            {
                StopCoroutine(resistanceRoutine);
            }
            resistanceRoutine = StartCoroutine(AnimateResistance());
        }

        private IEnumerator AnimateResistance()
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                resistanceRoutine = null;
                yield break;
            }

            Vector2 start = rect.anchoredPosition;
            Quaternion startRot = rect.localRotation;
            float duration = 0.14f;
            float time = 0f;
            float amplitude = 4f;
            float rotation = 2.5f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float damper = 1f - t;
                float shake = Mathf.Sin(t * Mathf.PI * 3f) * amplitude * damper;
                float twist = Mathf.Sin(t * Mathf.PI * 2f) * rotation * damper;
                rect.anchoredPosition = start + new Vector2(shake, 0f);
                rect.localRotation = Quaternion.Euler(0f, 0f, twist);
                yield return null;
            }

            rect.anchoredPosition = start;
            rect.localRotation = startRot;
            resistanceRoutine = null;
        }

        private void UpdateLiquidSurfaceForFill(float filledUnits, ColorId? topColor)
        {
            if (liquidSurface == null || slotRoot == null || lastBottle == null) return;

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            float clampedUnits = Mathf.Clamp(filledUnits, 0f, lastBottle.Capacity);
            int cap = lastBottle.Capacity;
            int refCap = _levelMaxCapacity;
            float fillHeight = BottleVisualMapping.LocalHeightForUnits(height, cap, refCap, clampedUnits);

            var rect = liquidSurface.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width * 0.98f, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(0f, Mathf.Clamp(fillHeight, 0f, height) - rect.sizeDelta.y * 0.35f);
            liquidSurface.gameObject.SetActive(fillHeight > 0.1f);

            if (topColor.HasValue && palette != null)
            {
                var c = palette.GetColor(topColor.Value);
                Color.RGBToHSV(c, out float h, out float s, out float v);
                v = Mathf.Clamp01(v + 0.18f);
                c = Color.HSVToRGB(h, s, v);
                c.a = 0.55f;
                liquidSurface.color = c;
            }
            else
            {
                liquidSurface.color = new Color(1f, 1f, 1f, 0f);
            }
        }
    }
}
