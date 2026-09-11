using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;

using static TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingSession
{
  private readonly List<NativeInputTransition> _pendingNativeInputs = new List<NativeInputTransition>();
  private InputTimelineAnchor? _previousInputAnchor;
  private bool _gameplayStateReached;
  private long _gameplayStateCaptureTicks;
  private double? _wonUnscaledTime;
  private long _lastTimelineTimeUs;
  private bool _hasTimelineTime;

  private void ObserveInputAnchorLocked(
    long captureTicks,
    double songPosition,
    double timelineRate,
    bool ready,
    bool forceSegmentBreak
  )
  {
    if (
      !ready
      || !_gameplayStateReached
      || captureTicks <= 0
      || !IsFinite(songPosition)
      || !IsFinite(timelineRate)
      || timelineRate <= 0d
    )
    {
      Data.InputInvalidAnchors++;
      return;
    }

    if (_pendingNativeInputs.Count > 0)
    {
      long pendingTicks = Math.Max(0L, captureTicks - _pendingNativeInputs[0].CaptureTimestampTicks);
      Data.InputPendingMaxDurationUs = Math.Max(
        Data.InputPendingMaxDurationUs,
        CaptureTicksToMicroseconds(pendingTicks)
      );
    }

    if (!Data.GameplayStartSongPosition.HasValue)
    {
      double elapsedSeconds = CaptureTicksToSeconds(captureTicks - _gameplayStateCaptureTicks);
      Data.GameplayStartSongPosition = songPosition - elapsedSeconds * timelineRate;
    }

    long timelineUs = Data.WonTimeUs.HasValue ? CurrentTimelineTimeUsLocked() : ToRecordTimeUs(songPosition);
    double effectiveRate = Data.WonTimeUs.HasValue ? 1d : timelineRate;
    InputTimelineAnchor current = new InputTimelineAnchor(captureTicks, timelineUs, effectiveRate);
    ObserveEvidenceSettingsLocked(effectiveRate);

    bool discontinuity = forceSegmentBreak;
    if (_previousInputAnchor.HasValue)
    {
      InputTimelineAnchor previous = _previousInputAnchor.Value;
      long elapsedUs = CaptureTicksToMicroseconds(current.CaptureTicks - previous.CaptureTicks);
      double expectedUs = elapsedUs * previous.Rate;
      long actualUs = current.TimeUs - previous.TimeUs;
      double toleranceUs = Math.Max(50_000d, Math.Abs(expectedUs) * 0.25d + 2_000d);
      if (
        current.CaptureTicks <= previous.CaptureTicks
        || actualUs < 0L
        || Math.Abs(current.Rate - previous.Rate) > 0.0001d
        || Math.Abs(actualUs - expectedUs) > toleranceUs
      )
      {
        discontinuity = true;
      }
    }

    if (discontinuity)
    {
      Data.InputDiscontinuities++;
      Data.InputLastDiscontinuity = "anchor_residual";
    }

    MapPendingNativeInputsLocked(current, discontinuity);
    _previousInputAnchor = current;
  }

  private static RecordInputFlags ToRecordInputFlags(bool down, bool extendedKey)
  {
    RecordInputFlags flags = RecordInputFlags.Async;
    if (down)
      flags |= RecordInputFlags.Down;
    if (extendedKey)
      flags |= RecordInputFlags.ExtendedKey;
    return flags;
  }

  private long ToRecordTimeUs(double songPosition)
  {
    return RecordingClock.ToRecordTimeUs(songPosition, Data.GameplayStartSongPosition);
  }

  private long CurrentTimelineTimeUsLocked()
  {
    long timeUs;
    if (Data.WonTimeUs.HasValue && _wonUnscaledTime.HasValue)
    {
      timeUs = RecordingClock.ContinueFromUnscaledTime(
        Data.WonTimeUs.Value,
        _wonUnscaledTime.Value,
        RecordingClock.CurrentUnscaledTime()
      );
    }
    else
    {
      timeUs = ToRecordTimeUs(RecordingClock.CurrentSongPosition());
    }

    return _hasTimelineTime ? Math.Max(_lastTimelineTimeUs, timeUs) : timeUs;
  }

  private void MarkTerminalLocked()
  {
    if (Data.TerminalTimeUs.HasValue)
      return;

    Data.TerminalTimeUs = CurrentTimelineTimeUsLocked();
    _lastTimelineTimeUs = Data.TerminalTimeUs.Value;
    _hasTimelineTime = true;
    Data.EndedAtUtc = DateTime.UtcNow.ToString("O");
  }

  private void MapPendingNativeInputsLocked(InputTimelineAnchor current, bool discontinuity)
  {
    int mapped = 0;
    InputTimelineAnchor? previous = discontinuity ? null : _previousInputAnchor;
    while (mapped < _pendingNativeInputs.Count)
    {
      NativeInputTransition input = _pendingNativeInputs[mapped];
      if (input.CaptureTimestampTicks > current.CaptureTicks)
        break;

      long timeUs;
      if (
        previous.HasValue
        && input.CaptureTimestampTicks >= previous.Value.CaptureTicks
        && current.CaptureTicks > previous.Value.CaptureTicks
      )
      {
        timeUs = InputTimelineMath.Interpolate(
          input.CaptureTimestampTicks,
          previous.Value.CaptureTicks,
          previous.Value.TimeUs,
          current.CaptureTicks,
          current.TimeUs
        );
      }
      else
      {
        timeUs = InputTimelineMath.BackProject(
          input.CaptureTimestampTicks,
          current.CaptureTicks,
          current.TimeUs,
          current.Rate
        );
      }

      AddInputLocked(
        timeUs,
        input.Key,
        ToRecordInputFlags(input.Down, input.ExtendedKey),
        input.NativeCode,
        input.NativeFlags
      );
      mapped++;
    }

    if (mapped > 0)
      _pendingNativeInputs.RemoveRange(0, mapped);
  }

  private void FlushPendingNativeInputsLocked()
  {
    if (_pendingNativeInputs.Count == 0)
      return;
    if (!_previousInputAnchor.HasValue)
    {
      Data.InputUnmappedEvents += _pendingNativeInputs.Count;
      Data.InputDegradedReason = "no_valid_anchor";
      _pendingNativeInputs.Clear();
      return;
    }

    InputTimelineAnchor previous = _previousInputAnchor.Value;
    NativeInputTransition last = _pendingNativeInputs[_pendingNativeInputs.Count - 1];
    long deltaTicks = Math.Max(0L, last.CaptureTimestampTicks - previous.CaptureTicks);
    if (CaptureTicksToMicroseconds(deltaTicks) > 250_000L)
    {
      Data.InputDegradedEvents += _pendingNativeInputs.Count;
      Data.InputDegradedReason = "segment_tail_extrapolation_over_250ms";
    }
    InputTimelineAnchor extrapolated = new InputTimelineAnchor(
      last.CaptureTimestampTicks,
      previous.TimeUs + (long)(CaptureTicksToMicroseconds(deltaTicks) * previous.Rate),
      previous.Rate
    );
    MapPendingNativeInputsLocked(extrapolated, discontinuity: false);
  }

  private double EffectiveTimelineRateLocked()
  {
    if (Data.WonTimeUs.HasValue)
      return 1d;
    return Data.EffectivePitch.HasValue && Data.EffectivePitch.Value > 0f ? Data.EffectivePitch.Value : 1d;
  }

  private static long CaptureTicksToMicroseconds(long ticks)
  {
    return (long)(ticks * 1_000_000d / Stopwatch.Frequency);
  }

  private static double CaptureTicksToSeconds(long ticks)
  {
    return ticks / (double)Stopwatch.Frequency;
  }

  private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

  private void AddInputLocked(long timeUs, int key, RecordInputFlags flags, int nativeCode = -1, ulong nativeFlags = 0)
  {
    if (_hasTimelineTime)
      timeUs = Math.Max(_lastTimelineTimeUs, timeUs);
    _lastTimelineTimeUs = timeUs;
    _hasTimelineTime = true;
    Data.Inputs.Add(new RecordedInput(timeUs, key, flags, nativeCode, nativeFlags));
    if (_evidenceSink != null && (!Data.WonTimeUs.HasValue || timeUs <= Data.WonTimeUs.Value))
    {
      _evidenceSink?.Write(new RecordedInput(timeUs, key, flags, nativeCode, nativeFlags));
      _evidenceInputs++;
    }
  }

  private readonly struct InputTimelineAnchor
  {
    public readonly long CaptureTicks;
    public readonly long TimeUs;
    public readonly double Rate;

    public InputTimelineAnchor(long captureTicks, long timeUs, double rate)
    {
      CaptureTicks = captureTicks;
      TimeUs = timeUs;
      Rate = rate;
    }
  }
}
