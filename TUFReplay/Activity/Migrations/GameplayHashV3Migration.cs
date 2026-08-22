using System;
using System.Collections.Generic;
using System.IO;
using ADOFAI;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Activity.Migrations;

public static class GameplayHashV3Migration
{
  public static GameplayHashV3MigrationResult Run() => RunCore(VerifyLegacyFile);

  public static GameplayHashV3MigrationResult Run(
    Func<string, byte[], int, IReadOnlyCollection<int>, byte[]> verifyLegacyFile
  )
  {
    if (verifyLegacyFile == null)
      throw new ArgumentNullException(nameof(verifyLegacyFile));

    return RunCore(
      (string levelPath, byte[] legacyHash, int legacyVersion, IReadOnlyCollection<int> pitches) =>
      {
        byte[] currentHash = verifyLegacyFile(levelPath, legacyHash, legacyVersion, pitches);
        return GameplayChartHash.IsSupported(GameplayChartHash.Version, currentHash)
          ? VerificationOutcome.Matched(currentHash)
          : VerificationOutcome.Mismatch;
      }
    );
  }

  private static GameplayHashV3MigrationResult RunCore(VerifyLegacyFileDelegate verifyLegacyFile)
  {
    var result = new GameplayHashV3MigrationResult();
    foreach (LegacyGameplayLevel legacy in ListLegacyLevels())
    {
      result.Scanned++;
      string canonicalPath = LevelPathIdentity.Canonicalize(legacy.Level.LevelPath);
      if (canonicalPath == null)
      {
        SaveAttempt(legacy.Level.Id, null, null, "missing");
        result.Deferred++;
        continue;
      }

      var file = new FileInfo(canonicalPath);
      long fileSize = file.Length;
      long modifiedUtcTicks = file.LastWriteTimeUtc.Ticks;
      if (legacy.AttemptMatches(fileSize, modifiedUtcTicks))
      {
        result.Skipped++;
        continue;
      }

      VerificationOutcome verification = verifyLegacyFile(
        canonicalPath,
        legacy.Level.GameplayHash,
        legacy.Level.GameplayHashVersion.Value,
        legacy.Pitches
      );
      if (verification.Retryable)
      {
        result.Retryable++;
        continue;
      }
      if (!GameplayChartHash.IsSupported(GameplayChartHash.Version, verification.CurrentHash))
      {
        SaveAttempt(legacy.Level.Id, fileSize, modifiedUtcTicks, "hash_mismatch");
        result.Deferred++;
        continue;
      }

      byte[] previousHash = legacy.Level.GameplayHash;
      int? previousVersion = legacy.Level.GameplayHashVersion;
      legacy.Level.GameplayHash = verification.CurrentHash;
      legacy.Level.GameplayHashVersion = GameplayChartHash.Version;
      LevelRepository.ResolveOrCreate(legacy.Level, previousHash, previousVersion);
      result.Migrated++;
    }

    return result;
  }

  private static VerificationOutcome VerifyLegacyFile(
    string levelPath,
    byte[] expectedHash,
    int expectedVersion,
    IReadOnlyCollection<int> pitches
  )
  {
    if (
      !GameplayChartHash.TryLoadCustomLevel(
        levelPath,
        GameplayChartHash.Version,
        out _,
        out LevelData levelData,
        out byte[] currentHash,
        out string error
      )
    )
      return error != null && error.Contains("NullReferenceException")
        ? VerificationOutcome.Retry
        : VerificationOutcome.Mismatch;

    var legacy = new LegacyGameplayLevel
    {
      Level = new LevelRecord { GameplayHash = expectedHash, GameplayHashVersion = expectedVersion },
    };
    foreach (int pitch in pitches)
      legacy.Pitches.Add(pitch);
    return MatchesLegacyHash(legacy, levelData)
      ? VerificationOutcome.Matched(currentHash)
      : VerificationOutcome.Mismatch;
  }

  private static bool MatchesLegacyHash(LegacyGameplayLevel legacy, LevelData levelData)
  {
    if (legacy.Level.GameplayHashVersion == 1)
      return GameplayChartHash.TryCompute(levelData, 1, out byte[] v1Hash, out _)
        && GameplayChartHash.Equals(legacy.Level.GameplayHash, v1Hash);

    if (legacy.Level.GameplayHashVersion == 3)
      return GameplayChartHash.MatchesVersion3IgnoringLevelVersion(legacy.Level.GameplayHash, levelData);

    if (legacy.Level.GameplayHashVersion != 2)
      return false;

    var pitches = new List<int>(legacy.Pitches);
    if (pitches.Count == 0)
      pitches.Add(levelData.pitch);

    byte currentHitsound = (byte)levelData.hitsound;
    int currentHitsoundVolume = levelData.hitsoundVolume;
    foreach (int pitch in pitches)
    {
      if (MatchesVersion2(legacy.Level.GameplayHash, levelData, pitch, currentHitsound, currentHitsoundVolume))
        return true;
    }

    var hitsounds = new List<byte> { currentHitsound };
    foreach (object value in Enum.GetValues(levelData.hitsound.GetType()))
    {
      byte hitsound = Convert.ToByte(value);
      if (!hitsounds.Contains(hitsound))
        hitsounds.Add(hitsound);
    }

    var volumes = new List<int> { currentHitsoundVolume };
    for (int volume = 0; volume <= 100; volume++)
    {
      if (volume != currentHitsoundVolume)
        volumes.Add(volume);
    }

    foreach (int pitch in pitches)
    foreach (byte hitsound in hitsounds)
    foreach (int hitsoundVolume in volumes)
    {
      if (hitsound == currentHitsound && hitsoundVolume == currentHitsoundVolume)
        continue;
      if (MatchesVersion2(legacy.Level.GameplayHash, levelData, pitch, hitsound, hitsoundVolume))
        return true;
    }

    return false;
  }

