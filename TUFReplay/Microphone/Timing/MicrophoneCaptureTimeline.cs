using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Timing;

// Builds a recording's microphone timeline from conductor anchors. ADOFAI derives
// each song position from a DSP clock that advances once per audio buffer, so a
// single anchor is stale by a random 0-45 ms and would shift the whole recording
// by that much. Averaging every anchor of a two-second window keeps that error
// constant across runs, and comparing windows finds pauses and rate changes.
public sealed class MicrophoneCaptureTimeline
{
  internal const long WindowRealUs = 2_000_000L;
  internal const double SectionBreakUs = 60_000d;
  internal const long FrozenStartPrerollUs = 100_000L;
  private const double RateTolerance = 0.0001d;
  private static readonly double MicrosecondsPerTick = 1_000_000d / Stopwatch.Frequency;

  private readonly List<Window> _windows = new List<Window>();
  private Window _current;
  private long? _wonTimelineUs;

  public bool HasAnchor => _windows.Count > 0;

  // Both values come from the same conductor update: capture ticks on the native
  // input Stopwatch clock, and that update's gameplay timeline position.
  public void Observe(long captureTicks, long timelineTimeUs, double gameplayRate)
  {
    if (_wonTimelineUs.HasValue || captureTicks <= 0 || !IsValidRate(gameplayRate))
      return;
    double originUs = captureTicks * MicrosecondsPerTick - timelineTimeUs / gameplayRate;
    Window current = _current;
    if (current != null)
    {
      if (timelineTimeUs < current.LastTimelineTimeUs)
        return;
      bool sameSection =
        Math.Abs(gameplayRate - current.GameplayRate) <= RateTolerance
        && Math.Abs(originUs - current.ReferenceOriginUs) <= SectionBreakUs;
      if (sameSection)
      {
        if ((timelineTimeUs - current.StartTimelineTimeUs) / gameplayRate < WindowRealUs)
        {
          current.Add(originUs, timelineTimeUs);
          return;
        }
        StartWindow(timelineTimeUs, gameplayRate, originUs, current.MeanOriginUs, startsSection: false);
        return;
      }
    }
    StartWindow(timelineTimeUs, gameplayRate, originUs, originUs, startsSection: true);
  }

  // After the clear the timeline follows real time from the won position.
  public void ObserveWon(long wonTimelineTimeUs)
  {
    if (_windows.Count > 0 && !_wonTimelineUs.HasValue)
      _wonTimelineUs = wonTimelineTimeUs;
  }

  public MicrophoneTimeline Build(long firstSampleTicks, bool frozenStart)
  {
    if (_windows.Count == 0 || firstSampleTicks <= 0)
      throw new InvalidOperationException("The microphone recording has no gameplay clock anchor.");

    double firstSampleUs = firstSampleTicks * MicrosecondsPerTick;
    var points = new List<MicrophoneTimelinePoint>(_windows.Count + 1);
    foreach (Window window in _windows)
    {
      double realUs = window.MeanOriginUs + window.StartTimelineTimeUs / window.GameplayRate;
      points.Add(
        new MicrophoneTimelinePoint
        {
          TimelineTimeUs = window.StartTimelineTimeUs,
          AudioTimeUs = checked((long)Math.Round(realUs - firstSampleUs)),
          GameplayRate = window.GameplayRate,
          StartsSection = window.StartsSection,
        }
      );
    }

    var timeline = new MicrophoneTimeline { Points = points.ToArray() };
    if (_wonTimelineUs.HasValue && _wonTimelineUs.Value >= points[points.Count - 1].TimelineTimeUs)
    {
      long wonAudioUs = checked((long)Math.Round(MicrophoneTimelineMap.ToAudioTimeUs(timeline, _wonTimelineUs.Value)));
      points.Add(
        new MicrophoneTimelinePoint
        {
          TimelineTimeUs = _wonTimelineUs.Value,
          AudioTimeUs = wonAudioUs,
          GameplayRate = 1d,
          StartsSection = true,
        }
      );
      timeline.Points = points.ToArray();
    }

    // A frozen start waited for its first input without a moving timeline. The
    // replay plays a normal countdown there, so keep only the input's own sound.
    if (frozenStart)
      timeline.AudibleFromUs = Math.Max(0L, points[0].AudioTimeUs - FrozenStartPrerollUs);
    return timeline;
  }

  // Older readers map the whole recording from one offset; keep the first point's.
  public static long ToCaptureStartOffsetUs(MicrophoneTimeline timeline)
  {
    MicrophoneTimelinePoint first = timeline.Points[0];
    return checked(
      (long)Math.Round(first.TimelineTimeUs / MicrophoneTimelineMap.ValidRate(first.GameplayRate) - first.AudioTimeUs)
    );
  }

  private void StartWindow(
    long timelineTimeUs,
    double gameplayRate,
    double originUs,
    double referenceOriginUs,
    bool startsSection
  )
  {
    _current = new Window(timelineTimeUs, gameplayRate, referenceOriginUs, startsSection);
    _current.Add(originUs, timelineTimeUs);
    _windows.Add(_current);
  }

  private static bool IsValidRate(double rate) => rate > 0d && !double.IsNaN(rate) && !double.IsInfinity(rate);

  private sealed class Window
  {
    // Sums are kept relative to the first origin for precision.
    private readonly double _baseOriginUs;
    private double _originDeltaSumUs;
    private int _count;

    public Window(long startTimelineTimeUs, double gameplayRate, double baseOriginUs, bool startsSection)
    {
      StartTimelineTimeUs = startTimelineTimeUs;
      LastTimelineTimeUs = startTimelineTimeUs;
      GameplayRate = gameplayRate;
      StartsSection = startsSection;
      _baseOriginUs = baseOriginUs;
    }

    public long StartTimelineTimeUs { get; }
    public long LastTimelineTimeUs { get; private set; }
    public double GameplayRate { get; }
    public bool StartsSection { get; }
    public double MeanOriginUs => _count == 0 ? _baseOriginUs : _baseOriginUs + _originDeltaSumUs / _count;

    // Compare against the running mean; a section's first window starts from its
    // first anchor, later windows from the previous window's mean.
    public double ReferenceOriginUs => MeanOriginUs;

    public void Add(double originUs, long timelineTimeUs)
    {
      _originDeltaSumUs += originUs - _baseOriginUs;
      _count++;
      LastTimelineTimeUs = timelineTimeUs;
    }
  }
}
