using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

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
