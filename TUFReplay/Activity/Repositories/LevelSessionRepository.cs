using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Models;
using TUFReplay.Shared.Database;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Activity.Repositories;

public static class LevelSessionRepository
{
  private const string Select =
    @"SELECT s.id,s.level_id,s.app_session_id,s.opened_at_utc,s.closed_at_utc,
l.source_kind,l.tuf_level_id,l.adofai_path,l.level_tile_count,l.gameplay_hash,l.gameplay_hash_version,
l.song,l.author,l.artist,l.metadata_state
FROM level_sessions s JOIN levels l ON l.id=s.level_id";

  public static void Save(LevelSession session)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
VALUES(@id,@level,@app,@open,@close)";
    command.Parameters.AddWithValue("@id", session.Id);
    command.Parameters.AddWithValue("@level", session.LevelId);
    command.Parameters.AddWithValue("@app", session.AppSessionId);
    command.Parameters.AddWithValue("@open", session.OpenedAtUtc);
    command.Parameters.AddWithValue("@close", DbValue.From(session.ClosedAtUtc));
    command.ExecuteNonQuery();
  }

  public static bool CloseOrDeleteIfEmpty(string id, string end)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT level_id FROM level_sessions WHERE id=@id";
    command.Parameters.AddWithValue("@id", id);
    string levelId = command.ExecuteScalar() as string;
    command.CommandText =
      @"DELETE FROM level_sessions
WHERE id=@id AND NOT EXISTS (SELECT 1 FROM runs WHERE level_session_id=@id)";
    int deleted = command.ExecuteNonQuery();

    if (deleted == 0)
    {
      command.CommandText = "UPDATE level_sessions SET closed_at_utc=@end WHERE id=@id";
      command.Parameters.AddWithValue("@end", end);
      command.ExecuteNonQuery();
      LevelRepository.TouchLastSeen(connection, transaction, levelId, end);
    }
    else
    {
      LevelRepository.DeleteIfOrphaned(connection, transaction, levelId);
    }

    transaction.Commit();
    return deleted > 0;
  }

  public static LevelSession Get(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    return Get(connection, null, id);
  }

  public static LevelSession Get(SqliteConnection connection, SqliteTransaction transaction, string id)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = Select + " WHERE s.id=@id LIMIT 1";
    command.Parameters.AddWithValue("@id", id);
    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? Read(reader) : null;
  }

  public static bool Exists(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM level_sessions WHERE id=@id LIMIT 1";
    command.Parameters.AddWithValue("@id", id);
    return command.ExecuteScalar() != null;
  }

  public static List<LevelSession> ListByLogicalLevelNewestFirst(string id)
  {
    var result = new List<LevelSession>();
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = Select + " WHERE s.level_id=@id ORDER BY s.opened_at_utc DESC,s.id DESC";
    command.Parameters.AddWithValue("@id", id);
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
      result.Add(Read(reader));
    return result;
  }

  public static void UpdateMetadata(string id, LevelMetadataSnapshot metadata, LevelMetadataState state)
  {
    LevelSession session = Get(id);
    LevelRepository.UpdateMetadata(session?.LevelId, metadata, state);
  }

  public static string UpdateTufLevelIdIfMissing(string id, int tufLevelId)
  {
    LevelSession session = Get(id);
    if (session == null)
      return null;
    if (session.SourceKind == LevelSourceKind.Tuf && session.TufLevelId.HasValue)
      return session.LevelId;
    return LevelRepository.PromoteToTufIdentity(id, tufLevelId);
  }

  private static LevelSession Read(SqliteDataReader reader) =>
    new LevelSession
    {
      Id = reader.GetString(0),
      LevelId = reader.GetString(1),
      AppSessionId = reader.GetString(2),
      OpenedAtUtc = reader.GetString(3),
      ClosedAtUtc = DbValue.NullableString(reader, 4),
      SourceKind = (LevelSourceKind)reader.GetInt32(5),
      TufLevelId = DbValue.NullableInt(reader, 6),
      LevelPath = reader.GetString(7),
      LevelTileCount = reader.GetInt32(8),
      GameplayHash = reader.IsDBNull(9) ? null : (byte[])reader.GetValue(9),
      GameplayHashVersion = DbValue.NullableInt(reader, 10),
      Song = DbValue.NullableString(reader, 11),
      Author = DbValue.NullableString(reader, 12),
      Artist = DbValue.NullableString(reader, 13),
      MetadataState = (LevelMetadataState)reader.GetInt32(14),
    };
}
