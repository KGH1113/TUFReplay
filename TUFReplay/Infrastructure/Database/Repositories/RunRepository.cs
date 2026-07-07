using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class RunRepository
{
  public static void Save(RunRecord run)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
INSERT OR REPLACE INTO runs
(id, app_session_id, level_session_id, tuf_level_id, run_index, segment_group_index,
 started_at_utc, ended_at_utc,
 level_tile_count, start_tile, last_tile,
 result, no_fail_mode,
 gameplay_start_song_position, level_pitch_percent, effective_pitch,
 input_count, hit_context_count, input_csv, hit_context_csv, meta_json)
VALUES
(@id, @app_session_id, @level_session_id, @tuf_level_id, @run_index, @segment_group_index,
 @started_at_utc, @ended_at_utc,
 @level_tile_count, @start_tile, @last_tile,
 @result, @no_fail_mode,
 @gameplay_start_song_position, @level_pitch_percent, @effective_pitch,
 @input_count, @hit_context_count, @input_csv, @hit_context_csv, @meta_json);";

    AddRunParameters(command, run);
    command.ExecuteNonQuery();
  }

  public static RunRecord Get(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectRunsSql + @"
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadRun(reader) : null;
  }

  public static List<RunRecord> ListByLevelSession(string levelSessionId)
  {
    return ListByLevelSession(levelSessionId, null, null);
  }

  public static List<RunRecord> ListByLevelSession(string levelSessionId, int? offset, int? limit)
  {
    List<RunRecord> runs = new List<RunRecord>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectRunsSql + @"
WHERE level_session_id = @level_session_id
ORDER BY run_index ASC, started_at_utc ASC
LIMIT @limit OFFSET @offset;";

    command.Parameters.AddWithValue("@level_session_id", levelSessionId);
    command.Parameters.AddWithValue("@limit", limit.GetValueOrDefault(-1));
    command.Parameters.AddWithValue("@offset", offset.GetValueOrDefault(0));

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) runs.Add(ReadRun(reader));

    return runs;
  }

  public static List<RunRecord> ListBySegmentGroup(string levelSessionId, int segmentGroupIndex)
  {
    List<RunRecord> runs = new List<RunRecord>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectRunsSql + @"
WHERE level_session_id = @level_session_id
  AND segment_group_index = @segment_group_index
ORDER BY run_index ASC, started_at_utc ASC;";

    command.Parameters.AddWithValue("@level_session_id", levelSessionId);
    command.Parameters.AddWithValue("@segment_group_index", segmentGroupIndex);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) runs.Add(ReadRun(reader));

    return runs;
  }

  public static RunRecord GetLastByLevelSession(string levelSessionId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectRunsSql + @"
WHERE level_session_id = @level_session_id
ORDER BY run_index DESC, started_at_utc DESC
LIMIT 1;";

    command.Parameters.AddWithValue("@level_session_id", levelSessionId);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadRun(reader) : null;
  }

  public static int NextSegmentGroupIndex(string levelSessionId, int startTile)
  {
    RunRecord lastRun = GetLastByLevelSession(levelSessionId);
    if (lastRun == null) return 0;
    return lastRun.StartTile == startTile ? lastRun.SegmentGroupIndex : lastRun.SegmentGroupIndex + 1;
  }

  public static List<SegmentGroupSummary> ListSegmentGroups(string levelSessionId)
  {
    List<SegmentGroupSummary> groups = new List<SegmentGroupSummary>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT segment_group_index,
       start_tile,
       count(*),
       max(coalesce(last_tile, start_tile)),
       min(started_at_utc),
       max(started_at_utc)
FROM runs
WHERE level_session_id = @level_session_id
GROUP BY segment_group_index, start_tile
ORDER BY segment_group_index ASC;";

    command.Parameters.AddWithValue("@level_session_id", levelSessionId);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      groups.Add(new SegmentGroupSummary
      {
        SegmentGroupIndex = reader.GetInt32(0),
        StartTile = reader.GetInt32(1),
        AttemptCount = reader.GetInt32(2),
        BestLastTile = reader.GetInt32(3),
        FirstStartedAtUtc = reader.GetString(4),
        LastStartedAtUtc = reader.GetString(5)
      });
    }

    return groups;
  }

  private const string SelectRunsSql = @"
