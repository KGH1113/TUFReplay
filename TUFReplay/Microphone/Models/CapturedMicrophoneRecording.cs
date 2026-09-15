namespace TUFReplay.Microphone.Models;

public sealed class CapturedMicrophoneRecording
{
  public string RunId;
  public string TempPath;
  public string DeviceId;
  public int SampleRate = 48000;
  public int Channels = 1;
  public long FrameCount;

  // Runtime-only timestamp of the first WAV sample, in the native input Stopwatch clock.
  public long CaptureStartTimestampTicks;
  public long CaptureStartOffsetUs;
}
