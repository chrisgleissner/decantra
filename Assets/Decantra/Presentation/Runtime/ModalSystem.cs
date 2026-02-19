/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    internal static class ModalDesignTokens
    {
        internal static class Typography
        {
            public const int ModalHeader = 48;
            public const int SectionTitle = 34;
            public const int BodyText = 30;
            public const int HelperText = 24;
            public const int ButtonText = 32;
            public const int CostText = 34;
        }

        internal static class Spacing
        {
            public const int OuterPadding = 28;
            public const int InnerPadding = 20;
            public const float SectionGap = 16f;
            public const float ControlGap = 12f;
        }

        internal static class Sizing
        {
            public static readonly Vector2 ModalMargin = new Vector2(44f, 56f);
            public static readonly Vector2 MediumPreferred = new Vector2(920f, 1320f);
            public static readonly Vector2 MediumMinimum = new Vector2(620f, 760f);
            public static readonly Vector2 CompactPreferred = new Vector2(920f, 780f);
            public static readonly Vector2 CompactMinimum = new Vector2(620f, 560f);
            public const float ActionButtonHeight = 76f;
        }

        internal static class Colors
        {
            public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.78f);
            public static readonly Color Panel = new Color(0.08f, 0.1f, 0.14f, 0.975f);
            public static readonly Color SectionSurface = new Color(1f, 1f, 1f, 0.06f);
            public static readonly Color PrimaryText = new Color(1f, 0.98f, 0.92f, 1f);
            public static readonly Color SecondaryText = new Color(1f, 0.98f, 0.92f, 0.9f);
            public static readonly Color HelperText = new Color(1f, 0.98f, 0.92f, 0.78f);
            public static readonly Color DisabledText = new Color(0.84f, 0.85f, 0.88f, 0.9f);
            public static readonly Color Warning = new Color(1f, 0.78f, 0.74f, 0.96f);
            public static readonly Color Positive = new Color(0.84f, 0.95f, 0.88f, 0.96f);
            public static readonly Color PrimaryAction = new Color(0.2f, 0.34f, 0.54f, 0.95f);
            public static readonly Color SecondaryAction = new Color(0.16f, 0.22f, 0.34f, 0.95f);
            public static readonly Color DestructiveAction = new Color(0.72f, 0.22f, 0.2f, 0.95f);
            public static readonly Color ConfirmAction = new Color(0.16f, 0.45f, 0.2f, 0.95f);
        }
    }

    public sealed class BaseModal : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool hideOnAwake;

        private bool _initialized;

        public bool IsVisible
        {
            get
            {
                if (canvasGroup == null)
                {
                    return gameObject.activeSelf;
                }

                return gameObject.activeSelf && canvasGroup.blocksRaycasts && canvasGroup.alpha > 0.01f;
            }
        }

        public void Configure(CanvasGroup group, bool shouldHideOnAwake = false)
        {
            canvasGroup = group;
            hideOnAwake = shouldHideOnAwake;
            _initialized = true;
        }

        public void Show()
        {
            EnsureInitialized();
            gameObject.SetActive(true);

            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            EnsureInitialized();

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            if (!hideOnAwake)
            {
                EnsureInitialized();
                return;
            }

            Hide();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            _initialized = true;
        }
    }

    public sealed class ResponsiveModalPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Vector2 preferredSize = new Vector2(920f, 1320f);
        [SerializeField] private Vector2 minimumSize = new Vector2(620f, 760f);
        [SerializeField] private Vector2 viewportMargin = new Vector2(44f, 56f);

        private Vector2 _lastViewportSize = Vector2.negativeInfinity;

        public void Configure(RectTransform target, Vector2 preferred, Vector2 minimum, Vector2 margin)
        {
            panel = target;
            preferredSize = preferred;
            minimumSize = minimum;
            viewportMargin = margin;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (panel == null)
            {
                return;
            }

            var parent = panel.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Vector2 viewportSize = parent.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return;
            }

            if ((viewportSize - _lastViewportSize).sqrMagnitude < 0.1f)
            {
                return;
            }

            _lastViewportSize = viewportSize;
            float availableWidth = Mathf.Max(320f, viewportSize.x - (viewportMargin.x * 2f));
            float availableHeight = Mathf.Max(360f, viewportSize.y - (viewportMargin.y * 2f));
            float effectiveMinWidth = Mathf.Min(minimumSize.x, availableWidth);
            float effectiveMinHeight = Mathf.Min(minimumSize.y, availableHeight);
            float width = Mathf.Clamp(preferredSize.x, effectiveMinWidth, availableWidth);
            float height = Mathf.Clamp(preferredSize.y, effectiveMinHeight, availableHeight);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            var layoutElement = panel.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = width;
                layoutElement.preferredHeight = height;
            }
        }
    }
}
