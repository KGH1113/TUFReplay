using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Infrastructure.Database.Schema;

namespace TUFReplay.Infrastructure.Database;

public static class Database
{
  public static string DbPath { get; private set; }

  public static void Initialize()
  {
    NativeSqliteLoader.Initialize();

    string dir = Path.Combine(Main.Instance.Path, "Data");
    Directory.CreateDirectory(dir);
    DbPath = Path.Combine(dir, "tufreplay.sqlite");

    using SqliteConnection connection = OpenConnection();
    DatabaseSchema.Ensure(connection);
  }

  public static SqliteConnection OpenConnection()
  {
    SqliteConnection connection = new SqliteConnection("Data Source=" + DbPath);
    connection.Open();
    return connection;
  }

}
