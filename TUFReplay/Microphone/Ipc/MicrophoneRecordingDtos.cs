namespace TUFReplay.Microphone.Ipc;

public sealed class MicrophoneRecordingDeleteResultDto
{
  public string RunId;
  public bool Deleted;
}

public sealed class MicrophoneRecordingKeepResultDto
{
  public string RunId;
  public bool Permanent;
}
