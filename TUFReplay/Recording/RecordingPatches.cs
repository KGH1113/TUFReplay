using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;

namespace TUFReplay.Recording;

public static class RecordingPatches
{
  [JAPatch(typeof(StateBehaviour), "ChangeState", PatchType.Postfix, true, ArgumentTypesType = new[] { typeof(Enum) })]
  private static void OnChangeState(Enum newState)
  {
    if (Recording.Instance == null) return;

    switch ((States)newState)
    {
      case States.PlayerControl:
        if (!Recording.Instance.Session.IsRecording) return;

        if (!RecordingGuard.CanRecord(out string reason))
        {
          Recording.Instance.Session.Stop();
          Main.Instance.Log("[Recording] Input capture skipped. reason=" + reason);
          return;
        }

        Recording.Instance.Session.StartInputCapture();
        break;

      case States.Won:
        RDInputRecorder.FlushHeldReleases(Recording.Instance.Session, "State.Won.FlushHeld");
        Recording.Instance.Session.StopInputCapture("won");
        Recording.Instance.OnRunCleared();
        break;

      case States.Fail:
      case States.Fail2:
        Recording.Instance.Session.StopInputCapture("failed");
        Recording.Instance.OnRunFailed();
        break;
    }
  }
}
