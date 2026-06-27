using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
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
  public bool NoFailMode;
  public int? LevelPitchPercent;
  public float? PitchSpeedMultiplier;
  public float? EffectivePitch;
  public string PitchSource;
  public List<RecordInput> Inputs = new List<RecordInput>();
  public List<RecordHitContext> HitContexts = new List<RecordHitContext>();

  public PlayRecord ToPlayRecord()
  {
    string inputCsv = ToInputCsv();
    string hitContextCsv = ToHitContextCsv();

    return new PlayRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      TufLevelId = LevelId,
      ClearedAtUtc = EndedAtUtc,
      StartedAtUtc = StartedAtUtc,
      EndedAtUtc = EndedAtUtc,
      InputCount = Inputs.Count,
      HitContextCount = HitContexts.Count,
      Submitted = false,
      MetaJson = ToMetaJson(),
      InputCsv = Encoding.UTF8.GetBytes(inputCsv),
      HitContextCsv = Encoding.UTF8.GetBytes(hitContextCsv),
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
      noFailMode = NoFailMode,
      levelPitchPercent = LevelPitchPercent,
      pitchSpeedMultiplier = PitchSpeedMultiplier,
      effectivePitch = EffectivePitch,
      pitchSource = PitchSource,
      inputFormat = "csv-conductor-timeus-key-flags",
      inputTimeBase = "ADOBase.conductor.songposition_minusi",
      inputCapture = "creplay-style-native-key-state-sample",
      inputKeySpace = "os-native-key-code",
      inputNativePlatform = NativeInputPlatformName(),
      inputCount = Inputs.Count,
      hitContextFormat = "csv-creplay-currentFloorId-currAngle-overloadCounter-noFailHit-isAuto-nextFloorAuto-cachedAngle-targetExitAngle-midspinInfiniteMargin-rdcAuto-curFreeRoamSection",
      hitContextCount = HitContexts.Count,
      micRecord = false
    };

    return JsonConvert.SerializeObject(meta, Formatting.None);
  }

  private static string NativeInputPlatformName()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
    return "unknown";
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

  private string ToHitContextCsv()
  {
    StringBuilder builder = new StringBuilder();

    foreach (RecordHitContext hit in HitContexts)
    {
      builder
        .Append(hit.CurrentFloorID)
        .Append(',')
        .Append(hit.CurrAngle.ToString("R", CultureInfo.InvariantCulture))
        .Append(',')
        .Append(hit.OverloadCounter.ToString("R", CultureInfo.InvariantCulture))
        .Append(',')
        .Append(hit.NoFailHit ? '1' : '0')
        .Append(',')
        .Append(hit.IsAuto ? '1' : '0')
        .Append(',')
        .Append(hit.NextFloorAuto ? '1' : '0')
        .Append(',')
        .Append(hit.CachedAngle.ToString("R", CultureInfo.InvariantCulture))
        .Append(',')
        .Append(hit.TargetExitAngle.ToString("R", CultureInfo.InvariantCulture))
        .Append(',')
        .Append(hit.MidspinInfiniteMargin ? '1' : '0')
        .Append(',')
        .Append(hit.RDCAuto ? '1' : '0')
        .Append(',')
        .Append(hit.CurFreeRoamSection)
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

public struct RecordHitContext
{
  public int CurrentFloorID;
  public double CurrAngle;
  public float OverloadCounter;
  public bool NoFailHit;
  public bool IsAuto;
  public bool NextFloorAuto;
  public double CachedAngle;
  public double TargetExitAngle;
  public bool MidspinInfiniteMargin;
  public bool RDCAuto;
  public int CurFreeRoamSection;
}
