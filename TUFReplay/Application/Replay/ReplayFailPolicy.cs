namespace TUFReplay.Application.Replay;

public static class ReplayFailPolicy
{
  public static bool ShouldUseReplayNoFail(ActiveReplayContext context, bool usesHitContextPlayback)
  {
    return usesHitContextPlayback || context?.Meta?.noFailMode == true;
  }

  public static void ApplyReplayNoFail(bool enabled)
  {
    if (ADOBase.controller == null) return;

    ADOBase.controller.noFail = enabled;
    ADOBase.controller.noFailInfiniteMargin = false;

    if (enabled)
    {
      ADOBase.controller.freeroamInvulnerability = true;
    }
    else
    {
      ADOBase.controller.freeroamInvulnerability = Persistence.freeroamInvulnerability;
    }
  }
}
