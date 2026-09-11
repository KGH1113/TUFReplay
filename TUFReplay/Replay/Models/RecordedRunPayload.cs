using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using TUFReplay.Activity.Models;

namespace TUFReplay.Replay.Models;

public class RecordedRunPayload
{
  public const string NativeInputFormatV2 = "csv-conductor-timeus-key-flags-nativecode-nativeflags-v2";
  public int? TufLevelId;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public double? GameplayStartSongPosition;
  public long? WonTimeUs;
  public long? TerminalTimeUs;
  public string InputTimeBase = ReplayInputTimeBases.Hybrid;
  public string InputCapture = "unsupported";
  public int InputPendingMax;
  public long InputPendingMaxDurationUs;
  public long InputAnchorMaxDurationUs;
  public long InputInvalidAnchors;
  public long InputDiscontinuities;
  public string InputLastDiscontinuity;
  public long InputUnmappedEvents;
  public long InputDegradedEvents;
  public string InputDegradedReason;
  public string InputFallbackReason;
  public long InputReceived;
  public long InputRecorded;
  public long InputRepeatDropped;
  public long InputOverflowDropped;
  public long InputResyncs;
  public long InputReadFailures;
  public int InputMaxQueueDepth;
  public long InputNativeCallbacks;
  public long InputNativeRepeatDropped;
  public long InputNativeUnmapped;
  public int InputNativeDevices;
  public int InputNativeQueueDepth;
  public bool NoFailMode;
  public int? LevelPitchPercent;
  public float? PitchSpeedMultiplier;
  public float? EffectivePitch;
  public float? XAccuracy;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public RunJudgmentSystem JudgmentSystem;
  public JudgmentCounts JudgmentCounts = new JudgmentCounts();
  public byte[] GameplayHash;
  public int? GameplayHashVersion;
  public string PitchSource;
  public List<RecordedInput> Inputs = new List<RecordedInput>();
  public List<RecordedHitContext> HitContexts = new List<RecordedHitContext>();

  public string ToActivityMetaJson()
  {
    var meta = new
    {
      metadataVersion = 1,
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
      judgmentSystem = JudgmentSystem.ToString(),
      pitchSource = PitchSource,
      inputFormat = NativeInputFormatV2,
      inputTimeBase = InputTimeBase,
      inputCapture = InputCapture,
      inputPendingMax = InputPendingMax,
      inputPendingMaxDurationUs = InputPendingMaxDurationUs,
      inputAnchorMaxDurationUs = InputAnchorMaxDurationUs,
      inputInvalidAnchors = InputInvalidAnchors,
      inputDiscontinuities = InputDiscontinuities,
      inputLastDiscontinuity = InputLastDiscontinuity,
      inputUnmappedEvents = InputUnmappedEvents,
      inputDegradedEvents = InputDegradedEvents,
      inputDegradedReason = InputDegradedReason,
      inputFallbackReason = InputFallbackReason,
      inputReceived = InputReceived,
      inputRecorded = InputRecorded,
      inputRepeatDropped = InputRepeatDropped,
      inputOverflowDropped = InputOverflowDropped,
      inputResyncs = InputResyncs,
      inputReadFailures = InputReadFailures,
      inputMaxQueueDepth = InputMaxQueueDepth,
      inputNativeCallbacks = InputNativeCallbacks,
      inputNativeRepeatDropped = InputNativeRepeatDropped,
      inputNativeUnmapped = InputNativeUnmapped,
      inputNativeDevices = InputNativeDevices,
      inputNativeQueueDepth = InputNativeQueueDepth,
      inputKeySpace = "os-native-key-code",
      inputNativePlatform = NativeInputPlatformName(),
      inputCount = Inputs.Count,
      hitContextFormat = "csv-creplay-currentFloorId-currAngle-overloadCounter-noFailHit-isAuto-nextFloorAuto-cachedAngle-targetExitAngle-midspinInfiniteMargin-rdcAuto-curFreeRoamSection-resolvedHitMargin-timeUs",
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
      builder
        .Append(input.TimeUs)
        .Append(',')
        .Append(input.Key)
        .Append(',')
        .Append((ushort)input.Flags)
        .Append(',')
        .Append(input.NativeCode)
        .Append(',')
        .Append(input.NativeFlags)
        .Append('\n');
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
        .Append(hit.CurFreeRoamSection);

      if (hit.ResolvedHitMargin.HasValue)
      {
        builder.Append(',').Append(hit.ResolvedHitMargin.Value);
        if (hit.TimeUs.HasValue)
        {
          builder.Append(',').Append(hit.TimeUs.Value);
        }
      }

      builder.Append('\n');
    }

    return Encoding.UTF8.GetBytes(builder.ToString());
  }

  public bool TryCreateArtifact(string runId, out ReplayArtifact artifact)
  {
    artifact = null;
    long previousInputTimeUs = 0L;
    for (int i = 0; i < Inputs.Count; i++)
    {
      RecordedInput input = Inputs[i];
      if (i > 0 && input.TimeUs < previousInputTimeUs)
        return false;
      previousInputTimeUs = input.TimeUs;
    }

    for (int i = 0; i < HitContexts.Count; i++)
    {
      RecordedHitContext hit = HitContexts[i];
      if (!hit.ResolvedHitMargin.HasValue || !hit.TimeUs.HasValue || hit.TimeUs.Value < 0L)
        return false;
    }

    artifact = new ReplayArtifact
    {
      RunId = runId,
      InputCount = Inputs.Count,
      HitContextCount = HitContexts.Count,
      InputCsv = ToInputCsvBytes(),
      HitContextCsv = ToHitContextCsvBytes(),
      MetadataJson = ToActivityMetaJson(),
    };
    return true;
  }
}
