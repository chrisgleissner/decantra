using System;
using System.Collections.Generic;
using System.Reflection;
using Decantra.Presentation.Controller;
using Decantra.Presentation.View;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Tests.PlayMode.Layout
{
    public sealed class LayoutProbe : MonoBehaviour
    {
        [Serializable]
        public sealed class LayoutMetrics
        {
            public string Tag;
            public string CapturedAtUtc;
            public int ScreenWidth;
            public int ScreenHeight;
            public float CanvasWidth;
            public float CanvasHeight;

            public float LogoTopY;
            public float LogoBottomY;
            public float LogoCenterX;

            public float Row1CapTopY;
            public float Row2CapTopY;
            public float Row3CapTopY;
            public float BottomBottleBottomY;

            public float LeftBottleCenterX;
            public float MiddleBottleCenterX;
            public float RightBottleCenterX;

            public float RowSpacing12;
            public float RowSpacing23;
            public float BottleSpacingLM;
            public float BottleSpacingMR;

            public float RowGap12;
            public float RowGap23;
            public bool HasBottleOverlap;

            public float LogoTopRatioY;
            public float LogoBottomRatioY;
            public float LogoCenterRatioX;
            public float Row1CapTopRatioY;
            public float Row2CapTopRatioY;
            public float Row3CapTopRatioY;
            public float BottomBottleBottomRatioY;
            public float LeftBottleCenterRatioX;
            public float MiddleBottleCenterRatioX;
            public float RightBottleCenterRatioX;
            public float RowSpacing12RatioY;
            public float RowSpacing23RatioY;
            public float BottleSpacingLMRatioX;
            public float BottleSpacingMRRatioX;
        }

        private readonly struct RowInfo
        {
            public RowInfo(List<BottleView> bottles, float topY, float bottomY)
            {
                Bottles = bottles;
                TopY = topY;
                BottomY = bottomY;
            }

            public List<BottleView> Bottles { get; }
            public float TopY { get; }
            public float BottomY { get; }
        }

        public LayoutMetrics Capture(GameController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            var canvasRect = ResolveReferenceCanvasRect();
            var bottleViews = ResolveBottleViews(controller);
            if (bottleViews.Count < 9)
            {
                throw new InvalidOperationException($"Expected 9 bottle views, found {bottleViews.Count}.");
            }

            var rows = BuildRows(bottleViews, canvasRect);
            if (rows.Count < 3)
            {
                throw new InvalidOperationException($"Expected at least 3 bottle rows, found {rows.Count}.");
            }

            var row1 = rows[0];
            var row2 = rows[1];
            var row3 = rows[2];

            var row1CenterBottle = GetCenterBottleByX(row1.Bottles, canvasRect);
            var row2CenterBottle = GetCenterBottleByX(row2.Bottles, canvasRect);
            var row3CenterBottle = GetCenterBottleByX(row3.Bottles, canvasRect);

            var referenceRowForColumns = row2.Bottles.Count == 3 ? row2 : row1;
            var sortedColumnBottles = new List<BottleView>(referenceRowForColumns.Bottles);
            sortedColumnBottles.Sort((a, b) => GetBottleCenterX(a, canvasRect).CompareTo(GetBottleCenterX(b, canvasRect)));

            var logoRect = ResolveLogoRect();
            var logoBounds = GetBoundsInReference(logoRect, canvasRect);

            float row1CapTop = GetCapTopY(row1CenterBottle, canvasRect);
            float row2CapTop = GetCapTopY(row2CenterBottle, canvasRect);
            float row3CapTop = GetCapTopY(row3CenterBottle, canvasRect);

            float bottomBottleBottom = GetBottleBottomY(row3CenterBottle, canvasRect);
            float leftCenter = GetBottleCenterX(sortedColumnBottles[0], canvasRect);
            float middleCenter = GetBottleCenterX(sortedColumnBottles[1], canvasRect);
            float rightCenter = GetBottleCenterX(sortedColumnBottles[2], canvasRect);

            float canvasWidth = Mathf.Max(1f, canvasRect.rect.width);
            float canvasHeight = Mathf.Max(1f, canvasRect.rect.height);

            float rowSpacing12 = row1CapTop - row2CapTop;
            float rowSpacing23 = row2CapTop - row3CapTop;
            float bottleSpacingLm = middleCenter - leftCenter;
            float bottleSpacingMr = rightCenter - middleCenter;

            float rowGap12 = row1.BottomY - row2.TopY;
            float rowGap23 = row2.BottomY - row3.TopY;

            return new LayoutMetrics
            {
                Tag = string.Empty,
                CapturedAtUtc = DateTime.UtcNow.ToString("O"),
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                CanvasWidth = canvasWidth,
                CanvasHeight = canvasHeight,

                LogoTopY = logoBounds.Top,
                LogoBottomY = logoBounds.Bottom,
                LogoCenterX = logoBounds.CenterX,

                Row1CapTopY = row1CapTop,
                Row2CapTopY = row2CapTop,
                Row3CapTopY = row3CapTop,
                BottomBottleBottomY = bottomBottleBottom,

                LeftBottleCenterX = leftCenter,
                MiddleBottleCenterX = middleCenter,
                RightBottleCenterX = rightCenter,

                RowSpacing12 = rowSpacing12,
                RowSpacing23 = rowSpacing23,
                BottleSpacingLM = bottleSpacingLm,
                BottleSpacingMR = bottleSpacingMr,

                RowGap12 = rowGap12,
                RowGap23 = rowGap23,
                HasBottleOverlap = rowGap12 < 0f || rowGap23 < 0f,

                LogoTopRatioY = logoBounds.Top / canvasHeight,
                LogoBottomRatioY = logoBounds.Bottom / canvasHeight,
                LogoCenterRatioX = logoBounds.CenterX / canvasWidth,
                Row1CapTopRatioY = row1CapTop / canvasHeight,
                Row2CapTopRatioY = row2CapTop / canvasHeight,
                Row3CapTopRatioY = row3CapTop / canvasHeight,
                BottomBottleBottomRatioY = bottomBottleBottom / canvasHeight,
                LeftBottleCenterRatioX = leftCenter / canvasWidth,
                MiddleBottleCenterRatioX = middleCenter / canvasWidth,
                RightBottleCenterRatioX = rightCenter / canvasWidth,
                RowSpacing12RatioY = rowSpacing12 / canvasHeight,
                RowSpacing23RatioY = rowSpacing23 / canvasHeight,
                BottleSpacingLMRatioX = bottleSpacingLm / canvasWidth,
                BottleSpacingMRRatioX = bottleSpacingMr / canvasWidth
            };
        }

        private static RectTransform ResolveReferenceCanvasRect()
        {
            var uiCanvas = GameObject.Find("Canvas_UI")?.GetComponent<Canvas>();
            if (uiCanvas == null)
            {
                throw new InvalidOperationException("Canvas_UI not found.");
            }

            var canvasRect = uiCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                throw new InvalidOperationException("Canvas_UI RectTransform not found.");
            }

            return canvasRect;
        }

        private static RectTransform ResolveLogoRect()
        {
            var logo = GameObject.Find("BrandLockup")?.GetComponent<RectTransform>();
            if (logo == null)
            {
                throw new InvalidOperationException("BrandLockup not found.");
            }

            return logo;
        }

        private static List<BottleView> ResolveBottleViews(GameController controller)
        {
            var field = typeof(GameController).GetField("bottleViews", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("GameController.bottleViews field not found.");
            }

            var views = field.GetValue(controller) as List<BottleView>;
            if (views == null)
            {
                throw new InvalidOperationException("GameController.bottleViews is null.");
            }

            var result = new List<BottleView>(views.Count);
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null)
                {
                    result.Add(views[i]);
                }
            }

            return result;
        }

        private static List<RowInfo> BuildRows(List<BottleView> bottles, RectTransform referenceRect)
        {
            var entries = new List<(BottleView Bottle, float CenterY)>(bottles.Count);
            for (int i = 0; i < bottles.Count; i++)
            {
                var view = bottles[i];
                var rect = view.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                var bounds = GetBoundsInReference(rect, referenceRect);
                entries.Add((view, (bounds.Top + bounds.Bottom) * 0.5f));
            }

            entries.Sort((a, b) => b.CenterY.CompareTo(a.CenterY));

            var rows = new List<RowInfo>(3);
            for (int offset = 0; offset < entries.Count; offset += 3)
            {
                int count = Mathf.Min(3, entries.Count - offset);
                if (count <= 0)
                {
                    continue;
                }

                var rowBottles = new List<BottleView>(count);
                float top = float.MinValue;
                float bottom = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var bottle = entries[offset + i].Bottle;
                    rowBottles.Add(bottle);

                    var bottleBounds = GetBottleVisualBoundsInReference(bottle, referenceRect);
                    top = Mathf.Max(top, bottleBounds.Top);
                    bottom = Mathf.Min(bottom, bottleBounds.Bottom);
                }

                rows.Add(new RowInfo(rowBottles, top, bottom));
            }

            return rows;
        }

        private static BottleView GetCenterBottleByX(List<BottleView> row, RectTransform referenceRect)
        {
            if (row.Count == 0)
            {
                throw new InvalidOperationException("Bottle row is empty.");
            }

            var ordered = new List<BottleView>(row);
            ordered.Sort((a, b) => GetBottleCenterX(a, referenceRect).CompareTo(GetBottleCenterX(b, referenceRect)));
            return ordered[Mathf.Clamp(ordered.Count / 2, 0, ordered.Count - 1)];
        }

        private static float GetBottleCenterX(BottleView bottle, RectTransform referenceRect)
        {
            var rect = bottle.GetComponent<RectTransform>();
            var bounds = GetBoundsInReference(rect, referenceRect);
            return bounds.CenterX;
        }

        private static float GetCapTopY(BottleView bottle, RectTransform referenceRect)
        {
            var rimRect = bottle.transform.Find("Rim") as RectTransform;
            if (rimRect != null)
            {
                return GetBoundsInReference(rimRect, referenceRect).Top;
            }

            return GetBottleVisualBoundsInReference(bottle, referenceRect).Top;
        }

        private static float GetBottleBottomY(BottleView bottle, RectTransform referenceRect)
        {
            return GetBottleVisualBoundsInReference(bottle, referenceRect).Bottom;
        }

        private static Bounds2D GetBottleVisualBoundsInReference(BottleView bottle, RectTransform referenceRect)
        {
            var outlineField = typeof(BottleView).GetField("outline", BindingFlags.Instance | BindingFlags.NonPublic);
            var outline = outlineField != null ? outlineField.GetValue(bottle) as Image : null;
            var targetRect = outline != null ? outline.rectTransform : bottle.GetComponent<RectTransform>();
            return GetBoundsInReference(targetRect, referenceRect);
        }

        private static Bounds2D GetBoundsInReference(RectTransform rect, RectTransform referenceRect)
        {
            if (rect == null || referenceRect == null)
            {
                throw new InvalidOperationException("RectTransform required for layout probe bounds.");
            }

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float top = float.MinValue;
            float bottom = float.MaxValue;
            float left = float.MaxValue;
            float right = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                var local = referenceRect.InverseTransformPoint(corners[i]);
                top = Mathf.Max(top, local.y);
                bottom = Mathf.Min(bottom, local.y);
                left = Mathf.Min(left, local.x);
                right = Mathf.Max(right, local.x);
            }

            return new Bounds2D(top, bottom, left, right);
        }

        private readonly struct Bounds2D
        {
            public Bounds2D(float top, float bottom, float left, float right)
            {
                Top = top;
                Bottom = bottom;
                Left = left;
                Right = right;
            }

            public float Top { get; }
            public float Bottom { get; }
            public float Left { get; }
            public float Right { get; }
            public float CenterX => (Left + Right) * 0.5f;
        }
    }
}