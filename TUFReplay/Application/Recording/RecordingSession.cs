using System;
using System.Collections.Generic;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.NativeInput.Capture;

namespace TUFReplay.Application.Recording;

public class RecordingSession
{
  private readonly object _lock = new object();
  private readonly List<PendingSongPositionInput> _pendingSongPositionInputs = new List<PendingSongPositionInput>();
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
        GameplayHash = gameplayHash == null ? null : (byte[])gameplayHash.Clone(),
        GameplayHashVersion = gameplayHashVersion,
      };
      RefreshPitchLocked();
      _pendingSongPositionInputs.Clear();
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
    lock (_lock)
    {
      if (!IsRecording)
        return;

      IsRecording = false;
      IsCapturingInput = false;
      RefreshPitchLocked();
      MarkTerminalLocked();
    }

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
      if (!Data.GameplayStartSongPosition.HasValue)
      {
        Data.GameplayStartSongPosition = RecordingClock.CurrentSongPosition();
        FlushPendingSongPositionInputsLocked();
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

      long wonTimeUs = CurrentTimelineTimeUsLocked();
      Data.WonTimeUs = wonTimeUs;
      _wonUnscaledTime = RecordingClock.CurrentUnscaledTime();
      _lastTimelineTimeUs = wonTimeUs;
      _hasTimelineTime = true;
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
    RecordInputTracker.StopCapture();
    lock (_lock)
      Data.InputCapture = RecordInputTracker.CaptureMode;
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
        _pendingSongPositionInputs.Add(new PendingSongPositionInput(RecordingClock.CurrentSongPosition(), key, flags));
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

      long newestTimestampNs = inputs[0].TimestampNs;
      for (int i = 1; i < inputs.Length; i++)
      {
        newestTimestampNs = Math.Max(newestTimestampNs, inputs[i].TimestampNs);
      }

      double timelineRate = 1d;
      if (!Data.WonTimeUs.HasValue && Data.EffectivePitch.HasValue && Data.EffectivePitch.Value > 0f)
        timelineRate = Data.EffectivePitch.Value;

      if (!Data.GameplayStartSongPosition.HasValue)
      {
        double anchorSongPosition = RecordingClock.CurrentSongPosition();
        for (int i = 0; i < inputs.Length; i++)
        {
          NativeInputTransition input = inputs[i];
          double elapsedSeconds = ElapsedSeconds(newestTimestampNs, input.TimestampNs);
          _pendingSongPositionInputs.Add(
            new PendingSongPositionInput(
              anchorSongPosition - elapsedSeconds * timelineRate,
              input.Key,
              ToRecordInputFlags(input.Down)
            )
          );
        }

        return inputs.Length;
      }

      long anchorTimeUs = CurrentTimelineTimeUsLocked();
      for (int i = 0; i < inputs.Length; i++)
      {
        NativeInputTransition input = inputs[i];
        double elapsedSeconds = ElapsedSeconds(newestTimestampNs, input.TimestampNs);
        long elapsedTimeUs = (long)(elapsedSeconds * timelineRate * 1_000_000d);
        AddInputLocked(anchorTimeUs - elapsedTimeUs, input.Key, ToRecordInputFlags(input.Down));
      }

      return inputs.Length;
    }
  }

  public void AddHitContext(RecordedHitContext hitContext)
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;
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

      data.JudgmentCounts = new JudgmentCounts
      {
        Overload = ReadHitCount(hits, HitMargin.FailOverload),
        TooEarly = ReadHitCount(hits, HitMargin.TooEarly),
        Early = ReadHitCount(hits, HitMargin.VeryEarly),
        EarlyPerfect = ReadHitCount(hits, HitMargin.EarlyPerfect),
        Perfect = ReadHitCount(hits, HitMargin.Perfect) + ReadHitCount(hits, HitMargin.Auto),
        LatePerfect = ReadHitCount(hits, HitMargin.LatePerfect),
        Late = ReadHitCount(hits, HitMargin.VeryLate),
        TooLate = ReadHitCount(hits, HitMargin.TooLate),
        Miss = ReadHitCount(hits, HitMargin.FailMiss),
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

  private static int ReadHitCount(int[] hits, HitMargin margin)
  {
    int index = (int)margin;
    return index >= 0 && index < hits.Length ? Math.Max(0, hits[index]) : 0;
  }

  private static double ElapsedSeconds(long newestTimestampNs, long timestampNs)
  {
    if (timestampNs <= 0 || timestampNs >= newestTimestampNs)
      return 0d;
    return (newestTimestampNs - timestampNs) / 1_000_000_000d;
  }

  private static RecordInputFlags ToRecordInputFlags(bool down)
  {
    RecordInputFlags flags = RecordInputFlags.Async;
    return down ? flags | RecordInputFlags.Down : flags;
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

  private void FlushPendingSongPositionInputsLocked()
  {
    foreach (PendingSongPositionInput input in _pendingSongPositionInputs)
    {
      AddInputLocked(ToRecordTimeUs(input.SongPosition), input.Key, input.Flags);
    }

    _pendingSongPositionInputs.Clear();
  }

  private void AddInputLocked(long timeUs, int key, RecordInputFlags flags)
  {
    if (_hasTimelineTime)
      timeUs = Math.Max(_lastTimelineTimeUs, timeUs);
    _lastTimelineTimeUs = timeUs;
    _hasTimelineTime = true;
    Data.Inputs.Add(new RecordedInput(timeUs, key, flags));
  }

  private readonly struct PendingSongPositionInput
  {
    public readonly double SongPosition;
    public readonly int Key;
    public readonly RecordInputFlags Flags;

    public PendingSongPositionInput(double songPosition, int key, RecordInputFlags flags)
    {
      SongPosition = songPosition;
      Key = key;
      Flags = flags;
    }
  }
}
