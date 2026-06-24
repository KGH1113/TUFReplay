using Newtonsoft.Json;
using TUFReplay.Shared;

namespace TUFReplay.LocalServer.Dtos;

public sealed class RecordDetailDto
{
  public string Id;
  public int TufLevelId;
  public string ClearedAtUtc;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public int InputCount;
  public bool Submitted;
  public long InputCsvBytes;
  public bool HasMicRecord;
  public long MicRecordBytes;
  public object Meta;

  public static RecordDetailDto From(PlayRecordMetadata record)
  {
    return new RecordDetailDto
    {
      Id = record.Id,
      TufLevelId = record.TufLevelId,
      ClearedAtUtc = record.ClearedAtUtc,
      StartedAtUtc = record.StartedAtUtc,
      EndedAtUtc = record.EndedAtUtc,
      InputCount = record.InputCount,
      Submitted = record.Submitted,
      InputCsvBytes = record.InputCsvBytes,
      HasMicRecord = record.HasMicRecord,
      MicRecordBytes = record.MicRecordBytes,
      Meta = ParseMeta(record.MetaJson)
    };
  }

  private static object ParseMeta(string metaJson)
  {
    if (string.IsNullOrEmpty(metaJson)) return null;

    try
    {
      return JsonConvert.DeserializeObject(metaJson);
    }
    catch
    {
      return metaJson;
    }
  }
}
