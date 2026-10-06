using System;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Transport;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Timing;

public static class WebcamPlaybackClock
{
  // Positive correction advances the image, matching the microphone convention.
  public static double ToVideoSeconds(ReplayPlaybackSnapshot snapshot, WebcamRecording recording, int offsetMs)
  {
    WebcamTimelineSegment segment = SegmentAt(snapshot.TimelineTimeUs, recording);
    if (segment != null)
      return (segment.VideoTimeUs + (snapshot.TimelineTimeUs - segment.TimelineTimeUs) / segment.GameplayRate)
          / 1_000_000d
        + offsetMs / 1000d;
    return ReplayMicrophoneClock.ToMicrophoneTimeUs(
        snapshot.TimelineTimeUs,
        recording.GameplayRate,
        recording.CaptureStartOffsetUs - offsetMs * 1000L,
        snapshot.WonTimeUs
      ) / 1_000_000d;
  }

  public static double PlaybackRate(ReplayPlaybackSnapshot snapshot, WebcamRecording recording)
  {
    WebcamTimelineSegment segment = SegmentAt(snapshot.TimelineTimeUs, recording);
    double rate =
      segment != null ? snapshot.TimelineRate / segment.GameplayRate
      : snapshot.WonTimeUs.HasValue && snapshot.TimelineTimeUs >= snapshot.WonTimeUs.Value ? snapshot.TimelineRate
      : snapshot.TimelineRate / recording.GameplayRate;
    return rate > 0 && !double.IsNaN(rate) && !double.IsInfinity(rate) ? rate : 1d;
  }

  private static WebcamTimelineSegment SegmentAt(long timeUs, WebcamRecording recording)
  {
    WebcamTimelineSegment[] segments = recording.Timeline;
    if (segments == null || segments.Length == 0)
      return null;
    int low = 0,
      high = segments.Length - 1;
    while (low < high)
    {
      int middle = (low + high + 1) / 2;
      if (segments[middle].TimelineTimeUs <= timeUs)
        low = middle;
      else
        high = middle - 1;
    }
    return segments[low];
  }
}
