namespace TUFReplay.Domain.Microphone;

public sealed class CapturedMicrophoneRecording
{
  public string RunId;
  public string TempPath;
  public string DeviceId;
  public int SampleRate = 48000;
  public int Channels = 1;
  public long FrameCount;
  public long CaptureStartOffsetUs;
}
