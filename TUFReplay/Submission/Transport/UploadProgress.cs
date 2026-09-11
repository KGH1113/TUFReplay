namespace TUFReplay.Submission.Transport;

public enum UploadProgressKind
{
  Connecting,
  Connected,
  HelloSent,
  Ready,
  FrameSent,
  Acknowledged,
  Reconnecting,
  CompleteSent,
  Sealed,
}

public readonly struct UploadProgress
{
  public UploadProgressKind Kind { get; }
  public long Sequence { get; }
  public int Bytes { get; }
  public int DelayMilliseconds { get; }

  public UploadProgress(UploadProgressKind kind, long sequence = -1L, int bytes = 0, int delayMilliseconds = 0)
  {
    Kind = kind;
    Sequence = sequence;
    Bytes = bytes;
    DelayMilliseconds = delayMilliseconds;
  }
}
