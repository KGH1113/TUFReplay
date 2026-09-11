using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Migrations;

namespace TUFReplay.Shared.Database;

public static class DatabaseSchema
{
  public static void Ensure(SqliteConnection connection)
  {
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
      command.ExecuteNonQuery();
    }
    ActivitySchema.Ensure(connection);
  }
}
