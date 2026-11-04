namespace TUFReplay.Microphone.Models;

public sealed class StoredMicrophoneRecording
{
  public string RunId;
  public string FilePath;
  public string Format;
  public int SampleRate;
  public int Channels;
  public long FrameCount;
  public string DeviceId;
  public long CaptureStartOffsetUs;
  public long ByteLength;

  // Null for recordings made before timeline maps; they use CaptureStartOffsetUs.
  public MicrophoneTimeline Timeline;
}
