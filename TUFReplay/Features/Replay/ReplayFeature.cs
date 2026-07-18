using TUFReplay.Application.Replay;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Features.Replay;

public class ReplayFeature
{
  public static ReplayFeature Instance;
  public bool Active { get; private set; }

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
  }

  public void Disable()
  {
    if (!Active)
      return;
    Active = false;

    ReplayPlaybackCoordinator.Shutdown();
    ReplayLevelFilePickerCoordinator.Shutdown();
  }
}
