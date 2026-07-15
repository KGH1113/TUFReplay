using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using TUFReplay.Domain.Activity;

namespace TUFReplay.Domain.ReplayData;

public class RecordedRunPayload
{
  public int? TufLevelId;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public double? GameplayStartSongPosition;
  public long? WonTimeUs;
  public long? TerminalTimeUs;
  public string InputTimeBase = TUFReplay.Application.Recording.RecordingClock.HybridInputTimeBase;
  public bool NoFailMode;
  public int? LevelPitchPercent;
  public float? PitchSpeedMultiplier;
  public float? EffectivePitch;
  public float? XAccuracy;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public JudgmentCounts JudgmentCounts = new JudgmentCounts();
  public string PitchSource;
  public List<RecordedInput> Inputs = new List<RecordedInput>();
  public List<RecordedHitContext> HitContexts = new List<RecordedHitContext>();

  public string ToActivityMetaJson()
  {
    var meta = new
    {
      formatVersion = 2,
      tufLevelId = TufLevelId,
      startedAtUtc = StartedAtUtc,
      endedAtUtc = EndedAtUtc,
      gameplayStartSongPosition = GameplayStartSongPosition,
      wonTimeUs = WonTimeUs,
      terminalTimeUs = TerminalTimeUs,
      noFailMode = NoFailMode,
      levelPitchPercent = LevelPitchPercent,
      pitchSpeedMultiplier = PitchSpeedMultiplier,
      effectivePitch = EffectivePitch,
      pitchSource = PitchSource,
      inputFormat = "csv-conductor-timeus-key-flags",
      inputTimeBase = InputTimeBase,
      inputCapture = "creplay-style-native-key-state-sample",
      inputKeySpace = "os-native-key-code",
      inputNativePlatform = NativeInputPlatformName(),
      inputCount = Inputs.Count,
      hitContextFormat = "csv-creplay-currentFloorId-currAngle-overloadCounter-noFailHit-isAuto-nextFloorAuto-cachedAngle-targetExitAngle-midspinInfiniteMargin-rdcAuto-curFreeRoamSection",
      hitContextCount = HitContexts.Count,
      micRecord = false,
    };

    return JsonConvert.SerializeObject(meta, Formatting.None);
  }

  private static string NativeInputPlatformName()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      return "linux";
    return "unknown";
  }

  public byte[] ToInputCsvBytes()
  {
    StringBuilder builder = new StringBuilder();

    foreach (RecordedInput input in Inputs)
    {
      builder.Append(input.TimeUs).Append(',').Append(input.Key).Append(',').Append((ushort)input.Flags).Append('\n');
    }

    return Encoding.UTF8.GetBytes(builder.ToString());
  }

  public byte[] ToHitContextCsvBytes()
  {
    StringBuilder builder = new StringBuilder();

    foreach (RecordedHitContext hit in HitContexts)
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

    return Encoding.UTF8.GetBytes(builder.ToString());
  }
}
