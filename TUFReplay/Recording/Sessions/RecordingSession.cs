using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Compatibility;

namespace TUFReplay.Recording.Sessions;

public class RecordingSession
{
  private readonly object _lock = new object();
  private readonly List<NativeInputTransition> _pendingNativeInputs = new List<NativeInputTransition>();
  private InputTimelineAnchor? _previousInputAnchor;
  private bool _gameplayStateReached;
  private long _gameplayStateCaptureTicks;
  private double? _wonUnscaledTime;
  private long _lastTimelineTimeUs;
  private bool _hasTimelineTime;

  public bool IsRecording { get; private set; }
  public bool IsCapturingInput { get; private set; }
  public int? TufLevelId { get; private set; }
  public RecordedRunPayload Data { get; private set; } = new RecordedRunPayload();
  public int InputCount
  {
    get
    {
      lock (_lock)
        return Data.Inputs.Count;
    }
  }
  public int HitContextCount
  {
    get
    {
      lock (_lock)
        return Data.HitContexts.Count;
    }
  }
  public bool HasRecordableData
  {
    get
    {
      lock (_lock)
        return Data.Inputs.Count > 0 || Data.HitContexts.Count > 0;
    }
  }

  public void Start(int? tufLevelId, bool autoRecord, byte[] gameplayHash = null, int? gameplayHashVersion = null)
  {
    lock (_lock)
    {
      TufLevelId = tufLevelId;
      IsRecording = autoRecord;
      IsCapturingInput = false;
      Data = new RecordedRunPayload
      {
        TufLevelId = tufLevelId,
        StartedAtUtc = DateTime.UtcNow.ToString("O"),
        NoFailMode = IsNoFailModeActive(),
        JudgmentSystem = AdofaiRuntimeCompatibility.CaptureJudgmentSystem(),
        GameplayHash = gameplayHash == null ? null : (byte[])gameplayHash.Clone(),
        GameplayHashVersion = gameplayHashVersion,
      };
      RefreshPitchLocked();
      _pendingNativeInputs.Clear();
      _previousInputAnchor = null;
      _gameplayStateReached = false;
      _gameplayStateCaptureTicks = 0L;
      _wonUnscaledTime = null;
      _lastTimelineTimeUs = 0L;
      _hasTimelineTime = false;
    }

    RecordInputTracker.Reset();
    Main.Instance.Log(
      "[Recording] Prepared. tufLevelId=" + (tufLevelId?.ToString() ?? "null") + ", autoRecord=" + IsRecording
    );
  }

  public void Stop()
  {
    bool stopInputCapture;
    lock (_lock)
    {
      if (!IsRecording)
        return;
      stopInputCapture = IsCapturingInput;
    }
    if (stopInputCapture)
      StopInputCapture("session_stop");

    lock (_lock)
    {
      if (!IsRecording)
        return;

      IsRecording = false;
      IsCapturingInput = false;
      RefreshPitchLocked();
      FlushPendingNativeInputsLocked();
      MarkTerminalLocked();
    }

    RecordInputTracker.CopyDiagnosticsTo(Data);
    RecordInputTracker.Reset();
    Main.Instance.Log("[Recording] Stopped. inputs=" + InputCount + ", hitContexts=" + HitContextCount);
  }

  public void StartInputCapture()
  {
    lock (_lock)
    {
      if (!IsRecording || IsCapturingInput)
        return;
      IsCapturingInput = true;
      RefreshNoFailModeLocked();
      RefreshPitchLocked();
    }

    RecordInputTracker.StartCapture();
    lock (_lock)
      Data.InputCapture = RecordInputTracker.CaptureMode;
    Main.Instance.Log("[Recording] Input capture started");
  }

