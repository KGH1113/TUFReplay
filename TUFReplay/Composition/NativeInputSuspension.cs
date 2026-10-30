using System;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Composition;

/// <summary>
/// Applies a native-input pause to recording and replay together. Both also check
/// <see cref="NativeInputPauseGate"/> every frame; this makes a change take effect at once
/// instead of on the next frame. Unity main thread only.
/// </summary>
internal static class NativeInputSuspension
{
  public static void Suspend(string reason)
  {
    try
    {
      RecordingSession session = RecordingFeature.Instance?.Session;
      if (session != null)
        RecordInputTracker.DrainCapturedTransitions(session);
      session?.BreakInputTimeline(reason);
      RecordInputTracker.SetCaptureWindowActive(false);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Native-input capture suspension", exception);
    }

    try
    {
      ReplaySessionService.SuspendNativeInputPlayback();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Native-input replay suspension", exception);
    }
  }

  /// <summary>Resumes replay emission once no source holds the pause.</summary>
  public static void Resume()
  {
    if (NativeInputPauseGate.IsHeld)
      return;

    try
    {
      ReplaySessionService.ResumeNativeInputPlayback();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Native-input replay resume", exception);
    }
  }
}
