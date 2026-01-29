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

        private readonly List<int> segmentUnits = new List<int>();
        private readonly List<Image> incomingSlots = new List<Image>();
        private Image outgoingMask;
        private int previewPourCount;
        private Color outlineBaseColor;
        private Bottle lastBottle;

        public int Index { get; private set; }

        private void Awake()
        {
            if (outline != null)
            {
                outlineBaseColor = outline.color;
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

            EnsureOutgoingMask();

            float width = slotRoot.rect.width;
            float height = slotRoot.rect.height;
            if (width <= 0f || height <= 0f)
            {
                width = 100f;
                height = 300f;
            }

            float unitHeight = height / lastBottle.Capacity;
            float outgoingHeight = unitHeight * amount * t;
            float filledHeight = unitHeight * lastBottle.Count;
            float maskY = Mathf.Max(0f, filledHeight - outgoingHeight);

            outgoingMask.gameObject.SetActive(true);
            var rect = outgoingMask.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(width, outgoingHeight);
            rect.anchoredPosition = new Vector2(0, maskY);
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
            if (outgoingMask != null)
            {
                outgoingMask.gameObject.SetActive(false);
            }
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

        private void EnsureOutgoingMask()
        {
            if (outgoingMask != null) return;
            var go = new GameObject("OutgoingMask", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(slotRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            outgoingMask = go.AddComponent<Image>();
            outgoingMask.raycastTarget = false;
            outgoingMask.color = Color.black;
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