  public void MarkGameplayStarted()
  {
    lock (_lock)
    {
      if (!IsRecording || !IsCapturingInput)
        return;
      RefreshNoFailModeLocked();
      RefreshPitchLocked();
      if (!Data.JudgmentDifficulty.HasValue)
        Data.JudgmentDifficulty = GetCurrentJudgmentDifficulty();
      if (!_gameplayStateReached)
      {
        _gameplayStateReached = true;
        _gameplayStateCaptureTicks = Stopwatch.GetTimestamp();
      }
    }

    Main.Instance.Log("[Recording] Gameplay started. songPosition=" + Data.GameplayStartSongPosition);
  }

  public void MarkWonReached()
  {
    lock (_lock)
    {
      if (!IsRecording || Data.WonTimeUs.HasValue)
        return;

      long wonCaptureTicks = Stopwatch.GetTimestamp();
      double wonSongPosition = RecordingClock.CurrentSongPosition();
      ObserveInputAnchorLocked(
        wonCaptureTicks,
        wonSongPosition,
        EffectiveTimelineRateLocked(),
        ready: true,
        forceSegmentBreak: false
      );
      long wonTimeUs = ToRecordTimeUs(wonSongPosition);
      if (_hasTimelineTime)
        wonTimeUs = Math.Max(_lastTimelineTimeUs, wonTimeUs);
      Data.WonTimeUs = wonTimeUs;
      _wonUnscaledTime = RecordingClock.CurrentUnscaledTime();
      _lastTimelineTimeUs = wonTimeUs;
      _hasTimelineTime = true;
      _previousInputAnchor = new InputTimelineAnchor(wonCaptureTicks, wonTimeUs, 1d);
      Data.InputDiscontinuities++;
      Data.InputLastDiscontinuity = "won";
    }

    Main.Instance.Log("[Recording] Won timeline anchored. wonTimeUs=" + Data.WonTimeUs);
  }

