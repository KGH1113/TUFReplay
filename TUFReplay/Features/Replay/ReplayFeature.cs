using TUFReplay.Application.Replay;

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
    if (Active) return;
    Active = true;

  }

  public void Disable()
  {
    if (!Active) return;
    Active = false;

    ReplayPlaybackCoordinator.Shutdown();
  }
}
