using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TUFHelper.ModScripts.Json;
using TUFReplay.Shared;

namespace TUFReplay.Recording;

public class RecordData
{
  public int LevelId;
  public LevelListInfoElementJson LevelInfo;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public double? GameplayStartSongPosition;
  public List<RecordInput> Inputs = new List<RecordInput>();

  public PlayRecord ToPlayRecord()
  {
    string inputCsv = ToInputCsv();

    return new PlayRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      TufLevelId = LevelId,
      ClearedAtUtc = EndedAtUtc,
      StartedAtUtc = StartedAtUtc,
      EndedAtUtc = EndedAtUtc,
      InputCount = Inputs.Count,
      Submitted = false,
      MetaJson = ToMetaJson(),
      InputCsv = Encoding.UTF8.GetBytes(inputCsv),
      MicRecord = null
    };
  }

  private string ToMetaJson()
  {
    var meta = new
    {
      formatVersion = 1,
      tufLevelId = LevelId,
      levelInfo = LevelInfo,
      startedAtUtc = StartedAtUtc,
      endedAtUtc = EndedAtUtc,
      gameplayStartSongPosition = GameplayStartSongPosition,
      inputFormat = "csv-timeus-key-flags",
      inputCount = Inputs.Count,
      micRecord = false
    };

    return JsonConvert.SerializeObject(meta, Formatting.None);
  }

  private string ToInputCsv()
  {
    StringBuilder builder = new StringBuilder();

    foreach (RecordInput input in Inputs)
    {
      builder
        .Append(input.TimeUs)
        .Append(',')
        .Append(input.Key)
        .Append(',')
        .Append((ushort)input.Flags)
        .Append('\n');
    }

    return builder.ToString();
  }
}

public struct RecordInput
{
  public long TimeUs;
  public int Key;
  public RecordInputFlags Flags;
}

[Flags]
public enum RecordInputFlags : ushort
{
  Down = 1 << 0,
  Async = 1 << 1,
  PassedHook = 1 << 2,
  MainCandidate = 1 << 3,
  GameplayCounted = 1 << 4
}
