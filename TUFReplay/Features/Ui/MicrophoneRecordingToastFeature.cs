using System;
using UnityEngine;

namespace TUFReplay.Features.Ui;

public sealed class MicrophoneRecordingToastFeature
{
  private MicrophoneRecordingToastHost _host;

  public void Enable()
  {
    if (_host != null)
      return;

    GameObject gameObject = new GameObject("TUFReplay Microphone Recording Toast Host");
    UnityEngine.Object.DontDestroyOnLoad(gameObject);
    _host = gameObject.AddComponent<MicrophoneRecordingToastHost>();
    _host.Initialize();
  }

  public void Disable()
  {
    if (_host == null)
      return;

    UnityEngine.Object.Destroy(_host.gameObject);
    _host = null;
  }

  public void ShowTest()
  {
    if (_host == null)
    {
      Main.Instance?.Log("[UI Test] Microphone recording toast is not available.");
      return;
    }

    _host.Show(result =>
    {
      string message;
      switch (result.Reason)
      {
        case MicrophoneRecordingToastReason.SaveButton:
          message = "save";
          break;
        case MicrophoneRecordingToastReason.DiscardButton:
          message = "discard";
          break;
        case MicrophoneRecordingToastReason.Timeout:
          message = "discard (timeout)";
          break;
        default:
          message = result.Decision.ToString();
          break;
      }

      Main.Instance?.Log("[UI Test] Microphone recording toast: " + message);
    });
  }

  public bool Show(Action<MicrophoneRecordingToastResult> onCompleted)
  {
    return _host != null && _host.Show(onCompleted);
  }
}

public sealed class MicrophoneRecordingToastHost : MonoBehaviour
{
  private MicrophoneRecordingToastView _view;

  public void Initialize()
  {
    try
    {
      _view = MicrophoneRecordingToastView.Load();
      Main.Instance?.Log("[UI] Microphone recording toast AssetBundle loaded.");
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("UI/MicrophoneRecordingToast", exception);
    }
  }

  public bool Show(Action<MicrophoneRecordingToastResult> onCompleted)
  {
    if (_view == null)
    {
      Main.Instance?.Log("[UI Test] Microphone recording toast AssetBundle is unavailable.");
      return false;
    }

    _view.Show(onCompleted);
    return true;
  }

  private void Update()
  {
    _view?.Tick(Time.unscaledDeltaTime);
  }

  private void OnDestroy()
  {
    _view?.Dispose();
    _view = null;
  }
}
