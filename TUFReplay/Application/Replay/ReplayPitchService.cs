using System;

namespace TUFReplay.Application.Replay;

public static class ReplayPitchService
{
  public static ReplayPitchApplyResult ApplyToEditorLevelData(int pitchPercent)
  {
    try
    {
      if (ADOBase.editor?.levelData?.songSettings == null)
        return ReplayPitchApplyResult.NotReady;
      if (ADOBase.editor.levelData.pitch == pitchPercent)
        return ReplayPitchApplyResult.Skipped;

      ADOBase.editor.levelData.songSettings["pitch"] = pitchPercent;
      ADOBase.editor.UpdateSongAndLevelSettings();

      return ReplayPitchApplyResult.Applied;
    }
    catch (Exception ex)
    {
      Main.Instance?.Log(
        "[ReplayPitchService] Failed to apply replay pitch as editor change. error=" + ex.GetType().Name
      );
      return ReplayPitchApplyResult.Failed;
    }
  }

  public static int? GetEditorPitch()
  {
    try
    {
      return ADOBase.editor?.levelData?.pitch;
    }
    catch
    {
      return null;
    }
  }
}

public enum ReplayPitchApplyResult
{
  NotReady,
  Skipped,
  Applied,
  Failed,
}
