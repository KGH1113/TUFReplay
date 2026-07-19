using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class RunRepository
{
  public static void Save(RunRecord r)
  {
    JudgmentCounts judgments = r.JudgmentCounts ?? new JudgmentCounts();
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"INSERT INTO runs(
id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,
gameplay_hash,gameplay_hash_version,
input_count,hit_context_count,input_csv,hit_context_csv,meta_json
) VALUES(
@id,@level,@idx,@start,@end,@startTile,@last,@result,@nf,
@song,@pitch,@effective,@xAccuracy,@judgmentDifficulty,
@judgmentOverload,@judgmentTooEarly,@judgmentEarly,@judgmentEarlyPerfect,@judgmentPerfect,
@judgmentLatePerfect,@judgmentLate,@judgmentTooLate,@judgmentMiss,
@gameplayHash,@gameplayHashVersion,
@inputs,@hits,@inputCsv,@hitCsv,@meta
)";
    q.Parameters.AddWithValue("@id", r.Id);
    q.Parameters.AddWithValue("@level", r.LevelSessionId);
    q.Parameters.AddWithValue("@idx", r.RunIndex);
    q.Parameters.AddWithValue("@start", r.StartedAtUtc);
    q.Parameters.AddWithValue("@end", DbValue.From(r.EndedAtUtc));
    q.Parameters.AddWithValue("@startTile", r.StartTile);
    q.Parameters.AddWithValue("@last", DbValue.From(r.LastTile));
    q.Parameters.AddWithValue("@result", r.Result ?? "unknown");
    q.Parameters.AddWithValue("@nf", DbValue.Bool(r.NoFailMode));
    q.Parameters.AddWithValue("@song", DbValue.From(r.GameplayStartSongPosition));
    q.Parameters.AddWithValue("@pitch", DbValue.From(r.LevelPitchPercent));
    q.Parameters.AddWithValue("@effective", DbValue.From(r.EffectivePitch));
    q.Parameters.AddWithValue("@xAccuracy", DbValue.From(r.XAccuracy));
    q.Parameters.AddWithValue(
      "@judgmentDifficulty",
      DbValue.From(r.JudgmentDifficulty.HasValue ? (int?)r.JudgmentDifficulty.Value : null)
    );
    q.Parameters.AddWithValue("@judgmentOverload", judgments.Overload);
    q.Parameters.AddWithValue("@judgmentTooEarly", judgments.TooEarly);
    q.Parameters.AddWithValue("@judgmentEarly", judgments.Early);
    q.Parameters.AddWithValue("@judgmentEarlyPerfect", judgments.EarlyPerfect);
    q.Parameters.AddWithValue("@judgmentPerfect", judgments.Perfect);
    q.Parameters.AddWithValue("@judgmentLatePerfect", judgments.LatePerfect);
    q.Parameters.AddWithValue("@judgmentLate", judgments.Late);
    q.Parameters.AddWithValue("@judgmentTooLate", judgments.TooLate);
    q.Parameters.AddWithValue("@judgmentMiss", judgments.Miss);
    q.Parameters.AddWithValue("@gameplayHash", (object)r.GameplayHash ?? System.DBNull.Value);
    q.Parameters.AddWithValue("@gameplayHashVersion", DbValue.From(r.GameplayHashVersion));
    q.Parameters.AddWithValue("@inputs", r.InputCount);
    q.Parameters.AddWithValue("@hits", r.HitContextCount);
    q.Parameters.AddWithValue("@inputCsv", r.InputCsv ?? new byte[0]);
    q.Parameters.AddWithValue("@hitCsv", r.HitContextCsv ?? new byte[0]);
    q.Parameters.AddWithValue("@meta", r.MetaJson ?? "{}");
    q.ExecuteNonQuery();
  }

  public static int GetNextRunIndex(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText = "SELECT coalesce(max(run_index),-1)+1 FROM runs WHERE level_session_id=@id";
    q.Parameters.AddWithValue("@id", id);
    return System.Convert.ToInt32(q.ExecuteScalar());
  }

  public static List<RunRecord> ListByLevelSession(string id, int offset, int limit)
  {
    var result = new List<RunRecord>();
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText = Select + " WHERE r.level_session_id=@id ORDER BY r.run_index ASC LIMIT @limit OFFSET @offset";
    q.Parameters.AddWithValue("@id", id);
    q.Parameters.AddWithValue("@limit", limit);
    q.Parameters.AddWithValue("@offset", offset);
    using SqliteDataReader x = q.ExecuteReader();
    while (x.Read())
      result.Add(Read(x));
    return result;
  }

  public static StoredReplayRun GetReplayRun(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      return null;

    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"
SELECT r.id,r.level_session_id,l.tuf_level_id,l.level_path,l.level_tile_count,
       r.start_tile,r.last_tile,r.result,r.input_csv,r.hit_context_csv,r.meta_json,
       r.gameplay_hash,r.gameplay_hash_version
FROM runs r
JOIN level_sessions l ON l.id=r.level_session_id
WHERE r.id=@id
LIMIT 1";
    q.Parameters.AddWithValue("@id", runId);
    using SqliteDataReader r = q.ExecuteReader();
    if (!r.Read())
      return null;

    return new StoredReplayRun
    {
      Id = r.GetString(0),
      LevelSessionId = r.GetString(1),
      TufLevelId = DbValue.NullableInt(r, 2),
      LevelPath = r.GetString(3),
      LevelTileCount = r.GetInt32(4),
      StartTile = r.GetInt32(5),
      LastTile = DbValue.NullableInt(r, 6),
      Result = r.GetString(7),
      InputCsv = (byte[])r.GetValue(8),
      HitContextCsv = (byte[])r.GetValue(9),
      MetaJson = r.GetString(10),
      GameplayHash = r.IsDBNull(11) ? null : (byte[])r.GetValue(11),
      GameplayHashVersion = DbValue.NullableInt(r, 12),
    };
  }

  public static void UpdateGameplayHashIfMissing(string runId, byte[] hash, int version)
  {
    if (string.IsNullOrWhiteSpace(runId) || hash == null)
      return;

    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      "UPDATE runs SET gameplay_hash=@hash,gameplay_hash_version=@version WHERE id=@id AND gameplay_hash IS NULL";
    q.Parameters.AddWithValue("@hash", hash);
    q.Parameters.AddWithValue("@version", version);
    q.Parameters.AddWithValue("@id", runId);
    q.ExecuteNonQuery();
  }

  private const string Select =
    @"SELECT
