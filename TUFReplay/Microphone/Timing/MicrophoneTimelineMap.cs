using System;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Timing;

public static class MicrophoneTimelineMap
{
  public static bool HasPoints(MicrophoneTimeline timeline) => timeline?.Points != null && timeline.Points.Length > 0;

  // WAV time, in microseconds, recorded at a gameplay timeline position.
  public static double ToAudioTimeUs(MicrophoneTimeline timeline, long timelineTimeUs)
  {
    MicrophoneTimelinePoint[] points = timeline.Points;
    int index = PointAt(points, timelineTimeUs);
    MicrophoneTimelinePoint point = points[index];
    if (timelineTimeUs >= point.TimelineTimeUs && index + 1 < points.Length && !points[index + 1].StartsSection)
    {
      MicrophoneTimelinePoint next = points[index + 1];
      long span = next.TimelineTimeUs - point.TimelineTimeUs;
      if (span > 0)
        return point.AudioTimeUs
          + (next.AudioTimeUs - point.AudioTimeUs) * ((timelineTimeUs - point.TimelineTimeUs) / (double)span);
    }
    return point.AudioTimeUs + (timelineTimeUs - point.TimelineTimeUs) / ValidRate(point.GameplayRate);
  }

  // First frame that may be heard. Earlier frames hold the wait of an
  // EnhancedCountdown frozen start for its first input.
  public static long AudibleFromFrame(MicrophoneTimeline timeline, int sampleRate)
  {
    long? audibleFromUs = timeline?.AudibleFromUs;
    if (!audibleFromUs.HasValue || audibleFromUs.Value <= 0L || sampleRate <= 0)
      return 0L;
    return (long)Math.Ceiling(audibleFromUs.Value * (double)sampleRate / 1_000_000d);
  }

  // Last point at or before the position; earlier positions use the first point.
  private static int PointAt(MicrophoneTimelinePoint[] points, long timelineTimeUs)
  {
    int low = 0;
    int high = points.Length - 1;
    while (low < high)
    {
      int middle = (low + high + 1) / 2;
      if (points[middle].TimelineTimeUs <= timelineTimeUs)
        low = middle;
      else
        high = middle - 1;
    }
    return low;
  }

  internal static double ValidRate(double rate) =>
    rate > 0d && !double.IsNaN(rate) && !double.IsInfinity(rate) ? rate : 1d;
}

// Maps nondecreasing WAV times back to the gameplay timeline. Audio captured
// while gameplay stood still between two sections has no timeline position.
public sealed class MicrophoneAudioTimelineCursor
{
  private readonly MicrophoneTimelinePoint[] _points;
  private int _index;

  public MicrophoneAudioTimelineCursor(MicrophoneTimeline timeline)
  {
    if (!MicrophoneTimelineMap.HasPoints(timeline))
      throw new ArgumentException("The microphone timeline has no points.", nameof(timeline));
    _points = timeline.Points;
  }

  public bool TryMap(double audioTimeUs, out double timelineTimeUs)
  {
    while (_index + 1 < _points.Length && audioTimeUs >= _points[_index + 1].AudioTimeUs)
      _index++;
    MicrophoneTimelinePoint point = _points[_index];
    MicrophoneTimelinePoint next = _index + 1 < _points.Length ? _points[_index + 1] : null;
    double timelinePerAudio =
      next != null && !next.StartsSection && next.AudioTimeUs > point.AudioTimeUs
        ? (next.TimelineTimeUs - point.TimelineTimeUs) / (double)(next.AudioTimeUs - point.AudioTimeUs)
        : MicrophoneTimelineMap.ValidRate(point.GameplayRate);
    timelineTimeUs = point.TimelineTimeUs + (audioTimeUs - point.AudioTimeUs) * timelinePerAudio;
    return next == null || !next.StartsSection || timelineTimeUs < next.TimelineTimeUs;
  }
}
