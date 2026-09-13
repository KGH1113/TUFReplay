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
    private const float HiddenOffset = -8f;
    private const float ToastHeight = 154f;
    private const float PersistentHeight = 204f;

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
      rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, persistent ? PersistentHeight : ToastHeight);
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
