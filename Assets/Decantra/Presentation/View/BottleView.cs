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
        [SerializeField] private Image anchorCollar;
        [SerializeField] private Image anchorShadow;
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

        public int Index { get; private set; }
        public bool IsSink => isSink;

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

            // Repurpose curvedHighlight for right-side reflection
            if (curvedHighlight != null)
            {
                curvedHighlight.gameObject.SetActive(true);
                curvedHighlight.color = new Color(1f, 1f, 1f, 0.08f);
                curvedHighlight.raycastTarget = false;

                var rect = curvedHighlight.rectTransform;
                // Right side reflection: 20% width, 70% height, offset from right
                // Using anchors: X from 0.70 to 0.90, Y from 0.15 to 0.85
                rect.anchorMin = new Vector2(0.7f, 0.15f);
                rect.anchorMax = new Vector2(0.9f, 0.85f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
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

            isSink = bottle.IsSink;

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

                if (anchorCollar != null)
                {
                    anchorCollar.gameObject.SetActive(true);
                }

                if (anchorShadow != null)
                {
                    anchorShadow.gameObject.SetActive(true);
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
                    basePlate.gameObject.SetActive(true);
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

                if (anchorCollar != null)
                {
                    anchorCollar.gameObject.SetActive(false);
                }

                if (anchorShadow != null)
                {
                    anchorShadow.gameObject.SetActive(false);
                }

                if (normalShadow != null)
                {
                    normalShadow.gameObject.SetActive(false);
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

                // Boost brightness significantly while maintaining saturation
                Color.RGBToHSV(c, out float h, out float s, out float v);
                v = Mathf.Clamp01(v * 1.8f); // Increase brightness significantly
                s = Mathf.Clamp01(s * 1.8f); // Boost saturation to counter any washout
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

            float unitHeight = height / lastBottle.Capacity;
            float fillHeight = Mathf.Clamp(filledUnits, 0f, lastBottle.Capacity) * unitHeight;

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
                v = Mathf.Clamp01(v + 0.12f);
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
