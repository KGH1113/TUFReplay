using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Infrastructure.NativeInput;

namespace TUFReplay.Infrastructure.Database.Schema;

internal static class SkyHookInputKeyMigration
{
  public static void Migrate(
    SqliteConnection connection,
    out int convertedRunCount,
    out int skippedRunCount,
    out int droppedInputCount
  )
  {
    convertedRunCount = 0;
    skippedRunCount = 0;
    droppedInputCount = 0;
    string platform = CurrentPlatform();

    using SqliteTransaction transaction = connection.BeginTransaction();
    List<StoredInputPayload> payloads = ReadAffectedPayloads(connection, transaction, platform);

    foreach (StoredInputPayload payload in payloads)
    {
      if (
        !TryConvertInputCsv(
          payload.InputCsv,
          out byte[] convertedInputCsv,
          out int convertedInputCount,
          out int droppedInputs
        )
      )
      {
        skippedRunCount++;
        continue;
      }

      JObject metadata;
      try
      {
        metadata = JObject.Parse(payload.MetaJson ?? "{}");
      }
      catch
      {
        skippedRunCount++;
        continue;
      }

      metadata["formatVersion"] = 3;
      metadata["inputCapture"] = "skyhook-native-events";
      metadata["inputKeySpace"] = NativeInputKeyCodeMapper.NativeKeySpace;
      metadata["inputCount"] = convertedInputCount;

      using SqliteCommand update = connection.CreateCommand();
      update.Transaction = transaction;
      update.CommandText =
        "UPDATE runs SET input_count = $inputCount, input_csv = $inputCsv, meta_json = $metaJson WHERE id = $id;";
      update.Parameters.AddWithValue("$inputCount", convertedInputCount);
      update.Parameters.AddWithValue("$inputCsv", convertedInputCsv);
      update.Parameters.AddWithValue("$metaJson", metadata.ToString(Formatting.None));
      update.Parameters.AddWithValue("$id", payload.Id);
      update.ExecuteNonQuery();
      convertedRunCount++;
      droppedInputCount += droppedInputs;
    }

    using (SqliteCommand version = connection.CreateCommand())
    {
      version.Transaction = transaction;
      version.CommandText = "PRAGMA user_version = 7;";
      version.ExecuteNonQuery();
    }

    transaction.Commit();
  }

  internal static bool TryConvertInputCsv(
    byte[] inputCsv,
    out byte[] convertedInputCsv,
    out int convertedInputCount,
    out int droppedInputCount
  )
  {
    convertedInputCsv = null;
    convertedInputCount = 0;
    droppedInputCount = 0;
    if (inputCsv == null || inputCsv.Length == 0)
    {
      convertedInputCsv = Array.Empty<byte>();
      return true;
    }

    string text = Encoding.UTF8.GetString(inputCsv);
    string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    StringBuilder builder = new StringBuilder(text.Length);

    foreach (string line in lines)
    {
      string[] parts = line.Split(',');
      if (parts.Length != 3)
        return false;
      if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hidUsage))
        return false;
      if (!NativeInputKeyCodeMapper.TryConvertSkyHookHidUsage(hidUsage, out int nativeKeyCode))
      {
        droppedInputCount++;
        continue;
      }

      builder.Append(parts[0]).Append(',').Append(nativeKeyCode).Append(',').Append(parts[2]).Append('\n');
      convertedInputCount++;
    }

    convertedInputCsv = Encoding.UTF8.GetBytes(builder.ToString());
    return true;
  }

  private static List<StoredInputPayload> ReadAffectedPayloads(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string platform
  )
  {
    List<StoredInputPayload> payloads = new List<StoredInputPayload>();
    if (platform == null)
      return payloads;

    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"
SELECT id, input_csv, meta_json
FROM runs
WHERE CAST(json_extract(meta_json, '$.formatVersion') AS INTEGER) = 2
  AND lower(COALESCE(json_extract(meta_json, '$.inputKeySpace'), '')) = $keySpace
  AND lower(COALESCE(json_extract(meta_json, '$.inputNativePlatform'), '')) = $platform;";
    command.Parameters.AddWithValue("$keySpace", NativeInputKeyCodeMapper.NativeKeySpace);
    command.Parameters.AddWithValue("$platform", platform);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      payloads.Add(new StoredInputPayload(reader.GetString(0), (byte[])reader.GetValue(1), reader.GetString(2)));
    }

    return payloads;
  }

  private static string CurrentPlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";
    return null;
  }

  private readonly struct StoredInputPayload
  {
    public readonly string Id;
    public readonly byte[] InputCsv;
    public readonly string MetaJson;

    public StoredInputPayload(string id, byte[] inputCsv, string metaJson)
    {
      Id = id;
      InputCsv = inputCsv;
      MetaJson = metaJson;
    }
  }
}
