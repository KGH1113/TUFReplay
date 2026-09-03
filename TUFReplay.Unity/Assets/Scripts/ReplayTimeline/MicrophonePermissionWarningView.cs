using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TUFReplay.Unity.Notifications
{
  [DisallowMultipleComponent]
  public sealed class MicrophonePermissionWarningView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
  {
    public const string DefaultTitle = "Microphone access is off";
    public const string DefaultMessage =
      "Enable \u201cTUFReplay Microphone Capture\u201d in System Settings \u2192 Privacy & Security \u2192 Microphone. "
      + "This run will continue without microphone audio.";

    private const float VisibleDuration = 5f;
    private const float ShowDuration = 0.16f;
    private const float HideDuration = 0.12f;
    private const float HiddenOffset = -8f;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text messageText;

    [SerializeField]
    private Image progressFill;

    [SerializeField]
    private Button dismissButton;

    private RectTransform rectTransform;
    private Vector2 restingPosition;
    private ToastState state;
    private float stateElapsed;
    private float remaining;
    private bool hovered;
    private bool initialized;

    public bool IsConfigured =>
      canvasGroup != null && titleText != null && messageText != null && progressFill != null && dismissButton != null;

    public void ConfigureReferences(
      CanvasGroup configuredCanvasGroup,
      TMP_Text configuredTitleText,
      TMP_Text configuredMessageText,
      Image configuredProgressFill,
      Button configuredDismissButton
    )
    {
      canvasGroup = configuredCanvasGroup;
      titleText = configuredTitleText;
      messageText = configuredMessageText;
      progressFill = configuredProgressFill;
      dismissButton = configuredDismissButton;
      initialized = false;
    }

    public void Show(string title = null, string message = null)
    {
      EnsureInitialized();
      if (!IsConfigured)
        return;

      if (state == ToastState.Hidden)
        restingPosition = rectTransform.anchoredPosition;

      titleText.text = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim();
      messageText.text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message.Trim();
      remaining = VisibleDuration;
      stateElapsed = 0f;
      hovered = false;
      state = ToastState.Showing;
      progressFill.fillAmount = 1f;
      canvasGroup.alpha = 0f;
      canvasGroup.interactable = true;
      canvasGroup.blocksRaycasts = true;
      rectTransform.anchoredPosition = restingPosition + new Vector2(0f, HiddenOffset);
      if (!gameObject.activeSelf)
        gameObject.SetActive(true);
    }

    public void Dismiss()
    {
      if (state is ToastState.Hidden or ToastState.Hiding)
        return;

      hovered = false;
      state = ToastState.Hiding;
      stateElapsed = 0f;
      canvasGroup.interactable = false;
      canvasGroup.blocksRaycasts = false;
    }

    public void ResetImmediate()
    {
      EnsureInitialized();
      state = ToastState.Hidden;
      stateElapsed = 0f;
      remaining = 0f;
      hovered = false;
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
      hovered = state == ToastState.Visible;
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
      if (state == ToastState.Hidden || !IsConfigured)
        return;

      float deltaTime = Time.unscaledDeltaTime;
      stateElapsed += deltaTime;
      switch (state)
      {
        case ToastState.Showing:
        {
          float progress = Mathf.Clamp01(stateElapsed / ShowDuration);
          float eased = 1f - Mathf.Pow(1f - progress, 3f);
          canvasGroup.alpha = eased;
          rectTransform.anchoredPosition = restingPosition + new Vector2(0f, Mathf.Lerp(HiddenOffset, 0f, eased));
          if (progress >= 1f)
          {
            state = ToastState.Visible;
            stateElapsed = 0f;
          }
          break;
        }
        case ToastState.Visible:
          rectTransform.anchoredPosition = restingPosition;
          if (!hovered)
          {
            remaining = Mathf.Max(0f, remaining - deltaTime);
            progressFill.fillAmount = remaining / VisibleDuration;
            if (remaining <= 0f)
              Dismiss();
          }
          break;
        case ToastState.Hiding:
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
      if (dismissButton != null)
        dismissButton.onClick.RemoveListener(Dismiss);
    }

    private void EnsureInitialized()
    {
      if (initialized)
        return;

      rectTransform = transform as RectTransform;
      if (rectTransform != null)
        restingPosition = rectTransform.anchoredPosition;
      if (dismissButton != null)
      {
        dismissButton.onClick.RemoveListener(Dismiss);
        dismissButton.onClick.AddListener(Dismiss);
      }
      initialized = true;
    }

    private enum ToastState
    {
      Hidden,
      Showing,
      Visible,
      Hiding,
    }
  }
}
