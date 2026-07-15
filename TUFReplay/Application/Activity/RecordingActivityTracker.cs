using System;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Adofai;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Activity;

public sealed class RecordingActivityTracker
{
  public string AppSessionId { get; private set; }
  public string LevelSessionId { get; private set; }
  public int? TufLevelId { get; private set; }
  public string LevelPath { get; private set; }

  public void StartAppSession()
  {
    if (AppSessionId != null)
      return;

    AppSessionId = Guid.NewGuid().ToString("N");
    DateTimeOffset now = DateTimeOffset.Now;
    AppSessionRepository.Save(
      new AppSession
      {
        Id = AppSessionId,
        StartedAtUtc = now.UtcDateTime.ToString("O"),
        RecorderTimeZoneId = TimeZoneInfo.Local.Id,
        RecorderUtcOffsetMinutes = (int)now.Offset.TotalMinutes,
      }
    );
  }

  public void StopAppSession()
  {
    CloseLevel();

    if (AppSessionId == null)
      return;

    AppSessionRepository.CloseOrDeleteIfEmpty(AppSessionId, DateTime.UtcNow.ToString("O"));
    AppSessionId = null;
  }

  public void OpenLevel(string levelPath, int? tufLevelId, int levelTileCount)
  {
    StartAppSession();

    if (LevelSessionId != null && string.Equals(LevelPath, levelPath, StringComparison.OrdinalIgnoreCase))
      return;

    CloseLevel();

    LevelSessionId = Guid.NewGuid().ToString("N");
    TufLevelId = tufLevelId;
    LevelPath = levelPath;
    bool metadataAvailable = AdofaiLevelMetadataReader.TryRead(levelPath, out LevelMetadataSnapshot metadata);
    LevelSessionRepository.Save(
      new LevelSession
      {
        Id = LevelSessionId,
        AppSessionId = AppSessionId,
        TufLevelId = tufLevelId,
        LevelPath = levelPath,
        OpenedAtUtc = DateTime.UtcNow.ToString("O"),
        LevelTileCount = levelTileCount,
        Song = metadata?.Song,
        Author = metadata?.Author,
        Artist = metadata?.Artist,
        MetadataState = metadataAvailable ? LevelMetadataState.Captured : LevelMetadataState.Unavailable,
      }
    );
  }

  public void CloseLevel()
  {
    if (LevelSessionId == null)
      return;

    LevelSessionRepository.CloseOrDeleteIfEmpty(LevelSessionId, DateTime.UtcNow.ToString("O"));
    LevelSessionId = null;
    TufLevelId = null;
    LevelPath = null;
  }

  public RunRecord CreateRunDraft(RecordedRunPayload data, int startTile, int levelTileCount)
  {
    if (AppSessionId == null || LevelSessionId == null || data == null)
      return null;

    int runIndex = RunRepository.GetNextRunIndex(LevelSessionId);

    return new RunRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      AppSessionId = AppSessionId,
      LevelSessionId = LevelSessionId,
      TufLevelId = data.TufLevelId,
      RunIndex = runIndex,
      SegmentGroupIndex = 0,
      StartedAtUtc = data.StartedAtUtc,
      LevelTileCount = levelTileCount,
      StartTile = startTile,
      NoFailMode = data.NoFailMode,
      GameplayStartSongPosition = data.GameplayStartSongPosition,
      LevelPitchPercent = data.LevelPitchPercent,
      EffectivePitch = data.EffectivePitch,
      InputCount = data.Inputs.Count,
      HitContextCount = data.HitContexts.Count,
      GameplayHash = data.GameplayHash == null ? null : (byte[])data.GameplayHash.Clone(),
      GameplayHashVersion = data.GameplayHashVersion,
      MetaJson = data.ToActivityMetaJson(),
    };
  }

  public void SaveRun(RunRecord run)
  {
    if (run == null)
      return;
    RunRepository.Save(run);
  }
}
