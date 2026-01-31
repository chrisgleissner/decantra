/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System.Collections.Generic;
using Decantra.Domain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation.View
{
    public sealed class BottleView : MonoBehaviour
    {
        [SerializeField] private RectTransform slotRoot;
        [SerializeField] private List<Image> slots = new List<Image>();
        [SerializeField] private ColorPalette palette;
        [SerializeField] private Image outline;
        [SerializeField] private Image body;
        [SerializeField] private Image basePlate;
        [SerializeField] private Image stopper;

        private readonly List<int> segmentUnits = new List<int>();
        private readonly List<Image> incomingSlots = new List<Image>();
        private int previewPourCount;
        private Color outlineBaseColor;
        private Color outlineDefaultColor;
        private Color bodyDefaultColor;
        private Color baseDefaultColor;
        private Vector3 baseScale = Vector3.one;
        private Bottle lastBottle;

        public int Index { get; private set; }

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

            int segmentIndex = 0;
            segmentUnits.Clear();

            int runCount = 0;
            ColorId? runColor = null;
            for (int i = 0; i < bottle.Slots.Count; i++)
            {
                var color = bottle.Slots[i];
                if (!color.HasValue)
                {
                    if (runCount > 0)
                    {
                        RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, segmentUnits);
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
                    RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, segmentUnits);
                    runColor = color;
                    runCount = 1;
                }
            }

            if (runCount > 0)
            {
                RenderSegment(runColor, runCount, segmentIndex++, width, height, bottle.Capacity, segmentUnits);
            }

            for (int i = segmentIndex; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }

            ApplyPreview();

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
            float scaleY = 1f;
            float scaleX = 1f;
            if (capacity <= 3)
            {
                scaleY = 0.88f;
                scaleX = 0.95f;
            }
            else if (capacity >= 5)
            {
                scaleY = 1.06f;
                scaleX = 1.02f;
            }

            transform.localScale = new Vector3(baseScale.x * scaleX, baseScale.y * scaleY, baseScale.z);
        }

        private void ApplySinkStyle(Bottle bottle)
        {
            if (bottle == null) return;

            if (bottle.IsSink)
            {
                if (basePlate != null)
                {
                    basePlate.gameObject.SetActive(true);
                    basePlate.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
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
            }
            else
            {
                if (basePlate != null)
                {
                    basePlate.gameObject.SetActive(false);
                    basePlate.color = baseDefaultColor;
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
            }
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

            int filledUnits = lastBottle.Count;
            float unitHeight = height / lastBottle.Capacity;
            float startY = unitHeight * filledUnits;
            float incomingHeight = unitHeight * amount;

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

            int filledUnits = lastBottle.Count;
            float unitHeight = height / lastBottle.Capacity;
            float startY = unitHeight * filledUnits;
            float incomingHeight = unitHeight * amount * t;

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

            float unitHeight = height / lastBottle.Capacity;
            int topIndex = segmentUnits.Count - 1;
            int topUnits = Mathf.Max(0, segmentUnits[topIndex]);
            float removedUnits = Mathf.Clamp(amount * t, 0f, topUnits);
            float remainingUnits = Mathf.Max(0f, topUnits - removedUnits);

            float yOffset = 0f;
            for (int i = 0; i < topIndex; i++)
            {
                if (i < segmentUnits.Count)
                {
                    yOffset += height * segmentUnits[i] / lastBottle.Capacity;
                }
            }

            var image = slots[topIndex];
            if (image == null) return;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, height * remainingUnits / lastBottle.Capacity);
            rect.anchoredPosition = new Vector2(0, yOffset);
        }

        public void ClearIncoming()
        {
            for (int i = 0; i < incomingSlots.Count; i++)
            {
                incomingSlots[i].gameObject.SetActive(false);
            }
        }

        public void ClearOutgoing()
        {
            ResetOutgoingVisuals();
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
            float yOffset = 0f;
            for (int i = 0; i < topIndex; i++)
            {
                if (i < segmentUnits.Count)
                {
                    yOffset += height * segmentUnits[i] / lastBottle.Capacity;
                }
            }

            var image = slots[topIndex];
            if (image == null) return;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, height * topUnits / lastBottle.Capacity);
            rect.anchoredPosition = new Vector2(0, yOffset);
        }

        private void RenderSegment(ColorId? color, int units, int index, float width, float height, int capacity, List<int> unitList)
        {
            if (index < 0 || index >= slots.Count) return;
            var image = slots[index];
            image.gameObject.SetActive(true);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, height * units / capacity);

            float yOffset = 0f;
            for (int i = 0; i < index; i++)
            {
                if (i < unitList.Count)
                {
                    yOffset += height * unitList[i] / capacity;
                }
            }
            rect.anchoredPosition = new Vector2(0, yOffset);

            if (color.HasValue && palette != null)
            {
                var c = palette.GetColor(color.Value);
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
    }
}
