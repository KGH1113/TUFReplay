using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Shared;

public static class PlayRecordRepository
{
  public static void Save(PlayRecord record)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
INSERT OR REPLACE INTO play_records
(id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted, meta_json, input_csv, mic_record)
VALUES
(@id, @tuf_level_id, @cleared_at_utc, @started_at_utc, @ended_at_utc, @input_count, @submitted, @meta_json, @input_csv, @mic_record);";

    command.Parameters.AddWithValue("@id", record.Id);
    command.Parameters.AddWithValue("@tuf_level_id", record.TufLevelId);
    command.Parameters.AddWithValue("@cleared_at_utc", record.ClearedAtUtc);
    command.Parameters.AddWithValue("@started_at_utc", record.StartedAtUtc);
    command.Parameters.AddWithValue("@ended_at_utc", ToDbValue(record.EndedAtUtc));
    command.Parameters.AddWithValue("@input_count", record.InputCount);
    command.Parameters.AddWithValue("@submitted", record.Submitted ? 1 : 0);
    command.Parameters.AddWithValue("@meta_json", record.MetaJson);
    AddBlobParameter(command, "@input_csv", record.InputCsv, false);
    AddBlobParameter(command, "@mic_record", record.MicRecord, true);

    command.ExecuteNonQuery();
  }

  public static List<PlayRecord> List()
  {
    List<PlayRecord> records = new List<PlayRecord>();

    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted, meta_json, input_csv, mic_record
FROM play_records
ORDER BY cleared_at_utc DESC;";

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) records.Add(ReadRecord(reader));

    return records;
  }

  public static List<PlayRecordSummary> ListSummaries()
  {
    List<PlayRecordSummary> records = new List<PlayRecordSummary>();

    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted,
       length(input_csv), mic_record IS NOT NULL, coalesce(length(mic_record), 0)
FROM play_records
ORDER BY cleared_at_utc DESC;";

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) records.Add(ReadSummary(reader));

    return records;
  }

  public static List<PlayRecord> ListByClearedDateRange(string fromUtc, string toUtc)
  {
    List<PlayRecord> records = new List<PlayRecord>();

    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted, meta_json, input_csv, mic_record
FROM play_records
WHERE cleared_at_utc >= @from_utc
  AND cleared_at_utc < @to_utc
ORDER BY cleared_at_utc DESC;";

    command.Parameters.AddWithValue("@from_utc", fromUtc);
    command.Parameters.AddWithValue("@to_utc", toUtc);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) records.Add(ReadRecord(reader));

    return records;
  }

  public static PlayRecord Get(string id)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted, meta_json, input_csv, mic_record
FROM play_records
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadRecord(reader) : null;
  }

  public static PlayRecordMetadata GetMetadata(string id)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, tuf_level_id, cleared_at_utc, started_at_utc, ended_at_utc, input_count, submitted,
       length(input_csv), mic_record IS NOT NULL, coalesce(length(mic_record), 0), meta_json
FROM play_records
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadMetadata(reader) : null;
  }

  public static void Delete(string id)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = "DELETE FROM play_records WHERE id = @id;";
    command.Parameters.AddWithValue("@id", id);
    command.ExecuteNonQuery();
  }

  private static PlayRecord ReadRecord(SqliteDataReader reader)
  {
    return new PlayRecord
    {
      Id = reader.GetString(0),
      TufLevelId = reader.GetInt32(1),
      ClearedAtUtc = reader.GetString(2),
      StartedAtUtc = reader.GetString(3),
      EndedAtUtc = ReadNullableString(reader, 4),
      InputCount = reader.GetInt32(5),
      Submitted = reader.GetInt32(6) != 0,
      MetaJson = reader.GetString(7),
      InputCsv = ReadBlob(reader, 8),
      MicRecord = reader.IsDBNull(9) ? null : ReadBlob(reader, 9)
    };
  }

  private static PlayRecordSummary ReadSummary(SqliteDataReader reader)
  {
    return new PlayRecordSummary
    {
      Id = reader.GetString(0),
      TufLevelId = reader.GetInt32(1),
      ClearedAtUtc = reader.GetString(2),
      StartedAtUtc = reader.GetString(3),
      EndedAtUtc = ReadNullableString(reader, 4),
      InputCount = reader.GetInt32(5),
      Submitted = reader.GetInt32(6) != 0,
      InputCsvBytes = reader.GetInt64(7),
      HasMicRecord = reader.GetInt32(8) != 0,
      MicRecordBytes = reader.GetInt64(9)
    };
  }

  private static PlayRecordMetadata ReadMetadata(SqliteDataReader reader)
  {
    PlayRecordSummary summary = ReadSummary(reader);
    return new PlayRecordMetadata
    {
      Id = summary.Id,
      TufLevelId = summary.TufLevelId,
      ClearedAtUtc = summary.ClearedAtUtc,
      StartedAtUtc = summary.StartedAtUtc,
      EndedAtUtc = summary.EndedAtUtc,
      InputCount = summary.InputCount,
      Submitted = summary.Submitted,
      InputCsvBytes = summary.InputCsvBytes,
      HasMicRecord = summary.HasMicRecord,
      MicRecordBytes = summary.MicRecordBytes,
      MetaJson = reader.GetString(10)
    };
  }

  private static void AddBlobParameter(SqliteCommand command, string name, byte[] value, bool nullable)
  {
    SqliteParameter parameter = command.CreateParameter();
    parameter.ParameterName = name;
    parameter.SqliteType = SqliteType.Blob;
    parameter.Value = value == null && nullable ? DBNull.Value : value;
    command.Parameters.Add(parameter);
  }

  private static byte[] ReadBlob(SqliteDataReader reader, int index)
  {
    long length = reader.GetBytes(index, 0, null, 0, 0);
    byte[] buffer = new byte[length];
    reader.GetBytes(index, 0, buffer, 0, buffer.Length);
    return buffer;
  }

  private static string ReadNullableString(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : reader.GetString(index);
  }

  private static object ToDbValue(string value)
  {
    return value == null ? DBNull.Value : (object)value;
  }
}
