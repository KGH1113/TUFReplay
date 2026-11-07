using System;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Timing;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Transport;

namespace TUFReplay.Microphone.Playback;

internal static class MicrophonePlaybackClock
{
  // WAV time to mix at a replay timeline position. The user offset compensates
  // microphone latency; the game input offset moves each sound to the song
  // position at which ADOFAI judged the input recorded with it.
  public static double ToAudioTimeUs(
    StoredMicrophoneRecording recording,
    ReplayPlaybackSnapshot snapshot,
    long timelineTimeUs,
    long userLatencyUs
  )
  {
    long latencyUs = userLatencyUs + snapshot.GameInputOffsetUs;
    if (MicrophoneTimelineMap.HasPoints(recording.Timeline))
      return MicrophoneTimelineMap.ToAudioTimeUs(recording.Timeline, timelineTimeUs) + latencyUs;
    return ReplayMicrophoneClock.ToMicrophoneTimeUs(
      timelineTimeUs,
      snapshot.GameplayRate,
      ReplayMicrophoneClock.ApplyLatencyCorrection(recording.CaptureStartOffsetUs, latencyUs),
      snapshot.WonTimeUs
    );
  }

  public static long ToFrame(double audioTimeUs, int sampleRate, long frameCount)
  {
    if (double.IsNaN(audioTimeUs) || audioTimeUs <= 0d)
      return 0L;
    double frame = audioTimeUs * sampleRate / 1_000_000d;
    return frame >= frameCount ? frameCount : Math.Max(0L, (long)frame);
  }

  // First frame that may be heard; earlier frames precede the audible part.
  public static long AudibleFromFrame(StoredMicrophoneRecording recording, int sampleRate) =>
    MicrophoneTimelineMap.AudibleFromFrame(recording?.Timeline, sampleRate);
}
