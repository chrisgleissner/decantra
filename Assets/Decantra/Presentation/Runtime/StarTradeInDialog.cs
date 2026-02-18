/*
Decantra - A Unity-based bottle-sorting puzzle game
Copyright (C) 2026 Christian Gleissner

Licensed under the GNU General Public License v2.0 or later.
See <https://www.gnu.org/licenses/> for details.
*/

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Decantra.Presentation
{
    internal static class StarTradeInUiConfig
    {
        internal static class FontSizes
        {
            public const int Header = ModalDesignTokens.Typography.ModalHeader;
            public const int Prompt = ModalDesignTokens.Typography.BodyText + 2;
            public const int CurrentStars = ModalDesignTokens.Typography.SectionTitle;
            public const int CardTitle = ModalDesignTokens.Typography.SectionTitle;
            public const int CardSubtitle = ModalDesignTokens.Typography.BodyText;
            public const int CostLabel = ModalDesignTokens.Typography.HelperText;
            public const int CostValue = ModalDesignTokens.Typography.CostText;
            public const int CardStatus = ModalDesignTokens.Typography.HelperText;
            public const int Confirmation = ModalDesignTokens.Typography.BodyText + 2;
            public const int ActionButton = ModalDesignTokens.Typography.ButtonText;
            public const int Helper = ModalDesignTokens.Typography.HelperText;
        }

        internal static class Copy
        {
            public const string Title = "Star Trade-In";
            public const string Prompt = "Choose an option below";
            public const string CurrentStarsFormat = "Current Stars: {0}";
            public const string ConvertTitle = "Convert All Sink Bottles";
            public const string ConvertSubtitle = "Sink bottles have a dark bottom stripe: they can receive liquid but cannot pour.";
            public const string AutoSolveTitle = "Auto-Solve Level";
            public const string AutoSolveSubtitle = "Plays a full solution for the current level.";
            public const string CostLabel = "Price";
            public const string StarsSuffix = "stars";
            public const string NotEnoughStars = "Not enough stars";
            public const string NoSinkBottles = "No sink bottles in this level";
            public const string Ready = "Ready";
            public const string ConfirmFormat = "Spend {0} stars to {1}?";
        }

        public static readonly Color ActiveConvertCardColor = ModalDesignTokens.Colors.PrimaryAction;
        public static readonly Color ActiveAutoSolveCardColor = new Color(0.32f, 0.23f, 0.5f, 0.96f);
        public static readonly Color DisabledCardColor = new Color(0.24f, 0.26f, 0.3f, 0.96f);
        public static readonly Color PrimaryTextColor = ModalDesignTokens.Colors.PrimaryText;
        public static readonly Color SecondaryTextColor = ModalDesignTokens.Colors.SecondaryText;
        public static readonly Color TertiaryTextColor = ModalDesignTokens.Colors.HelperText;
        public static readonly Color StatusWarningColor = ModalDesignTokens.Colors.Warning;
        public static readonly Color StatusNeutralColor = ModalDesignTokens.Colors.Positive;
        public static readonly Color DisabledTextColor = ModalDesignTokens.Colors.DisabledText;
    }

    public sealed class StarTradeInDialog : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text currentStarsText;
        [SerializeField] private Text messageText;
        [SerializeField] private GameObject selectionRoot;
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private Text confirmationText;

        [SerializeField] private Button convertButton;
        [SerializeField] private Image convertCardBackground;
        [SerializeField] private Text convertLabel;
        [SerializeField] private Text convertSubtitleText;
        [SerializeField] private Text convertCostLabelText;
        [SerializeField] private Text convertCostValueText;
        [SerializeField] private Text convertStatusText;

        [SerializeField] private Button autoSolveButton;
        [SerializeField] private Image autoSolveCardBackground;
        [SerializeField] private Text autoSolveLabel;
        [SerializeField] private Text autoSolveSubtitleText;
        [SerializeField] private Text autoSolveCostLabelText;
        [SerializeField] private Text autoSolveCostValueText;
        [SerializeField] private Text autoSolveStatusText;

        [SerializeField] private Text sinkDefinitionText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action _onClose;
        private Action _pendingConfirmAction;
        private bool _initialized;

        public bool IsVisible => gameObject.activeInHierarchy && canvasGroup != null && canvasGroup.blocksRaycasts && canvasGroup.alpha > 0.01f;

        public void Initialize()
        {
            EnsureCanvasGroup();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseAndNotify);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(ConfirmAndClose);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(SetSelectionState);
            }

            ApplyStaticCopy();

            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Hide();
        }

        public void Show(
            int currentStars,
            int convertCost,
            bool hasSinkBottle,
            bool canConvert,
            int autoSolveCost,
            bool canAutoSolve,
            Action onConvert,
            Action onAutoSolve,
            Action onClose)
        {
            Initialize();
            _onClose = onClose;
            _pendingConfirmAction = null;

            ApplyStaticCopy();

            if (currentStarsText != null)
            {
                currentStarsText.text = string.Format(
                    StarTradeInUiConfig.Copy.CurrentStarsFormat,
                    Mathf.Max(0, currentStars));
            }

            bool canAffordConvert = currentStars >= convertCost;
            bool convertStarGated = hasSinkBottle && (!canAffordConvert || !canConvert);
            string convertStatus = hasSinkBottle
                ? (convertStarGated ? StarTradeInUiConfig.Copy.NotEnoughStars : StarTradeInUiConfig.Copy.Ready)
                : StarTradeInUiConfig.Copy.NoSinkBottles;
            bool convertEnabled = hasSinkBottle && canAffordConvert && canConvert && onConvert != null;

            ConfigureCard(
                convertButton,
                convertCardBackground,
                convertCostValueText,
                convertStatusText,
                convertCost,
                convertEnabled,
                convertStatus,
                StarTradeInUiConfig.ActiveConvertCardColor,
                () => BeginConfirm(StarTradeInUiConfig.Copy.ConvertTitle, convertCost, onConvert));

            bool canAffordAutoSolve = currentStars >= autoSolveCost;
            string autoStatus = canAffordAutoSolve && canAutoSolve
                ? StarTradeInUiConfig.Copy.Ready
                : StarTradeInUiConfig.Copy.NotEnoughStars;
            bool autoEnabled = canAffordAutoSolve && canAutoSolve && onAutoSolve != null;

            ConfigureCard(
                autoSolveButton,
                autoSolveCardBackground,
                autoSolveCostValueText,
                autoSolveStatusText,
                autoSolveCost,
                autoEnabled,
                autoStatus,
                StarTradeInUiConfig.ActiveAutoSolveCardColor,
                () => BeginConfirm(StarTradeInUiConfig.Copy.AutoSolveTitle, autoSolveCost, onAutoSolve));

            SetSelectionState();

            if (canvasGroup == null || panel == null)
            {
                return;
            }

            gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            panel.anchoredPosition = Vector2.zero;
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            _pendingConfirmAction = null;

            EnsureCanvasGroup();

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            SetSelectionState();
            gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup != null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void ApplyStaticCopy()
        {
            if (messageText != null)
            {
                messageText.text = StarTradeInUiConfig.Copy.Prompt;
            }

            if (convertLabel != null)
            {
                convertLabel.text = StarTradeInUiConfig.Copy.ConvertTitle;
            }

            if (convertSubtitleText != null)
            {
                convertSubtitleText.text = StarTradeInUiConfig.Copy.ConvertSubtitle;
            }

            if (convertCostLabelText != null)
            {
                convertCostLabelText.text = StarTradeInUiConfig.Copy.CostLabel;
            }

            if (autoSolveLabel != null)
            {
                autoSolveLabel.text = StarTradeInUiConfig.Copy.AutoSolveTitle;
            }

            if (autoSolveSubtitleText != null)
            {
                autoSolveSubtitleText.text = StarTradeInUiConfig.Copy.AutoSolveSubtitle;
            }

            if (autoSolveCostLabelText != null)
            {
                autoSolveCostLabelText.text = StarTradeInUiConfig.Copy.CostLabel;
            }

            if (sinkDefinitionText != null)
            {
                sinkDefinitionText.text = string.Empty;
                sinkDefinitionText.gameObject.SetActive(false);
            }

            ApplyWrapping(convertSubtitleText);
            ApplyWrapping(autoSolveSubtitleText);
        }

        private static void ApplyWrapping(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
        }

        private void ConfigureCard(
            Button button,
            Image cardBackground,
            Text costValue,
            Text statusText,
            int cost,
            bool isEnabled,
            string status,
            Color activeColor,
            Action onClick)
        {
            if (costValue != null)
            {
                costValue.text = $"{Mathf.Max(0, cost)} {StarTradeInUiConfig.Copy.StarsSuffix}";
            }

            if (button != null)
            {
                button.interactable = isEnabled;
                button.onClick.RemoveAllListeners();
                if (isEnabled && onClick != null)
                {
                    button.onClick.AddListener(() => onClick.Invoke());
                }
            }

            if (cardBackground != null)
            {
                cardBackground.color = isEnabled ? activeColor : StarTradeInUiConfig.DisabledCardColor;
            }

            Color bodyColor = isEnabled ? StarTradeInUiConfig.PrimaryTextColor : StarTradeInUiConfig.DisabledTextColor;
            ApplyCardTextColors(button, bodyColor);

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = status == StarTradeInUiConfig.Copy.NotEnoughStars
                    ? StarTradeInUiConfig.StatusWarningColor
                    : (status == StarTradeInUiConfig.Copy.Ready
                        ? StarTradeInUiConfig.StatusNeutralColor
                        : StarTradeInUiConfig.DisabledTextColor);
            }
        }

        private static void ApplyCardTextColors(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            var texts = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                {
                    texts[i].color = color;
                }
            }
        }

        private void BeginConfirm(string actionLabel, int cost, Action confirmAction)
        {
            if (confirmAction == null)
            {
                return;
            }

            _pendingConfirmAction = confirmAction;

            if (confirmationText != null)
            {
                confirmationText.text = string.Format(StarTradeInUiConfig.Copy.ConfirmFormat, Mathf.Max(0, cost), actionLabel);
            }

            if (selectionRoot != null)
            {
                selectionRoot.SetActive(false);
            }

            if (confirmationRoot != null)
            {
                confirmationRoot.SetActive(true);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }
        }

        private void SetSelectionState()
        {
            _pendingConfirmAction = null;

            if (selectionRoot != null)
            {
                selectionRoot.SetActive(true);
            }

            if (confirmationRoot != null)
            {
                confirmationRoot.SetActive(false);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = false;
            }
        }

        private void ConfirmAndClose()
        {
            if (_pendingConfirmAction == null)
            {
                SetSelectionState();
                return;
            }

            var pending = _pendingConfirmAction;
            _pendingConfirmAction = null;
            CloseAndNotify();
            pending.Invoke();
        }

        private void CloseAndNotify()
        {
            Hide();
            _onClose?.Invoke();
        }

        private void Awake()
        {
            Initialize();
        }
    }
}
