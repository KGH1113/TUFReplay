using JALib.Core;
using TUFHelper.Utils;
using TUFReplay.Application.Replay;
using TUFReplay.Features.Gameplay;

namespace TUFReplay.Features.Replay;

public class ReplayFeature : Feature
{
  public static ReplayFeature Instance;

  public ReplayFeature() : base(
    Main.Instance,
    nameof(ReplayFeature),
    true,
    typeof(ReplayInputPatches)
  )
  {
    Instance = this;
    AddMultiFeatures(typeof(GameplayPatches));
  }

  protected override void OnEnable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
  }

  protected override void OnDisable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ReplaySessionService.StopActiveReplay("feature_disabled");
  }

  private static void OnPlayButtonPressed(object sender, PlayButtonEventArgs e)
  {
    ReplaySessionService.ClearActiveContextIfLevelChanged(e?.CurrentLevelInfo?.ID);
  }
}
