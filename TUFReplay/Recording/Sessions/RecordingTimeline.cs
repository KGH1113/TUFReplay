using System;

namespace TUFReplay.Recording.Sessions;

// Accessed under RecordingSession's lock. Native inputs can be mapped after hits
// that happened later, so their ordering floor must not advance on every hit.
internal sealed class RecordingTimeline
{
  private long? _lastInputOrBoundaryTimeUs;
  private long? _lastEventTimeUs;

  public void Reset()
  {
    _lastInputOrBoundaryTimeUs = null;
    _lastEventTimeUs = null;
  }

  public long Clamp(long timeUs)
  {
    return _lastEventTimeUs.HasValue ? Math.Max(_lastEventTimeUs.Value, timeUs) : timeUs;
  }

  public long RecordInput(long timeUs)
  {
    if (_lastInputOrBoundaryTimeUs.HasValue)
      timeUs = Math.Max(_lastInputOrBoundaryTimeUs.Value, timeUs);
    _lastInputOrBoundaryTimeUs = timeUs;
    _lastEventTimeUs = Clamp(timeUs);
    return timeUs;
  }

  public long RecordHit(long timeUs)
  {
    timeUs = Math.Max(0L, Clamp(timeUs));
    _lastEventTimeUs = timeUs;
    return timeUs;
  }

  public long RecordBoundary(long timeUs)
  {
    timeUs = Clamp(timeUs);
    _lastInputOrBoundaryTimeUs = timeUs;
    _lastEventTimeUs = timeUs;
    return timeUs;
  }
}
