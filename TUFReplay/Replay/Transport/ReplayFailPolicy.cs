using TUFReplay.Replay.Sessions;

namespace TUFReplay.Replay.Transport;

public static class ReplayFailPolicy
{
  public static bool ShouldUseReplayNoFail(ActiveReplayContext context)
  {
    return context?.NoFailMode == true;
  }

  public static void ApplyReplayNoFail(bool enabled)
  {
    if (ADOBase.controller == null)
      return;

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

    SyncEditorIndicator(enabled);
  }

  private static void SyncEditorIndicator(bool enabled)
  {
    if (scnEditor.instance?.buttonNoFail == null)
      return;

    UnityEngine.UI.Image image = scnEditor.instance.buttonNoFail.GetComponent<UnityEngine.UI.Image>();
    if (image == null)
      return;

    image.color = enabled ? UnityEngine.Color.white : new UnityEngine.Color(36f / 85f, 36f / 85f, 36f / 85f);
  }
}
