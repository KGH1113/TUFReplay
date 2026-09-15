using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TUFReplay.Unity.Notifications
{
  [DisallowMultipleComponent]
  // The legacy type name is retained so existing Unity prefab script references remain stable.
  public sealed class MicrophonePermissionWarningView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    private const float DefaultToastDuration = 5f;
    private const float ShowDuration = 0.16f;
    private const float HideDuration = 0.12f;
    private const float HiddenOffset = -6f;
    private const float MinWidth = 300f;
    private const float MaxWidth = 520f;
    private const float MinHeight = 72f;
    private const float MaxHeight = 360f;
    private const float ScreenMargin = 18f;
    private const float HorizontalPadding = 16f;
    private const float TopPadding = 14f;
    private const float HeaderMinHeight = 26f;
    private const float HeaderGap = 8f;
    private const float TitleLeadingInset = 52f;
    private const float TitleTrailingInset = 44f;
    private const float ToastBottomReserve = 18f;
    private const float PersistentBottomPadding = 16f;
    private const float PersistentActionReserve = 58f;
    private const float ActionHorizontalPadding = 20f;
    private const float MinActionWidth = 112f;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text messageText;

    [SerializeField]
    private GameObject progressTrack;

    [SerializeField]
    private Image progressFill;

    [SerializeField]
    private Button actionButton;

    [SerializeField]
    private TMP_Text actionLabel;

    [SerializeField]
    private Button dismissButton;

    private RectTransform rectTransform;
    private Vector2 restingPosition;
    private NotificationState state;
    private float stateElapsed;
    private float remaining;
    private float toastDuration;
    private bool hovered;
    private bool initialized;
    private Action action;

    public bool IsConfigured =>
      canvasGroup != null
      && titleText != null
      && messageText != null
      && progressTrack != null
      && progressFill != null
      && actionButton != null
      && actionLabel != null
      && dismissButton != null;

    public void ConfigureReferences(
      CanvasGroup configuredCanvasGroup,
      TMP_Text configuredTitleText,
      TMP_Text configuredMessageText,
      GameObject configuredProgressTrack,
      Image configuredProgressFill,
      Button configuredActionButton,
      TMP_Text configuredActionLabel,
      Button configuredDismissButton
    )
    {
      canvasGroup = configuredCanvasGroup;
      titleText = configuredTitleText;
      messageText = configuredMessageText;
      progressTrack = configuredProgressTrack;
      progressFill = configuredProgressFill;
      actionButton = configuredActionButton;
      actionLabel = configuredActionLabel;
      dismissButton = configuredDismissButton;
      initialized = false;
    }

    public bool ShowToast(string title, string message, float duration = DefaultToastDuration)
    {
      if (state is NotificationState.ShowingPersistent or NotificationState.PersistentVisible)
        return false;

      toastDuration = Mathf.Max(0.1f, duration);
      remaining = toastDuration;
      return Present(title, message, false, null, null);
    }

    public bool ShowPersistent(string title, string message, string actionText = null, Action actionCallback = null)
    {
      remaining = 0f;
      return Present(title, message, true, actionText, actionCallback);
    }

    public void Dismiss()
    {
      if (state is NotificationState.Hidden or NotificationState.Hiding)
        return;

      enabled = true;
      hovered = false;
      state = NotificationState.Hiding;
      stateElapsed = 0f;
      canvasGroup.interactable = false;
      canvasGroup.blocksRaycasts = false;
    }

    public void ResetImmediate()
    {
      EnsureInitialized();
      state = NotificationState.Hidden;
      stateElapsed = 0f;
      remaining = 0f;
      hovered = false;
      action = null;
      enabled = false;
      if (canvasGroup != null)
      {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
      }
      if (progressFill != null)
        progressFill.fillAmount = 0f;
      if (rectTransform != null)
        rectTransform.anchoredPosition = restingPosition;
      if (gameObject.activeSelf)
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
      hovered = state == NotificationState.ToastVisible;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
      hovered = false;
    }

    private void Awake()
    {
      EnsureInitialized();
    }

    private void Update()
    {
      if (state == NotificationState.Hidden || !IsConfigured)
        return;

      float deltaTime = Time.unscaledDeltaTime;
      stateElapsed += deltaTime;
      switch (state)
      {
        case NotificationState.ShowingToast:
        case NotificationState.ShowingPersistent:
        {
          float progress = Mathf.Clamp01(stateElapsed / ShowDuration);
          float eased = 1f - Mathf.Pow(1f - progress, 3f);
          canvasGroup.alpha = eased;
          rectTransform.anchoredPosition = restingPosition + new Vector2(0f, Mathf.Lerp(HiddenOffset, 0f, eased));
          if (progress >= 1f)
          {
            state =
              state == NotificationState.ShowingToast
                ? NotificationState.ToastVisible
                : NotificationState.PersistentVisible;
            stateElapsed = 0f;
            rectTransform.anchoredPosition = restingPosition;
            if (state == NotificationState.PersistentVisible)
              enabled = false;
          }
          break;
        }
        case NotificationState.ToastVisible:
          if (!hovered)
          {
            remaining = Mathf.Max(0f, remaining - deltaTime);
            progressFill.fillAmount = remaining / toastDuration;
            if (remaining <= 0f)
              Dismiss();
          }
          break;
        case NotificationState.Hiding:
        {
          float progress = Mathf.Clamp01(stateElapsed / HideDuration);
          canvasGroup.alpha = 1f - progress;
          if (progress >= 1f)
            ResetImmediate();
          break;
        }
      }
    }

    private void OnDestroy()
    {
      if (actionButton != null)
        actionButton.onClick.RemoveListener(InvokeAction);
      if (dismissButton != null)
        dismissButton.onClick.RemoveListener(Dismiss);
    }

    private bool Present(string title, string message, bool persistent, string actionText, Action actionCallback)
    {
      EnsureInitialized();
      if (!IsConfigured)
        return false;

      titleText.text = string.IsNullOrWhiteSpace(title) ? "TUFReplay" : title.Trim();
      messageText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
      action = actionCallback;
      bool hasAction = persistent && action != null && !string.IsNullOrWhiteSpace(actionText);
      actionLabel.text = hasAction ? actionText.Trim() : string.Empty;
      actionButton.gameObject.SetActive(hasAction);
      progressTrack.SetActive(!persistent);
      Reflow(persistent, hasAction);
      stateElapsed = 0f;
      hovered = false;
      state = persistent ? NotificationState.ShowingPersistent : NotificationState.ShowingToast;
      progressFill.fillAmount = persistent ? 0f : 1f;
      canvasGroup.alpha = 0f;
      canvasGroup.interactable = true;
      canvasGroup.blocksRaycasts = true;
      rectTransform.anchoredPosition = restingPosition + new Vector2(0f, HiddenOffset);
      enabled = true;
      if (!gameObject.activeSelf)
        gameObject.SetActive(true);
      return true;
    }

    private void Reflow(bool persistent, bool hasAction)
    {
      RectTransform parentRect = rectTransform.parent as RectTransform;
      float availableCanvasWidth =
        parentRect != null && parentRect.rect.width > 0f ? parentRect.rect.width - ScreenMargin * 2f : MaxWidth;
      float maximumWidth = Mathf.Max(1f, Mathf.Min(MaxWidth, availableCanvasWidth));
      float minimumWidth = Mathf.Min(MinWidth, maximumWidth);

      Vector2 titlePreferred = titleText.GetPreferredValues(titleText.text, Mathf.Infinity, Mathf.Infinity);
      Vector2 messageUnwrapped = messageText.GetPreferredValues(messageText.text, Mathf.Infinity, Mathf.Infinity);
      float desiredWidth = Mathf.Max(
        MinWidth,
        TitleLeadingInset + titlePreferred.x + TitleTrailingInset,
        HorizontalPadding * 2f + messageUnwrapped.x
      );

      Vector2 actionPreferred = Vector2.zero;
      if (hasAction)
      {
        actionPreferred = actionLabel.GetPreferredValues(actionLabel.text, Mathf.Infinity, Mathf.Infinity);
        desiredWidth = Mathf.Max(desiredWidth, HorizontalPadding * 2f + actionPreferred.x + ActionHorizontalPadding);
      }

      float width = Mathf.Clamp(desiredWidth, minimumWidth, maximumWidth);
      float titleWidth = Mathf.Max(1f, width - TitleLeadingInset - TitleTrailingInset);
      RectTransform titleRect = titleText.rectTransform;
      titleRect.sizeDelta = new Vector2(titleWidth, HeaderMinHeight);
      titleText.overflowMode = titlePreferred.x > titleWidth ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;

      float messageWidth = Mathf.Max(1f, width - HorizontalPadding * 2f);
      Vector2 messagePreferred = messageText.GetPreferredValues(messageText.text, messageWidth, Mathf.Infinity);
      float messageTop = TopPadding + HeaderMinHeight + HeaderGap;
      float bottomReserve = persistent
        ? hasAction
          ? PersistentActionReserve
          : PersistentBottomPadding
        : ToastBottomReserve;
      float desiredHeight = messageTop + messagePreferred.y + bottomReserve;

      float availableCanvasHeight =
        parentRect != null && parentRect.rect.height > 0f
          ? parentRect.rect.height - Mathf.Max(0f, restingPosition.y) - ScreenMargin
          : MaxHeight;
      float maximumHeight = Mathf.Max(MinHeight, Mathf.Min(MaxHeight, availableCanvasHeight));
      float height = Mathf.Clamp(desiredHeight, MinHeight, maximumHeight);
      float messageHeight = Mathf.Max(0f, height - messageTop - bottomReserve);

      RectTransform messageRect = messageText.rectTransform;
      messageRect.anchoredPosition = new Vector2(HorizontalPadding, -messageTop);
      messageRect.sizeDelta = new Vector2(messageWidth, messageHeight);
      messageText.overflowMode =
        messagePreferred.y > messageHeight + 0.01f ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;

      if (hasAction)
      {
        RectTransform actionRect = actionButton.transform as RectTransform;
        if (actionRect != null)
        {
          float maximumActionWidth = Mathf.Max(1f, width - HorizontalPadding * 2f);
          float actionWidth = Mathf.Clamp(
            actionPreferred.x + ActionHorizontalPadding,
            Mathf.Min(MinActionWidth, maximumActionWidth),
            maximumActionWidth
          );
          actionRect.sizeDelta = new Vector2(actionWidth, actionRect.sizeDelta.y);
        }
      }

      rectTransform.sizeDelta = new Vector2(width, height);
    }

    private void InvokeAction()
    {
      action?.Invoke();
    }

    private void EnsureInitialized()
    {
      if (initialized)
        return;

      rectTransform = transform as RectTransform;
      if (rectTransform != null)
        restingPosition = rectTransform.anchoredPosition;
      if (actionButton != null)
      {
        actionButton.onClick.RemoveListener(InvokeAction);
        actionButton.onClick.AddListener(InvokeAction);
      }
      if (dismissButton != null)
      {
        dismissButton.onClick.RemoveListener(Dismiss);
        dismissButton.onClick.AddListener(Dismiss);
      }
      initialized = true;
    }

    private enum NotificationState
    {
      Hidden,
      ShowingToast,
      ToastVisible,
      ShowingPersistent,
      PersistentVisible,
      Hiding,
    }
  }
}
