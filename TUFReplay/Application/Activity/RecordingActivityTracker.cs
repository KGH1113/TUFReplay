using System;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Activity;

public sealed class RecordingActivityTracker
{
  public string AppSessionId { get; private set; }
  public string LevelSessionId { get; private set; }
  public int? TufLevelId { get; private set; }

  public void StartAppSession()
  {
    if (AppSessionId != null) return;

    AppSessionId = Guid.NewGuid().ToString("N");
    AppSessionRepository.Save(new AppSession
    {
      Id = AppSessionId,
      StartedAtUtc = DateTime.UtcNow.ToString("O")
    });
  }

  public void StopAppSession()
  {
    CloseLevel();

    if (AppSessionId == null) return;

    AppSessionRepository.Close(AppSessionId, DateTime.UtcNow.ToString("O"));
    AppSessionId = null;
  }

  public void OpenLevel(int tufLevelId, int levelTileCount)
  {
    StartAppSession();

    if (LevelSessionId != null && TufLevelId == tufLevelId) return;

    CloseLevel();

    LevelSessionId = Guid.NewGuid().ToString("N");
    TufLevelId = tufLevelId;
    LevelSessionRepository.Save(new LevelSession
    {
      Id = LevelSessionId,
      AppSessionId = AppSessionId,
      TufLevelId = tufLevelId,
      OpenedAtUtc = DateTime.UtcNow.ToString("O"),
      LevelTileCount = levelTileCount
    });
  }

  public void CloseLevel()
  {
    if (LevelSessionId == null) return;

    LevelSessionRepository.Close(LevelSessionId, DateTime.UtcNow.ToString("O"));
    LevelSessionId = null;
    TufLevelId = null;
  }

  public RunRecord CreateRunDraft(RecordedRunPayload data, int startTile, int levelTileCount)
  {
    if (AppSessionId == null || LevelSessionId == null || data == null) return null;

    RunRecord lastRun = RunRepository.GetLastByLevelSession(LevelSessionId);
    int runIndex = lastRun == null ? 0 : lastRun.RunIndex + 1;
    int segmentGroupIndex = RunRepository.NextSegmentGroupIndex(LevelSessionId, startTile);

    return new RunRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      AppSessionId = AppSessionId,
      LevelSessionId = LevelSessionId,
      TufLevelId = data.LevelId,
      RunIndex = runIndex,
      SegmentGroupIndex = segmentGroupIndex,
      StartedAtUtc = data.StartedAtUtc,
      LevelTileCount = levelTileCount,
      StartTile = startTile,
      NoFailMode = data.NoFailMode,
      GameplayStartSongPosition = data.GameplayStartSongPosition,
      LevelPitchPercent = data.LevelPitchPercent,
      EffectivePitch = data.EffectivePitch,
      InputCount = data.Inputs.Count,
      HitContextCount = data.HitContexts.Count,
      MetaJson = data.ToActivityMetaJson()
    };
  }

  public void SaveRun(RunRecord run)
  {
    if (run == null) return;
    RunRepository.Save(run);
  }
}
