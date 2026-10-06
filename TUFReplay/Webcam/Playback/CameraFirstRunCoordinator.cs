using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using TUFReplay.Composition;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Timeline;
using TUFReplay.Unity.CameraSetup;
using TUFReplay.Webcam.Models;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

internal static class CameraFirstRunCoordinator
{
  private static bool _confirmed;
  private static CameraSetupView _view;
  private static Action _resume;
  private static bool _cancelled;
  private static bool _isVisible;
  private static TUFReplay.Webcam.Ipc.WebcamStateDto _state;
  private static float _nextStateRefresh;
  public static bool IsVisible => _isVisible;
  private static bool LiveCameraRequested => Main.Settings?.WebcamEnabled == true && Main.Settings.WebcamLiveVisible;

  public static bool InterceptEditorPlay(scnEditor editor)
  {
    if (!Eligible())
      return false;
    return Show(
      () =>
      {
        if (editor != null && editor.initialized && !editor.isLoading)
          editor.Play();
      },
      true
    );
  }

  public static IEnumerator GateCountdown(IEnumerator original)
  {
    try
    {
      if (Eligible() && Show(null, false))
      {
        while (_isVisible)
          yield return null;
        if (_cancelled)
          yield break;
      }
      while (original.MoveNext())
        yield return original.Current;
    }
    finally
    {
      (original as IDisposable)?.Dispose();
    }
  }

  private static bool Eligible() =>
    !_confirmed
    && LiveCameraRequested
    && FeatureRegistry.WebcamRecording?.Supported == true
    && !ReplaySessionService.HasActiveContext
    && FeatureRegistry.MicrophoneCalibration?.IsCalibrationLevel() != true
    && (ADOBase.isLevelEditor || (ADOBase.isScnGame && !ADOBase.isOfficialLevel));

  private static bool Show(Action resume, bool canCancel)
  {
    if (_isVisible)
      return true;
    try
    {
      if (_view == null)
      {
        GameObject instance = ReplayTimelineHud.InstantiateOverlay("Assets/Prefabs/CameraSetupRuntime.prefab");
        _view = instance?.GetComponent<CameraSetupView>();
      }
      if (_view?.IsConfigured != true)
      {
        ReplayTimelineHud.ShowNotificationToast(
          "Camera setup is unavailable",
          "Rebuild the TUFReplay UI bundle and reinstall the mod. You can still change camera settings in the companion."
        );
        _confirmed = true;
        return false;
      }
      _resume = resume;
      _cancelled = false;
      _isVisible = true;
      _state = null;
      _view.transform.SetAsLastSibling();
      _view.Show(
        Application.systemLanguage == SystemLanguage.Korean,
        canCancel,
        enabled => Update(new JObject { ["enabled"] = enabled }),
        visible => Update(new JObject { ["liveVisible"] = visible }),
        Continue,
        WithoutCamera,
        Cancel,
        () => Update(new JObject { ["enabled"] = true })
      );
      Tick();
      return true;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Camera/Setup", exception);
      _isVisible = false;
      return false;
    }
  }

  public static void Tick()
  {
    if (_isVisible && !LiveCameraRequested)
      Continue();
    CameraRenderDiagnostics.Tick(_isVisible);
    CameraLivePreview.Tick(_isVisible);
    if (!_isVisible || _view == null)
      return;
    var feature = FeatureRegistry.WebcamRecording;
    if (_state == null || Time.unscaledTime >= _nextStateRefresh)
    {
      _state = feature.GetState();
      _nextStateRefresh = Time.unscaledTime + 0.2f;
    }
    var state = _state;
    string device = null;
    foreach (var item in state.Devices)
      if (state.SelectedDeviceId == item.Id || state.SelectedDeviceId == null)
      {
        device = item.Name;
        break;
      }
    WebcamCropRect crop = WebcamCropRect.Get(Main.Settings);
    _view.SetState(
      state.Enabled,
      state.LiveVisible,
      feature.IsReady,
      state.Supported,
      device,
      state.Error,
      feature.IsReady ? CameraLivePreview.Texture : null,
      state.Mirror,
      new Rect((float)crop.X, (float)crop.Y, (float)crop.Width, (float)crop.Height),
      state.FlipVertical
    );
  }

  private static bool Update(JObject values)
  {
    if (!WebcamSettingsPatch.TryParse(values, out var patch))
      return false;
    if (FeatureRegistry.WebcamRecording.TryUpdate(patch, out string message))
    {
      _state = null;
      return true;
    }
    ReplayTimelineHud.ShowNotificationToast("Camera settings could not be saved", message);
    return false;
  }

  private static void Continue()
  {
    if (LiveCameraRequested && FeatureRegistry.WebcamRecording?.IsReady != true)
      return;
    // Skipping a hidden camera must not confirm a future visible-camera run.
    _confirmed = LiveCameraRequested;
    _isVisible = false;
    _view.Hide();
    Action resume = _resume;
    _resume = null;
    // Resume on the next mod update, after the modal's click has been released.
    if (resume != null)
      TUFReplay.Shared.Unity.UnityMainThread.Post(resume);
  }

  private static void WithoutCamera()
  {
    if (Update(new JObject { ["enabled"] = false }))
      Continue();
  }

  private static void Cancel()
  {
    _cancelled = true;
    _isVisible = false;
    _resume = null;
    _view.Hide();
  }

  public static void Shutdown()
  {
    _cancelled = true;
    _isVisible = false;
    _resume = null;
    if (_view != null)
    {
      _view.Hide();
      UnityEngine.Object.Destroy(_view.gameObject);
    }
    _view = null;
    CameraLivePreview.Shutdown();
    CameraRenderDiagnostics.Shutdown();
  }
}