SELECT id, app_session_id, level_session_id, tuf_level_id, run_index, segment_group_index,
       started_at_utc, ended_at_utc,
       level_tile_count, start_tile, last_tile,
       result, no_fail_mode,
       gameplay_start_song_position, level_pitch_percent, effective_pitch,
       input_count, hit_context_count, input_csv, hit_context_csv, meta_json
FROM runs
";

  private static void AddRunParameters(SqliteCommand command, RunRecord run)
  {
    command.Parameters.AddWithValue("@id", run.Id);
    command.Parameters.AddWithValue("@app_session_id", run.AppSessionId);
    command.Parameters.AddWithValue("@level_session_id", run.LevelSessionId);
    command.Parameters.AddWithValue("@tuf_level_id", run.TufLevelId);
    command.Parameters.AddWithValue("@run_index", run.RunIndex);
    command.Parameters.AddWithValue("@segment_group_index", run.SegmentGroupIndex);
    command.Parameters.AddWithValue("@started_at_utc", run.StartedAtUtc);
    command.Parameters.AddWithValue("@ended_at_utc", DbValue.From(run.EndedAtUtc));
    command.Parameters.AddWithValue("@level_tile_count", run.LevelTileCount);
    command.Parameters.AddWithValue("@start_tile", run.StartTile);
    command.Parameters.AddWithValue("@last_tile", DbValue.From(run.LastTile));
    command.Parameters.AddWithValue("@result", run.Result ?? "unknown");
    command.Parameters.AddWithValue("@no_fail_mode", DbValue.Bool(run.NoFailMode));
    command.Parameters.AddWithValue("@gameplay_start_song_position", DbValue.From(run.GameplayStartSongPosition));
    command.Parameters.AddWithValue("@level_pitch_percent", DbValue.From(run.LevelPitchPercent));
    command.Parameters.AddWithValue("@effective_pitch", DbValue.From(run.EffectivePitch));
    command.Parameters.AddWithValue("@input_count", run.InputCount);
    command.Parameters.AddWithValue("@hit_context_count", run.HitContextCount);
    command.Parameters.AddWithValue("@input_csv", run.InputCsv ?? new byte[0]);
    command.Parameters.AddWithValue("@hit_context_csv", run.HitContextCsv ?? new byte[0]);
    command.Parameters.AddWithValue("@meta_json", run.MetaJson ?? "{}");
  }

  private static RunRecord ReadRun(SqliteDataReader reader)
  {
    return new RunRecord
    {
      Id = reader.GetString(0),
      AppSessionId = reader.GetString(1),
      LevelSessionId = reader.GetString(2),
      TufLevelId = reader.GetInt32(3),
      RunIndex = reader.GetInt32(4),
      SegmentGroupIndex = reader.GetInt32(5),
      StartedAtUtc = reader.GetString(6),
      EndedAtUtc = DbValue.NullableString(reader, 7),
      LevelTileCount = reader.GetInt32(8),
      StartTile = reader.GetInt32(9),
      LastTile = DbValue.NullableInt(reader, 10),
      Result = reader.GetString(11),
      NoFailMode = reader.GetInt32(12) != 0,
      GameplayStartSongPosition = DbValue.NullableDouble(reader, 13),
      LevelPitchPercent = DbValue.NullableInt(reader, 14),
      EffectivePitch = DbValue.NullableFloat(reader, 15),
      InputCount = reader.GetInt32(16),
      HitContextCount = reader.GetInt32(17),
      InputCsv = ReadBlob(reader, 18),
      HitContextCsv = ReadBlob(reader, 19),
      MetaJson = reader.GetString(20)
    };
  }

  private static byte[] ReadBlob(SqliteDataReader reader, int index)
  {
    if (reader.IsDBNull(index)) return new byte[0];

    long length = reader.GetBytes(index, 0, null, 0, 0);
    byte[] buffer = new byte[(int)length];
    reader.GetBytes(index, 0, buffer, 0, buffer.Length);
    return buffer;
  }
}
