using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Threading;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Activity.Tracking;

public sealed class RecordingActivityTracker
{
  public string AppSessionId { get; private set; }
  public string LevelSessionId { get; private set; }
  public int? TufLevelId { get; private set; }
  public string LevelPath { get; private set; }
  private byte[] _gameplayHash;
  private int? _gameplayHashVersion;
  private int _nextRunIndex;
  private Action _ensureApp;
  private Action _ensureLevel;
  private readonly SerialBackgroundQueue _writes = new SerialBackgroundQueue(exception =>
    Main.Instance?.LogException("Recording/ActivityPersistence", exception)
  );

  public Task PendingWrites => _writes.Completion;

  public bool StartAppSession()
  {
    if (AppSessionId != null)
      return true;

    string appSessionId = Guid.NewGuid().ToString("N");
    DateTimeOffset now = DateTimeOffset.Now;
    var session = new AppSession
    {
      Id = appSessionId,
      StartedAtUtc = now.UtcDateTime.ToString("O"),
      RecorderTimeZoneId = TimeZoneInfo.Local.Id,
      RecorderUtcOffsetMinutes = (int)now.Offset.TotalMinutes,
    };
    bool saved = false;
    Action ensure = () =>
    {
      if (saved)
        return;
      AppSessionRepository.Save(session);
      saved = true;
    };
    _ensureApp = ensure;
    _writes.Enqueue(() => RetryLockedWrite(ensure));
    AppSessionId = appSessionId;
    return true;
  }

  public void StopAppSession()
  {
    CloseLevel();

    if (AppSessionId == null)
      return;

    string id = AppSessionId;
    string endedAtUtc = DateTime.UtcNow.ToString("O");
    Action ensureApp = _ensureApp;
    _writes.Enqueue(() =>
      RetryLockedWrite(() =>
      {
        ensureApp?.Invoke();
        AppSessionRepository.CloseOrDeleteIfEmpty(id, endedAtUtc);
        ActivityChanges.Notify();
      })
    );
    AppSessionId = null;
    _ensureApp = null;
  }

  public bool OpenLevel(
    string levelPath,
    int? tufLevelId,
    int levelTileCount,
    byte[] gameplayHash,
    int? gameplayHashVersion
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
      return true;
    }

    CloseLevel();

    string levelSessionId = Guid.NewGuid().ToString("N");
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
      FirstSeenAtUtc = openedAtUtc,
      LastSeenAtUtc = openedAtUtc,
    };
    var levelSession = new LevelSession
    {
      Id = levelSessionId,
      AppSessionId = AppSessionId,
      OpenedAtUtc = openedAtUtc,
    };
    Action ensureApp = _ensureApp;
    bool saved = false;
    bool metadataRead = false;
    Action ensure = () =>
    {
      if (saved)
        return;
      ensureApp();
      if (!metadataRead)
      {
        bool available = AdofaiLevelMetadataReader.TryRead(levelPath, out LevelMetadataSnapshot metadata);
        level.Song = metadata?.Song;
        level.Author = metadata?.Author;
        level.Artist = metadata?.Artist;
        level.MetadataState = available ? LevelMetadataState.Captured : LevelMetadataState.Unavailable;
        metadataRead = true;
      }
      levelSession.LevelId = LevelRepository.ResolveOrCreate(level);
      LevelSessionRepository.Save(levelSession);
      saved = true;
    };
    _ensureLevel = ensure;
    _writes.Enqueue(() => RetryLockedWrite(ensure));
    LevelSessionId = levelSessionId;
    TufLevelId = tufLevelId;
    LevelPath = levelPath;
    _gameplayHash = gameplayHash == null ? null : (byte[])gameplayHash.Clone();
    _gameplayHashVersion = gameplayHashVersion;
    _nextRunIndex = 0;
    return true;
  }

  public void CloseLevel()
  {
    if (LevelSessionId == null)
      return;

    string id = LevelSessionId;
    string closedAtUtc = DateTime.UtcNow.ToString("O");
    Action ensureLevel = _ensureLevel;
    _writes.Enqueue(() =>
      RetryLockedWrite(() =>
      {
        ensureLevel?.Invoke();
        LevelSessionRepository.CloseOrDeleteIfEmpty(id, closedAtUtc);
        ActivityChanges.Notify();
      })
    );
    LevelSessionId = null;
    TufLevelId = null;
    LevelPath = null;
    _gameplayHash = null;
    _gameplayHashVersion = null;
    _ensureLevel = null;
  }

  public RunRecord CreateRunDraft(RecordedRunPayload data, int startTile, int levelTileCount)
  {
    if (AppSessionId == null || LevelSessionId == null || data == null)
      return null;

    return new RunRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      AppSessionId = AppSessionId,
      LevelSessionId = LevelSessionId,
      TufLevelId = data.TufLevelId,
      RunIndex = _nextRunIndex,
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
    };
  }

  public void SaveRun(RunRecord run)
  {
    if (run == null)
      return;
    _nextRunIndex = Math.Max(_nextRunIndex, run.RunIndex + 1);
    Action ensureLevel = _ensureLevel;
    _writes.Enqueue(() =>
      RetryLockedWrite(() =>
      {
        ensureLevel?.Invoke();
        RunRepository.Save(run);
        ActivityChanges.Notify(run.Id);
      })
    );
  }

  public Task<bool> SaveRun(RunRecord run, RecordedRunPayload completed, Action persisted, Action<Exception> failed)
  {
    if (run == null || completed == null)
      throw new ArgumentNullException(run == null ? nameof(run) : nameof(completed));
    _nextRunIndex = Math.Max(_nextRunIndex, run.RunIndex + 1);
    var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    Action ensureLevel = _ensureLevel;
    _writes.Enqueue(() =>
    {
      try
      {
        Recording.Sessions.RecordingPayloadBuilder.Apply(run, completed);
        RetryLockedWrite(() =>
        {
          ensureLevel?.Invoke();
          RunRepository.Save(run);
        });
      }
      catch (Exception exception)
      {
        result.TrySetResult(false);
        failed?.Invoke(exception);
        throw;
      }
      result.TrySetResult(true);
      persisted?.Invoke();
      ActivityChanges.Notify(run.Id);
    });
    return result.Task;
  }

  private static void RetryLockedWrite(Action write)
  {
    for (int attempt = 0; ; attempt++)
    {
      try
      {
        write();
        return;
      }
      catch (SqliteException exception) when (attempt < 2 && DatabaseStore.IsTransientLock(exception))
      {
        System.Threading.Thread.Sleep(50 * (attempt + 1));
      }
    }
  }
}
