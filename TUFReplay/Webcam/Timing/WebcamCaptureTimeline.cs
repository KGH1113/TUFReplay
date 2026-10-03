using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Shared.Timing;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Timing;

// Store a new mapping only when gameplay pauses or its rate changes. Camera
// capture can stay continuous while the replay skips time spent waiting.
public sealed class WebcamCaptureTimeline
{
  private readonly List<CaptureTimelineAnchor> _segments = new List<CaptureTimelineAnchor>();
  private CaptureTimelineAnchor? _previous;
  private int _observations;
  private int _unavailableAnchors;
  private int _invalidRates;
  private int _invalidTimestamps;
  private int _backwardTimelineTimes;

  public CaptureTimelineAnchor? First => _segments.Count == 0 ? (CaptureTimelineAnchor?)null : _segments[0];

  internal void NoteUnavailableAnchor() => _unavailableAnchors++;

  internal object DiagnosticState() =>
    new
    {
      observations = _observations,
      unavailableAnchors = _unavailableAnchors,
      invalidRates = _invalidRates,
      invalidTimestamps = _invalidTimestamps,
      backwardTimelineTimes = _backwardTimelineTimes,
      segments = _segments.Count,
      first = First,
      last = _previous,
      stopwatchFrequency = Stopwatch.Frequency,
    };

  public void Observe(CaptureTimelineAnchor anchor)
  {
    _observations++;
    if (!ValidRate(anchor.GameplayRate))
    {
      _invalidRates++;
      return;
    }
    if (anchor.TimestampTicks <= 0)
    {
      _invalidTimestamps++;
      return;
    }
    bool changed = !_previous.HasValue;
    if (_previous.HasValue)
    {
      CaptureTimelineAnchor previous = _previous.Value;
      double realUs = (anchor.TimestampTicks - previous.TimestampTicks) * 1_000_000d / Stopwatch.Frequency;
      double timelineUs = (anchor.TimelineTimeUs - previous.TimelineTimeUs) / previous.GameplayRate;
      changed =
        Math.Abs(realUs - timelineUs) > 50_000 || Math.Abs(anchor.GameplayRate - previous.GameplayRate) > 0.0001;
      if (anchor.TimelineTimeUs < previous.TimelineTimeUs)
      {
        _backwardTimelineTimes++;
        return;
      }
    }
    if (changed)
    {
      if (_segments.Count > 0 && _segments[_segments.Count - 1].TimelineTimeUs == anchor.TimelineTimeUs)
        _segments[_segments.Count - 1] = anchor;
      else
        _segments.Add(anchor);
    }
    _previous = anchor;
  }

  public void ApplyTo(WebcamRecording recording)
  {
    if (!First.HasValue)
      throw new InvalidOperationException("The camera recording has no gameplay clock anchor.");
    recording.CaptureStartOffsetUs = First.Value.ToCaptureStartOffsetUs(recording.CaptureStartTimestampTicks);
    recording.GameplayRate = First.Value.GameplayRate;
    var timeline = new WebcamTimelineSegment[_segments.Count];
    for (int index = 0; index < timeline.Length; index++)
    {
      CaptureTimelineAnchor anchor = _segments[index];
      timeline[index] = new WebcamTimelineSegment
      {
        TimelineTimeUs = anchor.TimelineTimeUs,
        GameplayRate = anchor.GameplayRate,
        VideoTimeUs = (long)
          Math.Round((anchor.TimestampTicks - recording.CaptureStartTimestampTicks) * 1_000_000d / Stopwatch.Frequency),
      };
    }
    recording.Timeline = timeline;
  }

  private static bool ValidRate(double rate) => rate > 0 && !double.IsNaN(rate) && !double.IsInfinity(rate);
}
