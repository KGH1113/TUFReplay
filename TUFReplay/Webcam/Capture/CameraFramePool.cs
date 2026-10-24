using System;
using System.Collections.Generic;
using System.Threading;

namespace TUFReplay.Webcam.Capture;

// Capture, preview and encoding share immutable frames. A buffer returns only
// after every consumer releases it; a slow consumer cannot grow the pool.
internal sealed class CameraFramePool
{
  private readonly object _gate = new object();
  private readonly Stack<CameraFrame> _available = new Stack<CameraFrame>();

  public CameraFramePool(int byteCount, int capacity = 8)
  {
    if (byteCount <= 0 || capacity <= 0)
      throw new ArgumentOutOfRangeException();
    // Allocate while the camera is arming, never on a run's first frames.
    for (int index = 0; index < capacity; index++)
      _available.Push(new CameraFrame(this, byteCount));
  }

  public CameraFrame TryRent()
  {
    lock (_gate)
    {
      if (_available.Count == 0)
        return null;
      CameraFrame frame = _available.Pop();
      frame.Rent();
      return frame;
    }
  }

  internal void Return(CameraFrame frame)
  {
    lock (_gate)
      _available.Push(frame);
  }
}

internal sealed class CameraFrame
{
  private readonly CameraFramePool _pool;
  private int _references;

  internal CameraFrame(CameraFramePool pool, int byteCount)
  {
    _pool = pool;
    Pixels = new byte[byteCount];
  }

  public byte[] Pixels { get; }

  // Set only by the exclusive capture owner, before publication.
  public long TimestampTicks { get; set; }

  public bool IsFresh(long now, long frequency) =>
    TimestampTicks > 0 && frequency > 0 && now >= TimestampTicks && now - TimestampTicks <= frequency / 4;

  internal void Rent()
  {
    _references = 1;
    TimestampTicks = 0;
  }

  public CameraFrame Retain()
  {
    Interlocked.Increment(ref _references);
    return this;
  }

  public void Release()
  {
    if (Interlocked.Decrement(ref _references) == 0)
      _pool.Return(this);
  }
}