r.id,l.app_session_id,r.level_session_id,l.tuf_level_id,r.run_index,r.started_at_utc,r.ended_at_utc,
l.level_tile_count,r.start_tile,r.last_tile,r.result,r.no_fail_mode,r.gameplay_start_song_position,
r.level_pitch_percent,r.effective_pitch,r.x_accuracy,r.judgment_difficulty,
r.judgment_overload,r.judgment_too_early,r.judgment_early,r.judgment_early_perfect,r.judgment_perfect,
r.judgment_late_perfect,r.judgment_late,r.judgment_too_late,r.judgment_miss,
r.gameplay_hash,r.gameplay_hash_version,
r.input_count,r.hit_context_count,length(r.input_csv),length(r.hit_context_csv),r.meta_json,
coalesce(length(m.audio_wav),0),m.sample_rate,m.channels,m.frame_count
FROM runs r
JOIN level_sessions l ON l.id=r.level_session_id
LEFT JOIN microphone_recordings m ON m.run_id=r.id";

  private static RunRecord Read(SqliteDataReader r) =>
    new RunRecord
    {
      Id = r.GetString(0),
      AppSessionId = r.GetString(1),
      LevelSessionId = r.GetString(2),
      TufLevelId = DbValue.NullableInt(r, 3),
      RunIndex = r.GetInt32(4),
      StartedAtUtc = r.GetString(5),
      EndedAtUtc = DbValue.NullableString(r, 6),
      LevelTileCount = r.GetInt32(7),
      StartTile = r.GetInt32(8),
      LastTile = DbValue.NullableInt(r, 9),
      Result = r.GetString(10),
      NoFailMode = r.GetInt32(11) != 0,
      GameplayStartSongPosition = DbValue.NullableDouble(r, 12),
      LevelPitchPercent = DbValue.NullableInt(r, 13),
      EffectivePitch = DbValue.NullableFloat(r, 14),
      XAccuracy = DbValue.NullableFloat(r, 15),
      JudgmentDifficulty = ReadDifficulty(r, 16),
      JudgmentCounts = new JudgmentCounts
      {
        Overload = r.GetInt32(17),
        TooEarly = r.GetInt32(18),
        Early = r.GetInt32(19),
        EarlyPerfect = r.GetInt32(20),
        Perfect = r.GetInt32(21),
        LatePerfect = r.GetInt32(22),
        Late = r.GetInt32(23),
        TooLate = r.GetInt32(24),
        Miss = r.GetInt32(25),
      },
      GameplayHash = r.IsDBNull(26) ? null : (byte[])r.GetValue(26),
      GameplayHashVersion = DbValue.NullableInt(r, 27),
      InputCount = r.GetInt32(28),
      HitContextCount = r.GetInt32(29),
      InputCsvBytes = r.GetInt64(30),
      HitContextCsvBytes = r.GetInt64(31),
      MetaJson = r.GetString(32),
      MicrophoneRecordingBytes = r.GetInt64(33),
      MicrophoneSampleRate = DbValue.NullableInt(r, 34),
      MicrophoneChannels = DbValue.NullableInt(r, 35),
      MicrophoneFrameCount = r.IsDBNull(36) ? null : (long?)r.GetInt64(36),
    };

  private static RunJudgmentDifficulty? ReadDifficulty(SqliteDataReader reader, int index)
  {
    int? value = DbValue.NullableInt(reader, index);
    if (
      !value.HasValue
      || value.Value < (int)RunJudgmentDifficulty.Lenient
      || value.Value > (int)RunJudgmentDifficulty.Strict
    )
      return null;
    return (RunJudgmentDifficulty)value.Value;
  }
}