  public void MarkTerminal()
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;
      MarkTerminalLocked();
    }
  }

  public void StopInputCapture(string reason)
  {
    lock (_lock)
    {
      if (!IsCapturingInput)
        return;
      IsCapturingInput = false;
    }

    Main.Instance.Log("[Recording/InputDebug] Before stop: " + RecordInputTracker.DebugSnapshot());
    RecordInputTracker.StopCapture(this);
    lock (_lock)
      FlushPendingNativeInputsLocked();
    lock (_lock)
      RecordInputTracker.CopyDiagnosticsTo(Data);
    Main.Instance.Log("[Recording/InputDebug] After stop: " + RecordInputTracker.DebugSnapshot());
  }

  public void AddInputAtCurrentTime(int key, RecordInputFlags flags)
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;

      if (!Data.GameplayStartSongPosition.HasValue)
      {
        _pendingNativeInputs.Add(
          new NativeInputTransition(
            Stopwatch.GetTimestamp(),
            0L,
            key,
            (flags & RecordInputFlags.Down) != 0,
            (flags & RecordInputFlags.ExtendedKey) != 0
          )
        );
        return;
      }

      AddInputLocked(CurrentTimelineTimeUsLocked(), key, flags);
    }
  }

  internal int AddInputBatch(ReadOnlySpan<NativeInputTransition> inputs)
  {
    if (inputs.Length == 0)
      return 0;

    lock (_lock)
    {
      if (!IsRecording)
        return 0;

      for (int i = 0; i < inputs.Length; i++)
        _pendingNativeInputs.Add(inputs[i]);

      Data.InputPendingMax = Math.Max(Data.InputPendingMax, _pendingNativeInputs.Count);

      return inputs.Length;
    }
  }

  internal void ObserveInputAnchor(
    long prefixCaptureTicks,
    long postfixCaptureTicks,
    double songPosition,
    double timelineRate,
    bool ready
  )
  {
    lock (_lock)
    {
      if (!IsRecording || !IsCapturingInput)
        return;

      long durationTicks = Math.Max(0L, postfixCaptureTicks - prefixCaptureTicks);
      long durationUs = CaptureTicksToMicroseconds(durationTicks);
      Data.InputAnchorMaxDurationUs = Math.Max(Data.InputAnchorMaxDurationUs, durationUs);
      long captureTicks = prefixCaptureTicks + durationTicks / 2L;
      ObserveInputAnchorLocked(captureTicks, songPosition, timelineRate, ready, forceSegmentBreak: false);
    }
  }

  internal void BreakInputTimeline(string reason)
  {
    lock (_lock)
    {
      if (_previousInputAnchor.HasValue)
      {
        FlushPendingNativeInputsLocked();
        _previousInputAnchor = null;
        Data.InputDiscontinuities++;
        Data.InputLastDiscontinuity = reason;
      }
    }
  }

  public void AddHitContext(RecordedHitContext hitContext)
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;
      hitContext.TimeUs = Math.Max(0L, CurrentTimelineTimeUsLocked());
      RefreshNoFailModeLocked();
      Data.HitContexts.Add(hitContext);
    }
  }

  public void RemoveLastHitContext()
  {
    lock (_lock)
    {
      if (!IsRecording || Data.HitContexts.Count == 0)
        return;
      Data.HitContexts.RemoveAt(Data.HitContexts.Count - 1);
    }
  }

  public void SetLastHitContextMargin(int hitMargin)
  {
    lock (_lock)
    {
      if (!IsRecording || Data.HitContexts.Count == 0)
        return;

      int index = Data.HitContexts.Count - 1;
      RecordedHitContext context = Data.HitContexts[index];
      context.ResolvedHitMargin = hitMargin;
      Data.HitContexts[index] = context;
    }
  }

  public RunRecord CompleteRunRecord(RunRecord run, int? lastTile, string result)
  {
    lock (_lock)
    {
      if (run == null)
        return null;

      RefreshNoFailModeLocked();
      RefreshPitchLocked();
      Data.XAccuracy = GetXAccuracy();
      CaptureJudgmentStats(Data);
      MarkTerminalLocked();

      run.EndedAtUtc = Data.EndedAtUtc ?? DateTime.UtcNow.ToString("O");
      run.LastTile = lastTile;
      run.Result = result ?? "unknown";
      return RecordingPayloadBuilder.Apply(run, Data);
    }
  }

  public int GetLastReachedTile()
  {
    lock (_lock)
    {
      if (Data.HitContexts.Count > 0)
      {
        return Math.Max(0, Data.HitContexts[Data.HitContexts.Count - 1].CurrentFloorID);
      }
    }

    return GetCurrentTile();
  }

  public static int GetLevelTileCount()
  {
    try
    {
      if (ADOBase.lm?.listFloors != null)
        return ADOBase.lm.listFloors.Count;
    }
    catch
    {
      // Best-effort telemetry; run recording still works with a zero tile count.
    }

    return 0;
  }

  public static int GetCurrentTile()
  {
    try
    {
      if (ADOBase.controller?.currFloor != null)
        return Math.Max(0, ADOBase.controller.currFloor.seqID);
      if (scrController.instance?.currFloor != null)
        return Math.Max(0, scrController.instance.currFloor.seqID);
    }
    catch
    {
      // Best-effort telemetry; callers have safe fallbacks.
    }

    return 0;
  }

  private static bool IsNoFailModeActive()
  {
    try
    {
      return GCS.useNoFail || (ADOBase.controller != null && ADOBase.controller.noFail);
    }
    catch
    {
      return false;
    }
  }

  private void RefreshNoFailModeLocked()
  {
    Data.NoFailMode = Data.NoFailMode || IsNoFailModeActive();
  }

  private void RefreshPitchLocked()
  {
    Data.LevelPitchPercent = GetLevelPitchPercent();
    Data.PitchSpeedMultiplier = GetPitchSpeedMultiplier();
    Data.EffectivePitch = GetEffectivePitch();
    Data.PitchSource = Data.EffectivePitch.HasValue ? "runtime-conductor" : "runtime-level-data";
  }

  private static int? GetLevelPitchPercent()
  {
    try
    {
      if (ADOBase.isLevelEditor && ADOBase.editor != null)
      {
        return ADOBase.editor.levelData?.pitch;
      }

      return ADOBase.customLevel?.levelData?.pitch;
    }
    catch
    {
      return null;
    }
  }

  private static float? GetPitchSpeedMultiplier()
  {
    try
    {
      if (ADOBase.isLevelEditor && ADOBase.editor != null)
      {
        return ADOBase.editor.playbackSpeed;
      }

      return GCS.speedTrialMode ? GCS.currentSpeedTrial : 1f;
    }
    catch
    {
      return null;
    }
  }

  private static float? GetEffectivePitch()
  {
    try
    {
      if (ADOBase.conductor == null || ADOBase.conductor.song == null)
        return null;
      return ADOBase.conductor.song.pitch;
    }
    catch
    {
      return null;
    }
  }

  private static float? GetXAccuracy()
  {
    try
    {
      float value = ADOBase.controller?.playerOne?.marginTracker?.percentXAcc ?? float.NaN;
      if (float.IsNaN(value) || float.IsInfinity(value))
        return null;
      return Math.Max(0f, Math.Min(1f, value));
    }
    catch
    {
      return null;
    }
  }

  private static void CaptureJudgmentStats(RecordedRunPayload data)
  {
    try
    {
      scrMarginTracker tracker = ADOBase.controller?.playerOne?.marginTracker;
      if (tracker == null)
        return;

      int[] hits = tracker.hitMarginsCount;
      if (hits == null)
        return;

      RunJudgmentSystem judgmentSystem = data.JudgmentSystem;
      int auto = AdofaiRuntimeCompatibility.ReadHitCount(hits, "Auto");
      int perfectMinus = AdofaiRuntimeCompatibility.ReadHitCount(hits, "PerfectMinus");
      int xPerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "XPerfect");
      int perfectPlus = AdofaiRuntimeCompatibility.ReadHitCount(hits, "PerfectPlus");

      data.JudgmentCounts = new JudgmentCounts
      {
        Overload = AdofaiRuntimeCompatibility.ReadHitCount(hits, "FailOverload"),
        TooEarly = AdofaiRuntimeCompatibility.ReadHitCount(hits, "TooEarly"),
        Early = AdofaiRuntimeCompatibility.ReadHitCount(hits, "VeryEarly"),
        EarlyPerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "EarlyPerfect"),
        Perfect = judgmentSystem == RunJudgmentSystem.Legacy
          ? AdofaiRuntimeCompatibility.ReadHitCount(hits, "Perfect") + auto
          : judgmentSystem == RunJudgmentSystem.ModernClassic
            ? perfectMinus + xPerfect + perfectPlus + auto
            : 0,
        PerfectMinus = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? perfectMinus : 0,
        XPerfect = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? xPerfect + auto : 0,
        PerfectPlus = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? perfectPlus : 0,
        LatePerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "LatePerfect"),
        Late = AdofaiRuntimeCompatibility.ReadHitCount(hits, "VeryLate"),
        TooLate = AdofaiRuntimeCompatibility.ReadHitCount(hits, "TooLate"),
        Miss = AdofaiRuntimeCompatibility.ReadHitCount(hits, "FailMiss"),
      };
    }
    catch
    {
      // Judgment telemetry must never interrupt run persistence.
    }
  }

  private static RunJudgmentDifficulty? GetCurrentJudgmentDifficulty()
  {
    try
    {
      int difficulty = (int)GCS.difficulty;
      if (difficulty < (int)RunJudgmentDifficulty.Lenient || difficulty > (int)RunJudgmentDifficulty.Strict)
        return null;
      return (RunJudgmentDifficulty)difficulty;
    }
    catch
    {
      return null;
    }
  }

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
