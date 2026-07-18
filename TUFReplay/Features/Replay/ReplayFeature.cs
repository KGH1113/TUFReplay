using TUFReplay.Application.Replay;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Features.Replay;

public class ReplayFeature
{
  public static ReplayFeature Instance;
  public bool Active { get; private set; }
  private ReplayMicrophonePlaybackTicker _microphoneTicker;

  public ReplayFeature()
  {
    Instance = this;
  }

  public void Enable()
  {
    if (Active)
      return;
    Active = true;
    ReplayMicrophonePlaybackFiles.Initialize();
    var gameObject = new GameObject("TUFReplay Replay Microphone Ticker");
    Object.DontDestroyOnLoad(gameObject);
    _microphoneTicker = gameObject.AddComponent<ReplayMicrophonePlaybackTicker>();
  }

  public void Disable()
  {
    if (!Active)
      return;
    Active = false;

    ReplayPlaybackCoordinator.Shutdown();
    ReplayLevelFilePickerCoordinator.Shutdown();
    if (_microphoneTicker != null)
      Object.Destroy(_microphoneTicker.gameObject);
    _microphoneTicker = null;
  }
}

public sealed class ReplayMicrophonePlaybackTicker : MonoBehaviour
{
  private void Update() => ReplaySessionService.TickMicrophonePlayback();
}
