namespace TUFReplay.Webcam.Capture;

// The caller holds its sink lock. Rotate ownership instead of copying an idle
// camera frame or letting the producer overwrite the frame being attached.
internal sealed class LatestCameraFrame
{
  private byte[] _pixels;
  private long _timestamp;

  internal byte[] Exchange(byte[] complete, long timestamp)
  {
    byte[] previous = _pixels;
    _pixels = complete;
    _timestamp = timestamp;
    return previous != null && previous.Length == complete.Length ? previous : new byte[complete.Length];
  }

  internal bool TryGet(long now, long frequency, out byte[] pixels, out long timestamp)
  {
    pixels = _pixels;
    timestamp = _timestamp;
    return pixels != null && timestamp > 0 && now >= timestamp && frequency > 0 && now - timestamp <= frequency / 4;
  }

  internal void Clear()
  {
    _pixels = null;
    _timestamp = 0;
  }
}
