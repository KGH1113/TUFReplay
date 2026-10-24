using System;
using System.Collections.Generic;
using System.Threading;

namespace TUFReplay.Webcam.Capture;

// Never wait on an encoder/preview worker from the capture thread. Replacing
// stale pending frames bounds both memory and latency; their PTS are preserved
// by the timestamped recording stream instead of compressing elapsed time.
internal sealed class CameraFrameQueue : IDisposable
{
  private readonly object _gate = new object();
  private readonly Queue<CameraFrame> _frames;
  private readonly int _capacity;
  private bool _completed;
  private long _dropped;

  public CameraFrameQueue(int capacity)
  {
    if (capacity < 1)
      throw new ArgumentOutOfRangeException(nameof(capacity));
    _capacity = capacity;
    _frames = new Queue<CameraFrame>(capacity);
  }

  public long Dropped => Interlocked.Read(ref _dropped);

  public bool Submit(CameraFrame frame)
  {
    lock (_gate)
    {
      if (_completed)
        return false;
      if (_frames.Count == _capacity)
      {
        _frames.Dequeue().Release();
        Interlocked.Increment(ref _dropped);
      }
      _frames.Enqueue(frame.Retain());
      Monitor.Pulse(_gate);
      return true;
    }
  }

  public CameraFrame Take()
  {
    lock (_gate)
    {
      while (_frames.Count == 0 && !_completed)
        Monitor.Wait(_gate);
      return _frames.Count > 0 ? _frames.Dequeue() : null;
    }
  }

  public void Complete()
  {
    lock (_gate)
    {
      _completed = true;
      Monitor.PulseAll(_gate);
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _completed = true;
      while (_frames.Count > 0)
        _frames.Dequeue().Release();
      Monitor.PulseAll(_gate);
    }
  }
}
