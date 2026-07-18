using TUFReplay.Application.Calibration;

namespace TUFReplay.Ipc.Dtos;

public sealed class MicrophoneCalibrationStatusDto
{
  public string OperationId;
  public string State;
  public string ErrorCode;
  public string Message;
  public double DurationMs;
  public double PlaybackPositionMs;
  public int ResultRevision;
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;

  public static MicrophoneCalibrationStatusDto From(MicrophoneCalibrationStatus status)
  {
    status ??= new MicrophoneCalibrationStatus();
    return new MicrophoneCalibrationStatusDto
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
}

public sealed class MicrophoneCalibrationResultDto
{
  public string OperationId;
  public int Revision;
  public double DurationMs;
  public float[] GameWaveform;
  public float[] MicrophoneWaveform;

  public static MicrophoneCalibrationResultDto From(MicrophoneCalibrationResult result) =>
    new MicrophoneCalibrationResultDto
    {
      OperationId = result.OperationId,
      Revision = result.Revision,
      DurationMs = result.DurationMs,
      GameWaveform = result.GameWaveform,
      MicrophoneWaveform = result.MicrophoneWaveform,
    };
}
