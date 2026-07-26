using System;
using TUFReplay.Application.Calibration;
using TUFReplay.Infrastructure.Settings;

namespace TUFReplay.Features.Calibration;

internal sealed class MicrophoneCalibrationState
{
  private readonly object _gate = new object();
  private MicrophoneCalibrationStatus _status = new MicrophoneCalibrationStatus();
  private MicrophoneCalibrationResult _result;

  public bool Active
  {
    get
    {
      lock (_gate)
        return _status.State != MicrophoneCalibrationStates.Idle && _status.State != MicrophoneCalibrationStates.Error;
    }
  }

  public bool HasResult
  {
    get
    {
      lock (_gate)
        return _result != null;
    }
  }

  public MicrophoneCalibrationStatus GetStatus()
  {
    lock (_gate)
      return Clone(_status);
  }

  public MicrophoneCalibrationResult GetResult(string operationId, int revision)
  {
    lock (_gate)
    {
      if (!string.Equals(_status.OperationId, operationId, StringComparison.Ordinal) || _result == null)
        return null;
      if (revision > 0 && revision != _result.Revision)
        return null;
      return new MicrophoneCalibrationResult
      {
        OperationId = _result.OperationId,
        Revision = _result.Revision,
        DurationMs = _result.DurationMs,
        GameWaveform = (float[])_result.GameWaveform.Clone(),
        SongWaveform = (float[])_result.SongWaveform.Clone(),
        MicrophoneWaveform = (float[])_result.MicrophoneWaveform.Clone(),
      };
    }
  }

  public bool IsCurrent(string operationId)
  {
    lock (_gate)
      return !string.IsNullOrEmpty(operationId)
        && string.Equals(_status.OperationId, operationId, StringComparison.Ordinal);
  }

  public bool IsState(string operationId, string state)
  {
    lock (_gate)
      return !string.IsNullOrEmpty(operationId)
        && string.Equals(_status.OperationId, operationId, StringComparison.Ordinal)
        && string.Equals(_status.State, state, StringComparison.Ordinal);
  }

  public void Set(MicrophoneCalibrationStatus status)
  {
    lock (_gate)
      _status = status;
  }

  public void Update(string state, string message)
  {
    lock (_gate)
    {
      _status.State = state;
      _status.ErrorCode = null;
      _status.Message = message;
    }
  }

  public int CompleteResult(string operationId, double durationMs, float[] songWaveform, float[] microphoneWaveform)
  {
    lock (_gate)
    {
      if (!string.Equals(_status.OperationId, operationId, StringComparison.Ordinal))
        return 0;
      int revision = _status.ResultRevision + 1;
      _result = new MicrophoneCalibrationResult
      {
        OperationId = operationId,
        Revision = revision,
        DurationMs = durationMs,
        GameWaveform = songWaveform,
        SongWaveform = songWaveform,
        MicrophoneWaveform = microphoneWaveform,
      };
      _status.DurationMs = durationMs;
      _status.PlaybackPositionMs = 0d;
      _status.ResultRevision = revision;
      _status.State = MicrophoneCalibrationStates.Editing;
      _status.Message = "Drag the microphone waveform to align it with the game audio.";
      return revision;
    }
  }

  public void SetPlaybackPosition(double positionMs)
  {
    lock (_gate)
      _status.PlaybackPositionMs = Math.Max(0d, Math.Min(_status.DurationMs, positionMs));
  }

  public void SetOffset(int offsetMs)
  {
    lock (_gate)
      _status.MicrophoneOffsetMs = offsetMs;
  }

  public void SetVolume(int volumeDb)
  {
    lock (_gate)
      _status.MicrophoneVolumeDb = volumeDb;
  }

  public void ClearResult()
  {
    lock (_gate)
      _result = null;
  }

  public MicrophoneCalibrationStatus StaleOperation()
  {
    MicrophoneCalibrationStatus status = GetStatus();
    status.ErrorCode = "calibration_operation_stale";
    status.Message = "The calibration operation is no longer active.";
    return status;
  }

  public MicrophoneCalibrationStatus Error(string code, string message, string operationId = null)
  {
    lock (_gate)
    {
      _status = new MicrophoneCalibrationStatus
      {
        OperationId = operationId ?? _status.OperationId,
        State = MicrophoneCalibrationStates.Error,
        ErrorCode = code,
        Message = message,
        MicrophoneOffsetMs = TUFReplaySettingStore.Current?.MicrophoneOffsetMs ?? 0,
        MicrophoneVolumeDb = TUFReplaySettingStore.Current?.MicrophoneVolumeDb ?? 0,
      };
      return Clone(_status);
    }
  }

  public static MicrophoneCalibrationStatus Rejected(string code, string message) =>
    new MicrophoneCalibrationStatus
    {
      State = MicrophoneCalibrationStates.Error,
      ErrorCode = code,
      Message = message,
      MicrophoneOffsetMs = TUFReplaySettingStore.Current?.MicrophoneOffsetMs ?? 0,
      MicrophoneVolumeDb = TUFReplaySettingStore.Current?.MicrophoneVolumeDb ?? 0,
    };

  private static MicrophoneCalibrationStatus Clone(MicrophoneCalibrationStatus status) =>
    new MicrophoneCalibrationStatus
    {
      OperationId = status.OperationId,
      State = status.State,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
      DurationMs = status.DurationMs,
      PlaybackPositionMs = status.PlaybackPositionMs,
      ResultRevision = status.ResultRevision,
      MicrophoneOffsetMs = status.MicrophoneOffsetMs,
      MicrophoneVolumeDb = status.MicrophoneVolumeDb,
    };
}
