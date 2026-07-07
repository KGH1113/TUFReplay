namespace TUFReplay.Application.Replay;

public static class ReplayRunController
{
  public static void MarkRestartPrepared(ActiveReplayContext context)
  {
    if (context == null) return;
    context.NativeInputPlayer?.ReleaseAll();
    context.RunStarted = false;
  }
}
