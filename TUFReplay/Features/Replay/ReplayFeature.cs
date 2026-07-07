using TUFHelper.Utils;
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

    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
  }

  public void Disable()
  {
    if (!Active) return;
    Active = false;

    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ReplaySessionService.StopActiveReplay("feature_disabled");
  }

  private static void OnPlayButtonPressed(object sender, PlayButtonEventArgs e)
  {
    ReplaySessionService.ClearActiveContextIfLevelChanged(e?.CurrentLevelInfo?.ID);
  }
}
