using System;

namespace TUFReplay.Application.Recording;

public static class RecordingGuard
{
  public static bool CanRecord(out string reason)
  {
    try
    {
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
