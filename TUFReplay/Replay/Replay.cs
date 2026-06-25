using JALib.Core;

namespace TUFReplay.Replay;

public class Replay : Feature
{
  public static Replay Instance;

  public Replay() : base(
    Main.Instance,
    nameof(Replay),
    true,
    typeof(ReplayInputPatches)
  )
  {
    Instance = this;
  }

  protected override void OnDisable()
  {
    ReplayService.StopActiveReplay("feature_disabled");
  }
}
