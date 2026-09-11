using System;

namespace TUFReplay.Replay.Playback;

public static class MicrophoneGain
{
  public static float FromDecibels(double decibels) => (float)Math.Pow(10d, decibels / 20d);
}
