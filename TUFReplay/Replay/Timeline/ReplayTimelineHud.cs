using System;
using System.IO;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Timeline;
using TUFReplay.Replay.Transport;
using TUFReplay.Unity.Notifications;
using TUFReplay.Unity.ReplayTimeline;
using UnityEngine;
using UnityEngine.UI;

namespace TUFReplay.Replay.Timeline;

internal sealed class ReplayTimelineHud : MonoBehaviour
{
  private const string RuntimePrefabPath = "Assets/Prefabs/ReplayTimelineRuntime.prefab";
  private const string MicrophonePermissionWarningPrefabPath =
    "Assets/Prefabs/MicrophonePermissionWarningRuntime.prefab";
  private const int TimelineSortOrder = 32000;
  private static ReplayTimelineHud _instance;

  private AssetBundle _bundle;
  private ReplayTimelineView _view;
  private MicrophonePermissionWarningView _microphonePermissionWarningView;
  private UIFloatingPanelDragHandle _panelDragHandle;
  private string _activeRunId;
  private long _lastElapsedSecond = -1L;
  private long _lastDurationSecond = -1L;
  private bool? _lastPlaying;
  private bool? _lastInteractable;
  private bool? _lastSeekInteractable;
  private bool _isScrubbing;
  private long _scrubDurationTimeUs;
  private readonly Vector3[] _nativeControlCorners = new Vector3[4];
  private int _lastScreenWidth = -1;
  private int _lastScreenHeight = -1;

  internal static bool IsConsumingDragInput =>
    _instance != null
    && _instance._view != null
    && _instance._view.gameObject.activeInHierarchy
    && (
      _instance._panelDragHandle?.IsDragging == true || _instance._isScrubbing || _instance.IsPointerPressOverPanel()
    );

