using System;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Adofai;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Application.Activity;

public sealed class RecordingActivityTracker
{
  public string AppSessionId { get; private set; }
  public string LevelSessionId { get; private set; }
  public int? TufLevelId { get; private set; }
  public string LevelPath { get; private set; }
  private byte[] _gameplayHash;
  private int? _gameplayHashVersion;

  public bool StartAppSession()
  {
    if (AppSessionId != null)
      return true;

    string appSessionId = Guid.NewGuid().ToString("N");
    DateTimeOffset now = DateTimeOffset.Now;
    try
    {
      AppSessionRepository.Save(
        new AppSession
        {
          Id = appSessionId,
          StartedAtUtc = now.UtcDateTime.ToString("O"),
          RecorderTimeZoneId = TimeZoneInfo.Local.Id,
          RecorderUtcOffsetMinutes = (int)now.Offset.TotalMinutes,
        }
      );
      AppSessionId = appSessionId;
      return true;
    }
    catch (SqliteException exception) when (DatabaseStore.IsTransientLock(exception))
    {
      return false;
    }
  }

  public void StopAppSession()
  {
    CloseLevel();

    if (AppSessionId == null)
      return;

    AppSessionRepository.CloseOrDeleteIfEmpty(AppSessionId, DateTime.UtcNow.ToString("O"));
    AppSessionId = null;
  }

  public bool OpenLevel(
    string levelPath,
    int? tufLevelId,
    int levelTileCount,
    byte[] gameplayHash,
    int? gameplayHashVersion,
    byte[] legacyGameplayHash,
    int? legacyGameplayHashVersion
  )
  {
    if (!StartAppSession())
      return false;

    if (
      LevelSessionId != null
      && LevelPathIdentity.Equals(LevelPath, levelPath)
      && GameplayChartHash.IsSupported(_gameplayHashVersion, _gameplayHash)
      && GameplayChartHash.IsSupported(gameplayHashVersion, gameplayHash)
      && GameplayChartHash.Equals(_gameplayHash, gameplayHash)
    )
    {
      LevelSession currentSession = LevelSessionRepository.Get(LevelSessionId);
      LevelRepository.MergeLegacyIdentity(currentSession?.LevelId, legacyGameplayHash, legacyGameplayHashVersion);
      return true;
    }

    CloseLevel();

    string levelSessionId = Guid.NewGuid().ToString("N");
    bool metadataAvailable = AdofaiLevelMetadataReader.TryRead(levelPath, out LevelMetadataSnapshot metadata);
    string openedAtUtc = DateTime.UtcNow.ToString("O");
    var level = new LevelRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      SourceKind = tufLevelId.HasValue ? LevelSourceKind.Tuf : LevelSourceKind.Local,
      TufLevelId = tufLevelId,
      LevelPath = levelPath,
      LevelTileCount = levelTileCount,
      GameplayHash = gameplayHash == null ? null : (byte[])gameplayHash.Clone(),
      GameplayHashVersion = gameplayHashVersion,
      Song = metadata?.Song,
      Author = metadata?.Author,
      Artist = metadata?.Artist,
      MetadataState = metadataAvailable ? LevelMetadataState.Captured : LevelMetadataState.Unavailable,
      FirstSeenAtUtc = openedAtUtc,
      LastSeenAtUtc = openedAtUtc,
    };
    var levelSession = new LevelSession
    {
      Id = levelSessionId,
      LevelId = LevelRepository.ResolveOrCreate(level, legacyGameplayHash, legacyGameplayHashVersion),
      AppSessionId = AppSessionId,
      OpenedAtUtc = openedAtUtc,
    };
    LevelSessionRepository.Save(levelSession);
    LevelSessionId = levelSessionId;
    TufLevelId = tufLevelId;
    LevelPath = levelPath;
    _gameplayHash = gameplayHash == null ? null : (byte[])gameplayHash.Clone();
    _gameplayHashVersion = gameplayHashVersion;
    return true;
  }

  public void CloseLevel()
  {
    if (LevelSessionId == null)
      return;

    LevelSessionRepository.CloseOrDeleteIfEmpty(LevelSessionId, DateTime.UtcNow.ToString("O"));
    LevelSessionId = null;
    TufLevelId = null;
    LevelPath = null;
    _gameplayHash = null;
    _gameplayHashVersion = null;
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
