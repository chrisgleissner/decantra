/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Decantra.Presentation.View
{
    public sealed class BottleInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private BottleView bottleView;
        [SerializeField] private Controller.GameController controller;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private LayoutElement layoutElement;
        private CanvasGroup canvasGroup;
        private GridLayoutGroup gridLayout;
        private Vector3 originalPosition;
        private Transform originalParent;
        private BottleInput currentTarget;
        private Quaternion originalRotation;
        private bool _isDragging;

        public bool IsDragging => _isDragging;

        private void EnsureComponents()
        {
            rectTransform ??= GetComponent<RectTransform>();
            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
                if (rootCanvas == null)
                {
                    rootCanvas = Object.FindObjectOfType<Canvas>();
                }
            }

            layoutElement ??= GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            gridLayout ??= GetComponentInParent<GridLayoutGroup>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (bottleView == null || controller == null) return;
            controller.NotifyFirstInteraction();
            controller.OnBottleTapped(bottleView.Index);
        }

        private void Awake()
        {
            EnsureComponents();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (controller == null || bottleView == null) return;
            if (controller.IsInputLocked) return;
            EnsureComponents();
            if (rootCanvas == null) return;
            if (!controller.CanDragBottle(bottleView.Index))
            {
                if (bottleView.IsSink)
                {
                    controller.NotifyFirstInteraction();
                    bottleView.PlayResistanceFeedback();
                }
                return;
            }

            controller.NotifyFirstInteraction();

            originalParent = rectTransform.parent;
            originalPosition = rectTransform.position;
            originalRotation = rectTransform.rotation;
            layoutElement.ignoreLayout = false;
            if (gridLayout != null)
            {
                gridLayout.enabled = false;
            }
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
            }
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controller == null || bottleView == null) return;
            if (rootCanvas == null || rectTransform == null) return;
            if (!_isDragging) return;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                rootCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var worldPoint);
            rectTransform.position = worldPoint;

            var target = FindDropTarget(eventData);
            if (target != currentTarget)
            {
                ClearPreview();
                currentTarget = target;
            }

            if (currentTarget != null)
            {
                int amount = controller.GetPourAmount(bottleView.Index, currentTarget.bottleView.Index);
                if (amount > 0)
                {
                    bottleView.PreviewPour(amount);
                    bottleView.transform.rotation = Quaternion.Euler(0, 0, -15f);
                    currentTarget.bottleView.SetHighlight(true);
                }
                else
                {
                    bottleView.ClearPreview();
                    bottleView.transform.rotation = originalRotation;
                    currentTarget.bottleView.SetHighlight(false);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (controller == null || bottleView == null) return;
            if (!_isDragging) return;
            _isDragging = false;
            var target = FindDropTarget(eventData);
            ClearPreview();

            bool moved = false;
            float duration = 0.15f;
            if (target != null && target.bottleView != null)
            {
                moved = controller.TryStartMove(bottleView.Index, target.bottleView.Index, out duration);
            }

            StartCoroutine(AnimateReturn(duration));
        }

        private IEnumerator AnimateReturn(float duration)
        {
            float time = 0f;
            Vector3 start = rectTransform.position;
            Vector3 end = originalPosition;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                rectTransform.position = Vector3.Lerp(start, end, t);
                yield return new WaitForEndOfFrame();
            }

            rectTransform.position = end;
            rectTransform.rotation = originalRotation;
            if (gridLayout != null)
            {
                gridLayout.enabled = true;
            }
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }
            if (layoutElement != null)
            {
                layoutElement.ignoreLayout = false;
            }
        }

        private BottleInput FindDropTarget(PointerEventData eventData)
        {
            if (EventSystem.current == null) return null;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                var candidate = results[i].gameObject.GetComponentInParent<BottleInput>();
                if (candidate != null && candidate != this)
                {
                    return candidate;
                }
            }
            return null;
        }

        private void ClearPreview()
        {
            bottleView.ClearPreview();
            if (currentTarget != null)
            {
                currentTarget.bottleView.SetHighlight(false);
                currentTarget.bottleView.ClearIncoming();
            }
            currentTarget = null;
        }
    }
}