  internal static void Initialize()
  {
    Shutdown();

    try
    {
      string platformFolder = GetPlatformFolder();
      if (platformFolder == null)
      {
        Main.Instance?.Log("[ReplayTimelineHud] UI is unavailable on this platform.");
        return;
      }

      string bundlePath = Path.Combine(Main.Instance.PayloadPath, "Assets", platformFolder, "tufreplay_ui.bundle");
      AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
      if (bundle == null)
      {
        Main.Instance?.Log("[ReplayTimelineHud] UI bundle could not be loaded: " + bundlePath);
        return;
      }

      GameObject prefab = bundle.LoadAsset<GameObject>(RuntimePrefabPath);
      if (prefab == null)
      {
        Main.Instance?.Log("[ReplayTimelineHud] Runtime prefab is missing from the UI bundle.");
        bundle.Unload(true);
        return;
      }
      GameObject microphonePermissionWarningPrefab = bundle.LoadAsset<GameObject>(
        MicrophonePermissionWarningPrefabPath
      );
      if (microphonePermissionWarningPrefab == null)
      {
        Main.Instance?.Log("[ReplayTimelineHud] Microphone permission warning prefab is missing from the UI bundle.");
        bundle.Unload(true);
        return;
      }

      GameObject canvasObject = new GameObject(
        "TUFReplayTimelineCanvas",
        typeof(RectTransform),
        typeof(Canvas),
        typeof(CanvasScaler),
        typeof(GraphicRaycaster),
        typeof(ReplayTimelineHud)
      );
      DontDestroyOnLoad(canvasObject);

      Canvas canvas = canvasObject.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.overrideSorting = true;
      canvas.sortingOrder = TimelineSortOrder;

      CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920f, 1080f);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;

      ReplayTimelineHud hud = canvasObject.GetComponent<ReplayTimelineHud>();
      hud._bundle = bundle;
      _instance = hud;

      GameObject timeline = Instantiate(prefab, canvasObject.transform, false);
      timeline.name = "ReplayTimelineRuntime";
      ReplayTimelineView view = timeline.GetComponent<ReplayTimelineView>();
      if (view == null)
        throw new InvalidOperationException("ReplayTimelineRuntime.prefab has no ReplayTimelineView.");

      hud._view = view;
      hud._panelDragHandle = timeline.GetComponentInChildren<UIFloatingPanelDragHandle>(true);
      hud._view.BindTogglePlayback(ReplaySessionService.TryTogglePauseFromTimeline);
      hud._view.BindSeekRelative(ReplaySessionService.TrySeekTimelineRelative);
      hud._view.BindScrub(hud.BeginScrub, hud.PreviewScrub, hud.CommitScrub, hud.CancelScrub);
      hud._view.BindExpandDocked(hud.ExpandDockedTimeline);
      hud._view.gameObject.SetActive(false);

      GameObject microphonePermissionWarning = Instantiate(
        microphonePermissionWarningPrefab,
        canvasObject.transform,
        false
      );
      microphonePermissionWarning.name = "MicrophonePermissionWarningRuntime";
      MicrophonePermissionWarningView microphonePermissionWarningView =
        microphonePermissionWarning.GetComponent<MicrophonePermissionWarningView>();
      if (microphonePermissionWarningView == null)
        throw new InvalidOperationException(
          "MicrophonePermissionWarningRuntime.prefab has no MicrophonePermissionWarningView."
        );
      hud._microphonePermissionWarningView = microphonePermissionWarningView;
      hud._microphonePermissionWarningView.ResetImmediate();
      _instance = hud;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("ReplayTimelineHud.Initialize", exception);
      Shutdown();
    }
  }

  internal static void Shutdown()
  {
    ReplayTimelineHud instance = _instance;
    _instance = null;
    if (instance == null)
      return;

    AssetBundle bundle = instance._bundle;
    instance._bundle = null;
    instance._microphonePermissionWarningView?.ResetImmediate();
    if (instance.gameObject != null)
    {
      instance.gameObject.SetActive(false);
      Destroy(instance.gameObject);
    }
    bundle?.Unload(true);
  }

  internal static bool ShowMicrophonePermissionWarning()
  {
    MicrophonePermissionWarningView view = _instance?._microphonePermissionWarningView;
    if (view == null)
      return false;

    view.Show(MicrophonePermissionWarningView.DefaultTitle, MicrophonePermissionWarningView.DefaultMessage);
    return true;
  }

  internal static void ResetMicrophonePermissionWarning()
  {
    _instance?._microphonePermissionWarningView?.ResetImmediate();
  }

  private void Update()
  {
    if (_view == null)
      return;

    if (!ReplaySessionService.TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot))
    {
      if (ReplaySessionService.IsTimelineRestartPending)
        return;

      Hide();
      return;
    }

    if (!string.Equals(_activeRunId, snapshot.RunId, StringComparison.Ordinal))
      BeginReplay(snapshot);

    if (!_view.gameObject.activeSelf)
      _view.gameObject.SetActive(true);

    if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
    {
      _lastScreenWidth = Screen.width;
      _lastScreenHeight = Screen.height;
      UpdatePlacementReference();
    }

    if (!_isScrubbing)
    {
      float progress =
        snapshot.DurationTimeUs > 0L ? (float)((double)snapshot.ElapsedTimeUs / snapshot.DurationTimeUs) : 0f;
      _view.SetProgress(progress);

      long elapsedSecond = snapshot.ElapsedTimeUs / 1_000_000L;
      long durationSecond = snapshot.DurationTimeUs / 1_000_000L;
      if (elapsedSecond != _lastElapsedSecond || durationSecond != _lastDurationSecond)
      {
        _lastElapsedSecond = elapsedSecond;
        _lastDurationSecond = durationSecond;
        _view.SetTime(elapsedSecond, durationSecond);
      }
    }

    bool playing = !snapshot.Paused;
    if (_lastPlaying != playing)
    {
      _lastPlaying = playing;
      _view.SetPlaying(playing);
    }
    if (_lastInteractable != snapshot.CanTogglePause)
    {
      _lastInteractable = snapshot.CanTogglePause;
      _view.SetPlaybackControlInteractable(snapshot.CanTogglePause);
    }
    if (_lastSeekInteractable != snapshot.CanSeek)
    {
      _lastSeekInteractable = snapshot.CanSeek;
      _view.SetSeekControlsInteractable(snapshot.CanSeek);
    }
  }

  private void BeginReplay(ReplayTimelinePlaybackSnapshot snapshot)
  {
    _activeRunId = snapshot.RunId;
    _lastElapsedSecond = -1L;
    _lastDurationSecond = -1L;
    _lastPlaying = null;
    _lastInteractable = null;
    _lastSeekInteractable = null;
    _isScrubbing = false;
    _scrubDurationTimeUs = 0L;
    _lastScreenWidth = Screen.width;
    _lastScreenHeight = Screen.height;
    _view.ResetJudgmentFilter();
    _view.SetJudgmentMarkers(Array.Empty<ReplayJudgmentMarker>());
    if (ReplaySessionService.TryGetTimelineJudgments(out ReplayTimelineJudgmentSnapshot[] judgments))
    {
      ReplayJudgmentMarker[] markers = new ReplayJudgmentMarker[judgments.Length];
      for (int i = 0; i < judgments.Length; i++)
      {
        markers[i] = new ReplayJudgmentMarker(
          ReplaySessionService.ToNormalizedTimelineTime(judgments[i].TimeUs, snapshot.DurationTimeUs),
          ToViewJudgmentKind(judgments[i].Kind)
        );
      }
      _view.SetJudgmentMarkers(markers);
    }
    ResetPlacement();
  }

  private static ReplayJudgmentKind ToViewJudgmentKind(ReplayTimelineJudgmentKind kind)
  {
    switch (kind)
    {
      case ReplayTimelineJudgmentKind.Overload:
        return ReplayJudgmentKind.Overload;
      case ReplayTimelineJudgmentKind.TooEarly:
        return ReplayJudgmentKind.TooEarly;
      case ReplayTimelineJudgmentKind.Early:
        return ReplayJudgmentKind.Early;
      case ReplayTimelineJudgmentKind.EarlyPerfect:
        return ReplayJudgmentKind.EarlyPerfect;
      case ReplayTimelineJudgmentKind.Perfect:
        return ReplayJudgmentKind.Perfect;
      case ReplayTimelineJudgmentKind.LatePerfect:
        return ReplayJudgmentKind.LatePerfect;
      case ReplayTimelineJudgmentKind.Late:
        return ReplayJudgmentKind.Late;
      case ReplayTimelineJudgmentKind.TooLate:
        return ReplayJudgmentKind.TooLate;
      case ReplayTimelineJudgmentKind.Miss:
        return ReplayJudgmentKind.Miss;
      default:
        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }
  }

  private void Hide()
  {
    CancelScrub();
    _view.CloseJudgmentDropdown();
    _view.SetJudgmentMarkers(Array.Empty<ReplayJudgmentMarker>());
    if (_view.gameObject.activeSelf)
      _view.gameObject.SetActive(false);
    _activeRunId = null;
  }

  private void BeginScrub()
  {
    if (_isScrubbing)
      return;
    if (!ReplaySessionService.TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot))
      return;
    if (!snapshot.CanSeek || !ReplaySessionService.BeginTimelineScrub())
      return;

    _isScrubbing = true;
    _scrubDurationTimeUs = snapshot.DurationTimeUs;
  }

  private void PreviewScrub(float normalized)
  {
    if (!_isScrubbing)
      return;

    normalized = Mathf.Clamp01(normalized);
    double durationSeconds = _scrubDurationTimeUs / 1_000_000d;
    _view.SetProgress(normalized);
    _view.SetTime(durationSeconds * normalized, durationSeconds);
  }

  private void CommitScrub(float normalized)
  {
    if (!_isScrubbing)
      return;

    _isScrubbing = false;
    ReplaySessionService.CommitTimelineScrub(normalized);
    ResetTimeCache();
  }

  private void CancelScrub()
  {
    if (!_isScrubbing)
      return;

    _isScrubbing = false;
    ReplaySessionService.CancelTimelineScrub();
    ResetTimeCache();
  }

  private void ResetTimeCache()
  {
    _scrubDurationTimeUs = 0L;
    _lastElapsedSecond = -1L;
    _lastDurationSecond = -1L;
  }

  private bool IsPointerPressOverPanel()
  {
    return (Input.GetMouseButton(0) || Input.GetMouseButton(2))
      && _panelDragHandle?.ContainsScreenPoint(Input.mousePosition) == true;
  }

  private void ResetPlacement()
  {
    bool hasNativeControls = TryGetNativeControlsScreenRect(out Rect nativeControls);
    _view.ResetPlacementDocked(nativeControls, hasNativeControls);
  }

  private void UpdatePlacementReference()
  {
    bool hasNativeControls = TryGetNativeControlsScreenRect(out Rect nativeControls);
    _view.UpdatePlacementReference(nativeControls, hasNativeControls);
  }

  private void ExpandDockedTimeline()
  {
    UpdatePlacementReference();
    _view.ExpandDockedToDefault();
  }

  private bool TryGetNativeControlsScreenRect(out Rect screenRect)
  {
    screenRect = default;
    scnEditor editor = scnEditor.instance;
    if (editor == null)
      return false;

    bool hasBounds = false;
    IncludeNativeControl(editor.editorDifficultySelector?.transform as RectTransform, ref screenRect, ref hasBounds);
    IncludeNativeControl(editor.buttonNoFail?.transform as RectTransform, ref screenRect, ref hasBounds);
    IncludeNativeControl(editor.buttonAuto?.transform as RectTransform, ref screenRect, ref hasBounds);
    return hasBounds;
  }

  private void IncludeNativeControl(RectTransform rect, ref Rect screenRect, ref bool hasBounds)
  {
    if (rect == null || !rect.gameObject.activeInHierarchy)
      return;

    Canvas canvas = rect.GetComponentInParent<Canvas>();
    Camera eventCamera =
      canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    rect.GetWorldCorners(_nativeControlCorners);
    for (int cornerIndex = 0; cornerIndex < _nativeControlCorners.Length; cornerIndex++)
    {
      Vector2 point = RectTransformUtility.WorldToScreenPoint(eventCamera, _nativeControlCorners[cornerIndex]);
      if (!hasBounds)
      {
        screenRect = new Rect(point, Vector2.zero);
        hasBounds = true;
        continue;
      }

      screenRect.xMin = Mathf.Min(screenRect.xMin, point.x);
      screenRect.xMax = Mathf.Max(screenRect.xMax, point.x);
      screenRect.yMin = Mathf.Min(screenRect.yMin, point.y);
      screenRect.yMax = Mathf.Max(screenRect.yMax, point.y);
    }
  }

  private void OnDestroy()
  {
    CancelScrub();
    _microphonePermissionWarningView?.ResetImmediate();
    if (_instance == this)
      _instance = null;
  }

  private static string GetPlatformFolder()
  {
    switch (UnityEngine.Application.platform)
    {
      case RuntimePlatform.OSXPlayer:
        return "mac";
      case RuntimePlatform.WindowsPlayer:
        return "win";
      case RuntimePlatform.LinuxPlayer:
        return "linux";
      default:
        return null;
    }
  }
}
