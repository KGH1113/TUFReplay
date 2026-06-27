using JALib.Core;
using TUFHelper.Utils;

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

  protected override void OnEnable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
  }

  protected override void OnDisable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ReplayService.StopActiveReplay("feature_disabled");
  }

  private static void OnPlayButtonPressed(object sender, PlayButtonEventArgs e)
  {
    ReplayService.ClearActiveContextIfLevelChanged(e?.CurrentLevelInfo?.ID);
  }
}
