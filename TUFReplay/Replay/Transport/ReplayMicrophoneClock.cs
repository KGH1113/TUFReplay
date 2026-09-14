using System;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Transport;

public static class ReplayMicrophoneClock
{
  public static long ApplyLatencyCorrection(long captureStartOffsetUs, long microphoneLatencyUs) =>
    captureStartOffsetUs - microphoneLatencyUs;

  public static double ToMicrophoneTimeUs(
    long replayTimeUs,
    double gameplayRate,
    long captureStartOffsetUs,
    long? wonTimeUs = null
  )
  {
    double rate =
      gameplayRate > 0d && !double.IsNaN(gameplayRate) && !double.IsInfinity(gameplayRate) ? gameplayRate : 1d;
    double elapsedUs =
      wonTimeUs.HasValue && replayTimeUs >= wonTimeUs.Value
        ? wonTimeUs.Value / rate + (replayTimeUs - wonTimeUs.Value)
        : replayTimeUs / rate;
    return elapsedUs - captureStartOffsetUs;
  }

  public static long ToFrame(
    long replayTimeUs,
    double timelineRate,
    long captureStartOffsetUs,
    int sampleRate,
    long frameCount,
    long? wonTimeUs = null
  )
  {
    if (sampleRate <= 0 || frameCount < 0)
      throw new ArgumentOutOfRangeException(nameof(sampleRate));

    double microphoneTimeUs = ToMicrophoneTimeUs(replayTimeUs, timelineRate, captureStartOffsetUs, wonTimeUs);
    if (microphoneTimeUs <= 0d)
      return 0L;

    double frame = microphoneTimeUs * sampleRate / 1_000_000d;
    if (frame >= frameCount)
      return frameCount;
    return Math.Max(0L, (long)frame);
  }

  public static long ToFrame(
    ReplayPlaybackSnapshot snapshot,
    long captureStartOffsetUs,
    int sampleRate,
    long frameCount
  )
  {
    // songposition_minusi has already moved the replay timeline backwards by
    // ADOFAI's input calibration. Undo that movement for microphone audio so
    // the user setting represents microphone latency only.
    long physicalCaptureOffsetUs = ApplyLatencyCorrection(captureStartOffsetUs, snapshot.GameInputOffsetUs);
    return ToFrame(
      snapshot.TimelineTimeUs,
      snapshot.GameplayRate,
      physicalCaptureOffsetUs,
      sampleRate,
      frameCount,
      snapshot.WonTimeUs
    );
  }
}
