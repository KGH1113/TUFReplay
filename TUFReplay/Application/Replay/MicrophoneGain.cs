using System;

namespace TUFReplay.Application.Replay;

public static class MicrophoneGain
{
  public static float FromDecibels(double decibels) => (float)Math.Pow(10d, decibels / 20d);
}
