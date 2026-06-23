using System;

namespace TUFReplay.Recording;

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

      if (GCS.useNoFail || (ADOBase.controller != null && ADOBase.controller.noFail))
      {
        reason = "no_fail";
        return false;
      }

      if (GCS.practiceMode)
      {
        reason = "practice_mode";
        return false;
      }

      if (GCS.checkpointNum > 0 || (ADOBase.controller != null && ADOBase.controller.startedFromCheckpoint))
      {
        reason = "checkpoint_start";
        return false;
      }
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
