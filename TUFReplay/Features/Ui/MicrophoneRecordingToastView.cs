using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Features.Ui;

internal sealed class MicrophoneRecordingToastView : IDisposable
{
  private const string BundleName = "tufreplay_ui.bundle";
  private const float DurationSeconds = 5f;

  private readonly AssetBundle _bundle;
  private readonly GameObject _root;
  private readonly RectTransform _card;
  private readonly CanvasGroup _canvasGroup;
  private readonly RectTransform _timerFill;
  private readonly Button _discardButton;
  private readonly Button _saveButton;
  private readonly MicrophoneRecordingToastPointerRelay _pointerRelay;
  private readonly Vector2 _restingPosition;
  private readonly Vector3 _restingScale;

  private Action<MicrophoneRecordingToastResult> _onCompleted;
  private float _remainingSeconds;
  private bool _hovered;
  private bool _targetVisible;
  private bool _disposed;

  private MicrophoneRecordingToastView(AssetBundle bundle, GameObject root)
  {
    _bundle = bundle;
    _root = root;
    _card = Find<RectTransform>("ToastCard");
    _canvasGroup = _card.GetComponent<CanvasGroup>();
    _timerFill = Find<RectTransform>("ToastCard/TimerTrack/TimerFill");
    _discardButton = Find<Button>("ToastCard/DiscardButton");
    _saveButton = Find<Button>("ToastCard/SaveButton");
    _restingPosition = _card.anchoredPosition;
    _restingScale = _card.localScale;

    _pointerRelay = _card.gameObject.AddComponent<MicrophoneRecordingToastPointerRelay>();
    _pointerRelay.HoverChanged = SetHovered;
    _discardButton.onClick.AddListener(Discard);
    _saveButton.onClick.AddListener(Save);

    _canvasGroup.alpha = 0f;
    _canvasGroup.interactable = false;
    _canvasGroup.blocksRaycasts = false;
    _card.gameObject.SetActive(false);
    _root.SetActive(true);
  }

  public static MicrophoneRecordingToastView Load()
  {
    string path = Path.Combine(Main.Instance.Path, "Assets", PlatformFolder(), BundleName);
    if (!File.Exists(path))
      throw new FileNotFoundException("TUFReplay UI AssetBundle was not found.", path);

    AssetBundle bundle = AssetBundle.LoadFromFile(path);
    if (bundle == null)
      throw new InvalidOperationException("Failed to load the TUFReplay UI AssetBundle: " + path);

    string prefabName = bundle
      .GetAllAssetNames()
      .FirstOrDefault(name => name.EndsWith("/microphonerecordingtoast.prefab", StringComparison.OrdinalIgnoreCase));
    GameObject prefab = string.IsNullOrWhiteSpace(prefabName) ? null : bundle.LoadAsset<GameObject>(prefabName);

    if (prefab == null)
    {
      bundle.Unload(true);
      throw new InvalidOperationException("MicrophoneRecordingToast prefab was not found in the UI AssetBundle.");
    }

    GameObject root = UnityEngine.Object.Instantiate(prefab);
    root.name = "TUFReplay Microphone Recording Toast";
    UnityEngine.Object.DontDestroyOnLoad(root);
    return new MicrophoneRecordingToastView(bundle, root);
  }

  public void Show(Action<MicrophoneRecordingToastResult> onCompleted)
  {
    if (_disposed)
      return;

    _onCompleted = onCompleted;
    _remainingSeconds = DurationSeconds;
    SetTimerProgress(1f);
    _hovered = false;
    _targetVisible = true;

    _card.gameObject.SetActive(true);
    _canvasGroup.alpha = 0f;
    _canvasGroup.interactable = true;
    _canvasGroup.blocksRaycasts = true;
    _card.anchoredPosition = _restingPosition + new Vector2(0f, -18f);
    _card.localScale = _restingScale * 0.985f;
  }

  public void Tick(float deltaTime)
  {
    if (_disposed || !_card.gameObject.activeSelf)
      return;

    if (_targetVisible && !_hovered)
    {
      _remainingSeconds = Mathf.Max(0f, _remainingSeconds - deltaTime);
      SetTimerProgress(_remainingSeconds / DurationSeconds);
      if (_remainingSeconds <= 0f)
        Complete(MicrophoneRecordingToastDecision.Discard, MicrophoneRecordingToastReason.Timeout);
    }

    float blend = 1f - Mathf.Exp(-12f * deltaTime);
    float targetAlpha = _targetVisible ? 1f : 0f;
    Vector2 targetPosition = _restingPosition + (_targetVisible ? Vector2.zero : new Vector2(0f, -8f));
    Vector3 targetScale = _restingScale * (_targetVisible ? 1f : 0.992f);

    _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, blend);
    _card.anchoredPosition = Vector2.Lerp(_card.anchoredPosition, targetPosition, blend);
    _card.localScale = Vector3.Lerp(_card.localScale, targetScale, blend);

    if (!_targetVisible && _canvasGroup.alpha < 0.01f)
      _card.gameObject.SetActive(false);
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    _onCompleted = null;
    _discardButton.onClick.RemoveListener(Discard);
    _saveButton.onClick.RemoveListener(Save);
    _pointerRelay.HoverChanged = null;

    if (_root != null)
      UnityEngine.Object.Destroy(_root);

    _bundle?.Unload(false);
  }

  private void Save()
  {
    Complete(MicrophoneRecordingToastDecision.Save, MicrophoneRecordingToastReason.SaveButton);
  }

  private void Discard()
  {
    Complete(MicrophoneRecordingToastDecision.Discard, MicrophoneRecordingToastReason.DiscardButton);
  }

  private void Complete(MicrophoneRecordingToastDecision decision, MicrophoneRecordingToastReason reason)
  {
    if (!_targetVisible)
      return;

    _targetVisible = false;
    _hovered = false;
    _canvasGroup.interactable = false;
    _canvasGroup.blocksRaycasts = false;

    Action<MicrophoneRecordingToastResult> callback = _onCompleted;
    _onCompleted = null;
    callback?.Invoke(new MicrophoneRecordingToastResult(decision, reason));
  }

  private void SetHovered(bool hovered)
  {
    _hovered = _targetVisible && hovered;
  }

  private void SetTimerProgress(float progress)
  {
    _timerFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
  }

  private T Find<T>(string path)
    where T : Component
  {
    Transform transform = _root.transform.Find(path);
    if (transform == null)
      throw new InvalidOperationException("Missing toast object: " + path);

    T component = transform.GetComponent<T>();
    if (component == null)
      throw new InvalidOperationException("Missing " + typeof(T).Name + " on toast object: " + path);

    return component;
  }

  private static string PlatformFolder()
  {
    switch (UnityEngine.Application.platform)
    {
      case RuntimePlatform.OSXPlayer:
      case RuntimePlatform.OSXEditor:
        return "mac";
      case RuntimePlatform.WindowsPlayer:
      case RuntimePlatform.WindowsEditor:
        return "win";
      case RuntimePlatform.LinuxPlayer:
      case RuntimePlatform.LinuxEditor:
        return "linux";
      default:
        throw new PlatformNotSupportedException(
          "Unsupported TUFReplay UI platform: " + UnityEngine.Application.platform
        );
    }
  }
}
