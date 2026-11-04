using System;

namespace TUFReplay.Replay.Playback;

// Matches OBS Studio's Gain filter: a -30 dB to +30 dB slider in 0.1 dB steps,
// applied as a plain linear multiplier. Clipping happens only at the final mix.
public static class MicrophoneGain
{
  public const double MinDecibels = -30d;
  public const double MaxDecibels = 30d;
  private const double StepsPerDecibel = 10d;

  // The companion rounds the same way, so both sides agree on every saved value.
  public static double Normalize(double decibels)
  {
    if (double.IsNaN(decibels))
      return 0d;
    double clamped = Math.Max(MinDecibels, Math.Min(MaxDecibels, decibels));
    double rounded = Math.Round(clamped * StepsPerDecibel, MidpointRounding.AwayFromZero) / StepsPerDecibel;
    return rounded == 0d ? 0d : rounded;
  }

  // OBS db_to_mul: powf(10, db / 20) in single precision; a non-finite gain is silent.
  public static float FromDecibels(double decibels)
  {
    float db = (float)decibels;
    if (float.IsNaN(db) || float.IsInfinity(db))
      return 0f;
    return (float)Math.Pow(10d, db / 20f);
  }
}
