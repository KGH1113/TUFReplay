using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
  public string InputFailureReason;
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
  public int? GameInputOffsetMs;
  public int? LevelPitchPercent;
  public float? PitchSpeedMultiplier;
  public float? EffectivePitch;
  public float? XAccuracy;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public string HitMarginLimit;
  public RunJudgmentSystem JudgmentSystem;
  public JudgmentCounts JudgmentCounts = new JudgmentCounts();
  public byte[] GameplayHash;
  public int? GameplayHashVersion;
  public string PitchSource;
  public List<RecordedInput> Inputs = new List<RecordedInput>();
  public List<RecordedHitContext> HitContexts = new List<RecordedHitContext>();

  // The session seals its input/hit lists before transferring this snapshot to
  // the writer. Scalar metadata is copied without duplicating a long recording.
  internal RecordedRunPayload CompletedSnapshot() => (RecordedRunPayload)MemberwiseClone();

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
      gameInputOffsetMs = GameInputOffsetMs,
      levelPitchPercent = LevelPitchPercent,
      pitchSpeedMultiplier = PitchSpeedMultiplier,
      effectivePitch = EffectivePitch,
      judgmentSystem = JudgmentSystem.ToString(),
      hitMarginLimit = HitMarginLimit,
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
      inputFailureReason = InputFailureReason,
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
    using var writer = new Utf8CsvWriter();
    foreach (RecordedInput input in Inputs)
    {
      writer
        .Append(input.TimeUs)
        .Append(',')
        .Append(input.Key)
        .Append(',')
        .Append((int)(ushort)input.Flags)
        .Append(',')
        .Append(input.NativeCode)
        .Append(',')
        .Append(input.NativeFlags)
        .Append('\n');
    }
    return writer.ToArray();
  }

  public byte[] ToHitContextCsvBytes()
  {
    using var writer = new Utf8CsvWriter();
    foreach (RecordedHitContext hit in HitContexts)
    {
      writer
        .Append(hit.CurrentFloorID)
        .Append(',')
        .AppendRoundTrip(hit.CurrAngle)
        .Append(',')
        .AppendRoundTrip(hit.OverloadCounter)
        .Append(',')
        .Append(hit.NoFailHit ? '1' : '0')
        .Append(',')
        .Append(hit.IsAuto ? '1' : '0')
        .Append(',')
        .Append(hit.NextFloorAuto ? '1' : '0')
        .Append(',')
        .AppendRoundTrip(hit.CachedAngle)
        .Append(',')
        .AppendRoundTrip(hit.TargetExitAngle)
        .Append(',')
        .Append(hit.MidspinInfiniteMargin ? '1' : '0')
        .Append(',')
        .Append(hit.RDCAuto ? '1' : '0')
        .Append(',')
        .Append(hit.CurFreeRoamSection);

      if (hit.ResolvedHitMargin.HasValue)
      {
        writer.Append(',').Append(hit.ResolvedHitMargin.Value);
        if (hit.TimeUs.HasValue)
          writer.Append(',').Append(hit.TimeUs.Value);
      }

      writer.Append('\n');
    }
    return writer.ToArray();
  }

  public bool TryCreateArtifact(string runId, out ReplayArtifact artifact)
  {
    artifact = null;
    if (GetInputFailureReason() != null)
      return false;
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

  public string GetInputFailureReason()
  {
    if (!string.IsNullOrEmpty(InputFailureReason))
      return InputFailureReason;
    if (InputOverflowDropped > 0)
      return ReplayUnavailableReasons.InputQueueOverflow;
    if (InputReadFailures > 0)
      return ReplayUnavailableReasons.InputReadFailed;
    return string.IsNullOrEmpty(InputFallbackReason) ? null : ReplayUnavailableReasons.InputStartFailed;
  }
}
