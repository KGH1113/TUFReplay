using System;
using TUFHelper.ModScripts.Json;
using TUFReplay.Shared;

namespace TUFReplay.Recording;

public class RecordingSession
{
  private readonly object _lock = new object();

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
        StartedAtUtc = DateTime.UtcNow.ToString("O")
      };
    }

    RDInputRecorder.Reset();
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

    RDInputRecorder.Reset();
    Main.Instance.Log("[Recording] Stopped. inputs=" + InputCount);
  }

  public void StartInputCapture()
  {
    lock (_lock)
    {
      if (!IsRecording || IsCapturingInput) return;

      IsCapturingInput = true;
      Data.GameplayStartSongPosition = GetSongPosition();
    }

    RDInputRecorder.Reset();
    Main.Instance.Log("[Recording] Gameplay input marker set. songPosition=" + Data.GameplayStartSongPosition);
  }

  public void StopInputCapture(string reason)
  {
    lock (_lock)
    {
      if (!IsCapturingInput) return;

      IsCapturingInput = false;
    }

    RDInputRecorder.Reset();
    Main.Instance.Log("[Recording] Input capture stopped. reason=" + reason + ", inputs=" + InputCount);
  }

  public void AddInput(int keyCode, bool down, double songPosition)
  {
    lock (_lock)
    {
      if (!IsRecording) return;

      Data.Inputs.Add(new RecordInput
      {
        SongPosition = songPosition,
        KeyCode = keyCode,
        Down = down
      });
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
}