  private static bool MatchesVersion2(
    byte[] expected,
    LevelData levelData,
    int pitch,
    byte hitsound,
    int hitsoundVolume
  )
  {
    return GameplayChartHash.TryComputeVersion2(levelData, pitch, hitsound, hitsoundVolume, out byte[] candidate, out _)
      && GameplayChartHash.Equals(expected, candidate);
  }

  private static List<LegacyGameplayLevel> ListLegacyLevels()
  {
    var levels = new List<LegacyGameplayLevel>();
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"SELECT l.id,l.source_kind,l.tuf_level_id,l.adofai_path,l.gameplay_hash,l.gameplay_hash_version,
l.level_tile_count,l.song,l.author,l.artist,l.metadata_state,l.first_seen_at_utc,l.last_seen_at_utc,
p.level_pitch_percent,a.file_size,a.file_modified_utc_ticks,a.result
FROM levels l
LEFT JOIN (
  SELECT DISTINCT s.level_id,r.level_pitch_percent
  FROM level_sessions s JOIN runs r ON r.level_session_id=s.id
  WHERE r.level_pitch_percent IS NOT NULL
) p ON p.level_id=l.id
LEFT JOIN gameplay_hash_migration_attempts a ON a.level_id=l.id
WHERE l.gameplay_hash_version IN (1,2,3) AND l.gameplay_hash IS NOT NULL
ORDER BY l.id,p.level_pitch_percent";
    using SqliteDataReader reader = command.ExecuteReader();
    LegacyGameplayLevel current = null;
    while (reader.Read())
    {
      string id = reader.GetString(0);
      if (current == null || current.Level.Id != id)
      {
        current = new LegacyGameplayLevel
        {
          Level = new LevelRecord
          {
            Id = id,
            SourceKind = (LevelSourceKind)reader.GetInt32(1),
            TufLevelId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            LevelPath = reader.GetString(3),
            GameplayHash = (byte[])reader.GetValue(4),
            GameplayHashVersion = reader.GetInt32(5),
            LevelTileCount = reader.GetInt32(6),
            Song = reader.IsDBNull(7) ? null : reader.GetString(7),
            Author = reader.IsDBNull(8) ? null : reader.GetString(8),
            Artist = reader.IsDBNull(9) ? null : reader.GetString(9),
            MetadataState = (LevelMetadataState)reader.GetInt32(10),
            FirstSeenAtUtc = reader.GetString(11),
            LastSeenAtUtc = reader.GetString(12),
          },
          AttemptFileSize = reader.IsDBNull(14) ? null : reader.GetInt64(14),
          AttemptModifiedUtcTicks = reader.IsDBNull(15) ? null : reader.GetInt64(15),
          AttemptResult = reader.IsDBNull(16) ? null : reader.GetString(16),
        };
        levels.Add(current);
      }

      if (!reader.IsDBNull(13))
        current.Pitches.Add(reader.GetInt32(13));
    }

    return levels;
  }

  private static void SaveAttempt(string levelId, long? fileSize, long? modifiedUtcTicks, string result)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"INSERT INTO gameplay_hash_migration_attempts(
  level_id,file_size,file_modified_utc_ticks,result,attempted_at_utc
) VALUES(@level,@size,@modified,@result,@attempted)
ON CONFLICT(level_id) DO UPDATE SET
  file_size=excluded.file_size,
  file_modified_utc_ticks=excluded.file_modified_utc_ticks,
  result=excluded.result,
  attempted_at_utc=excluded.attempted_at_utc";
    command.Parameters.AddWithValue("@level", levelId);
    command.Parameters.AddWithValue("@size", (object)fileSize ?? DBNull.Value);
    command.Parameters.AddWithValue("@modified", (object)modifiedUtcTicks ?? DBNull.Value);
    command.Parameters.AddWithValue("@result", result);
    command.Parameters.AddWithValue("@attempted", DateTime.UtcNow.ToString("O"));
    command.ExecuteNonQuery();
  }

  private sealed class LegacyGameplayLevel
  {
    public LevelRecord Level;
    public readonly HashSet<int> Pitches = new HashSet<int>();
    public long? AttemptFileSize;
    public long? AttemptModifiedUtcTicks;
    public string AttemptResult;

    public bool AttemptMatches(long fileSize, long modifiedUtcTicks) =>
      AttemptResult == "hash_mismatch" && AttemptFileSize == fileSize && AttemptModifiedUtcTicks == modifiedUtcTicks;
  }

  private delegate VerificationOutcome VerifyLegacyFileDelegate(
    string levelPath,
    byte[] legacyHash,
    int legacyVersion,
    IReadOnlyCollection<int> pitches
  );

  private readonly struct VerificationOutcome
  {
    public static readonly VerificationOutcome Mismatch = new VerificationOutcome(null, false);
    public static readonly VerificationOutcome Retry = new VerificationOutcome(null, true);

    private VerificationOutcome(byte[] currentHash, bool retryable)
    {
      CurrentHash = currentHash;
      Retryable = retryable;
    }

    public byte[] CurrentHash { get; }
    public bool Retryable { get; }

    public static VerificationOutcome Matched(byte[] currentHash) => new VerificationOutcome(currentHash, false);
  }
}

public sealed class GameplayHashV3MigrationResult
{
  public int Scanned;
  public int Migrated;
  public int Skipped;
  public int Deferred;
  public int Retryable;
}
