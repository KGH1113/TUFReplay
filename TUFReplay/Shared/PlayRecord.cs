namespace TUFReplay.Shared;

public class PlayRecord
{
  public string Id;
  public int TufLevelId;
  public string ClearedAtUtc;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public int InputCount;
  public bool Submitted;
  public string MetaJson;
  public byte[] InputCsv;
  public byte[] MicRecord;
}

public class PlayRecordSummary
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
}

public class PlayRecordMetadata : PlayRecordSummary
{
  public string MetaJson;
}
