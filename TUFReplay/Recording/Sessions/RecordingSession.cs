using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Compatibility;
using static TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingSession
{
  private readonly object _lock = new object();

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
      AbortEvidenceLocked("recording_restarted");
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
      AbortEvidenceLocked("recording_stopped");
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
      WriteEvidenceStateLocked(TUFReplay.Recording.Capture.RecordingStateKind.CaptureStarted);
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
        WriteEvidenceStateLocked(TUFReplay.Recording.Capture.RecordingStateKind.GameplayStarted);
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
      WriteEvidenceStateLocked(TUFReplay.Recording.Capture.RecordingStateKind.Won);
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

  public static int GetLevelTileCount() => TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry.GetLevelTileCount();

  public static int GetCurrentTile() => TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry.GetCurrentTile();
}
