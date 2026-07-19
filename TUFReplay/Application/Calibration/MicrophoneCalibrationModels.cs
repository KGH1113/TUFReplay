namespace TUFReplay.Application.Calibration;

public static class MicrophoneCalibrationStates
{
  public const string Idle = "idle";
  public const string Arming = "arming";
  public const string OpeningLevel = "opening_level";
  public const string WaitingForRun = "waiting_for_run";
  public const string Recording = "recording";
  public const string Processing = "processing";
  public const string Editing = "editing";
  public const string PreviewStarting = "preview_starting";
  public const string PreviewPlaying = "preview_playing";
  public const string Error = "error";
}

public sealed class MicrophoneCalibrationStatus
{
  public string OperationId;
  public string State = MicrophoneCalibrationStates.Idle;
  public string ErrorCode;
  public string Message;
  public double DurationMs;
  public double PlaybackPositionMs;
  public int ResultRevision;
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;
}

public sealed class MicrophoneCalibrationResult
{
  public string OperationId;
  public int Revision;
  public double DurationMs;
  public float[] GameWaveform;
  public float[] MicrophoneWaveform;
}
