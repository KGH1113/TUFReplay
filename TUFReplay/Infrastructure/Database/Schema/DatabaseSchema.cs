using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class DatabaseSchema
{
  public static void Ensure(SqliteConnection connection)
  {
    ActivitySchema.Ensure(connection);
  }
}
