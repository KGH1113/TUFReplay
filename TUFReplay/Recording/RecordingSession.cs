using System;
using System.Collections.Generic;
using TUFHelper.ModScripts.Json;
using TUFReplay.Shared;

namespace TUFReplay.Recording;

public class RecordingSession
{
  private readonly object _lock = new object();
  private readonly List<PendingSongPositionInput> _pendingSongPositionInputs = new List<PendingSongPositionInput>();

  public bool IsRecording { get; private set; }
  public bool IsCapturingInput { get; private set; }
  public int? LevelId { get; private set; }
  public RecordData Data { get; private set; } = new RecordData();
  public int InputCount
  {
    get
    {
      lock (_lock) return Data.Inputs.Count;
    }
  }
  public int HitContextCount
  {
    get
    {
      lock (_lock) return Data.HitContexts.Count;
    }
  }
  public bool HasRecordableData
  {
    get
    {
      lock (_lock) return Data.Inputs.Count > 0 || Data.HitContexts.Count > 0;
    }
  }

  public void Start(int levelId, LevelListInfoElementJson levelInfo)
  {
    lock (_lock)
    {
      LevelId = levelId;
      IsRecording = Recording.Settings == null || Recording.Settings.AutoRecord;
      IsCapturingInput = false;
      Data = new RecordData
      {
        LevelId = levelId,
        LevelInfo = levelInfo,
        StartedAtUtc = DateTime.UtcNow.ToString("O"),
        NoFailMode = IsNoFailModeActive()
      };
      _pendingSongPositionInputs.Clear();
    }

    RecordInputTracker.Reset();
    RecordingPatches.ResetHitContextState();
    Main.Instance.Log("[Recording] Prepared. tufLevelId=" + levelId + ", autoRecord=" + IsRecording);
  }

  public void Stop()
  {
    lock (_lock)
    {
      if (!IsRecording) return;

      IsRecording = false;
      IsCapturingInput = false;
      Data.EndedAtUtc = DateTime.UtcNow.ToString("O");
    }

    RecordInputTracker.Reset();
    RecordingPatches.ResetHitContextState();
    Main.Instance.Log("[Recording] Stopped. inputs=" + InputCount + ", hitContexts=" + HitContextCount);
  }

  public void StartInputCapture()
  {
    lock (_lock)
    {
      if (!IsRecording || IsCapturingInput) return;
      IsCapturingInput = true;
      RefreshNoFailModeLocked();
    }

    RecordInputTracker.StartCapture();
    Main.Instance.Log("[Recording] Input capture started");
  }

  public void MarkGameplayStarted()
  {
    lock (_lock)
    {
      if (!IsRecording || !IsCapturingInput) return;
      RefreshNoFailModeLocked();
      if (!Data.GameplayStartSongPosition.HasValue)
      {
        Data.GameplayStartSongPosition = GetSongPosition();
        FlushPendingSongPositionInputsLocked();
      }
    }

    Main.Instance.Log("[Recording] Gameplay started. songPosition=" + Data.GameplayStartSongPosition);
  }

  public void StopInputCapture(string reason)
  {
    lock (_lock)
    {
      if (!IsCapturingInput) return;
      IsCapturingInput = false;
    }

    Main.Instance.Log("[Recording/InputDebug] Before stop: " + RecordInputTracker.DebugSnapshot());
    RecordInputTracker.StopCapture();
    Main.Instance.Log("[Recording/InputDebug] After stop: " + RecordInputTracker.DebugSnapshot());
  }

  public void AddInputAtSongPosition(double songPosition, int key, RecordInputFlags flags)
  {
    lock (_lock)
    {
      if (!IsRecording) return;

      if (!Data.GameplayStartSongPosition.HasValue)
      {
        _pendingSongPositionInputs.Add(new PendingSongPositionInput(songPosition, key, flags));
        return;
      }

      AddInputLocked(ToRecordTimeUs(songPosition), key, flags);
    }
  }

  public void AddHitContext(RecordHitContext hitContext)
  {
    lock (_lock)
    {
      if (!IsRecording) return;
      RefreshNoFailModeLocked();
      Data.HitContexts.Add(hitContext);
    }
  }

  public void RemoveLastHitContext()
  {
    lock (_lock)
    {
      if (!IsRecording || Data.HitContexts.Count == 0) return;
      Data.HitContexts.RemoveAt(Data.HitContexts.Count - 1);
    }
  }

  public PlayRecord ToPlayRecord()
  {
    lock (_lock)
    {
      return Data.ToPlayRecord();
    }
  }

  private static double GetSongPosition()
  {
    return ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
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

  private long ToRecordTimeUs(double songPosition)
  {
    double start = Data.GameplayStartSongPosition ?? songPosition;
    return (long)((songPosition - start) * 1_000_000d);
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
    Data.Inputs.Add(new RecordInput
    {
      TimeUs = timeUs,
      Key = key,
      Flags = flags
    });
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
