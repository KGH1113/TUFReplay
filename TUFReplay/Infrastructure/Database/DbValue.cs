using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database;

public static class DbValue
{
  public static object From(string value) => value == null ? DBNull.Value : value;

  public static object From(int? value) => value.HasValue ? value.Value : DBNull.Value;

  public static object From(long? value) => value.HasValue ? value.Value : DBNull.Value;

  public static object From(double? value) => value.HasValue ? value.Value : DBNull.Value;

  public static object From(float? value) => value.HasValue ? value.Value : DBNull.Value;

  public static int Bool(bool value) => value ? 1 : 0;

  public static string NullableString(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : reader.GetString(index);
  }

  public static int? NullableInt(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : reader.GetInt32(index);
  }

  public static long? NullableLong(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : reader.GetInt64(index);
  }

  public static double? NullableDouble(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : reader.GetDouble(index);
  }

  public static float? NullableFloat(SqliteDataReader reader, int index)
  {
    return reader.IsDBNull(index) ? null : (float)reader.GetDouble(index);
  }
}
