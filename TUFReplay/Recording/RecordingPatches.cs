using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;
using SkyHook;

namespace TUFReplay.Recording;

public static class RecordingPatches
{
  private static int _skyHookPrefixLogged; // TODO: Delete this

  [JAPatch(
    typeof(SkyHookManager),
    "HookCallback",
    PatchType.Prefix,
    true,
    ArgumentTypesType = new[] { typeof(SkyHookEvent) },
    Priority = int.MaxValue
  )]
  private static void OnSkyHookCallbackPrefix(SkyHookEvent ev)
  {
    if (System.Threading.Interlocked.Exchange(ref _skyHookPrefixLogged, 1) == 0) // TODO: Delete this
    {
      Main.Instance.Log("[Recording/InputDebug] SkyHook HookCallback prefix fired.");
    }
    RecordInputTracker.EnqueueRawAsync(ev);
  }

  [JAPatch(
    typeof(AsyncInputManager),
    "Update",
    PatchType.Postfix,
    true
  )]
  private static void OnAsyncInputManagerUpdatePostfix()
  {
    RecordingSession session = Recording.Instance?.Session;
    if (session == null || !session.IsRecording) return;

    RecordInputTracker.DrainTo(session);
  }

  [JAPatch(
    typeof(StateBehaviour),
    "ChangeState",
    PatchType.Postfix,
    true,
    ArgumentTypesType = new[] { typeof(Enum) }
  )]
  private static void OnChangeState(Enum newState)
  {
    Recording recording = Recording.Instance;
    if (recording == null) return;

    switch ((States)newState)
    {
      case States.Countdown:
        if (!recording.Session.IsRecording) return;

        if (!RecordingGuard.CanRecord(out string reason))
        {
          recording.Session.Stop();
          Main.Instance.Log("[Recording] Input capture skipped. reason=" + reason);
          return;
        }

        recording.Session.StartInputCapture();
        break;

      case States.PlayerControl:
        Recording.Instance.Session.MarkGameplayStarted();
        break;

      case States.Won:
        Recording.Instance.OnClearReached();
        break;

      case States.Fail:
      case States.Fail2:
        Recording.Instance.OnRunFailed();
        break;
    }
  }

  [JAPatch(
    typeof(scnEditor),
    "SwitchToEditMode",
    PatchType.Postfix,
    true,
    ArgumentTypesType = new[] { typeof(bool) }
  )]
  private static void OnSwitchToEditModePostfix(bool clsToEditor)
  {
    Recording.Instance?.OnReturnedToEditor();
  }
}
