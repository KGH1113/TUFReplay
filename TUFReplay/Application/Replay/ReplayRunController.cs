namespace TUFReplay.Application.Replay;

public static class ReplayRunController
{
  public static bool ShouldInitializeFromPlayerControl(ActiveReplayContext context)
  {
    return context != null && !context.RunStarted && context.Phase == ReplayPlaybackPhase.Prepared;
  }

  public static void MarkRestartPrepared(ActiveReplayContext context)
  {
    if (context == null)
      return;
    context.NativeInputPlayer?.ReleaseAll();
    context.Phase = ReplayPlaybackPhase.Prepared;
    context.RunStarted = false;
    context.WonClockStarted = false;
    context.WonClockStartedAt = 0d;
    context.WonClockStartTimeUs = 0L;
  }
}
