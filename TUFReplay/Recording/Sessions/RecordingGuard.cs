using System;

namespace TUFReplay.Recording.Sessions;

public static class RecordingGuard
{
  public static bool CanRecord(out string reason)
  {
    try
    {
      // Offline renderers advance Unity on a fixed frame clock. Their simulated
      // hits are playback and must never become new activity records.
      if (UnityEngine.Time.captureFramerate > 0)
      {
        reason = "offline_render";
        return false;
      }
      if (RDC.auto)
      {
        reason = "autoplay";
        return false;
      }

      if (GCS.practiceMode)
      {
        reason = "practice_mode";
        return false;
      }

      // Segment runs are started through checkpoint-like editor state; activity recording needs them.
    }
    catch (Exception ex)
    {
      reason = "guard_error:" + ex.GetType().Name;
      return false;
    }

    reason = null;
    return true;
  }
}
