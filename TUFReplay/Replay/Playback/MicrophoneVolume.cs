namespace TUFReplay.Replay.Playback;

// The recorded microphone's volume, set in ADOFAI's audio settings. In-game
// replays and renders both use it. Off leaves the microphone out of both.
public readonly struct MicrophoneVolume
{
  public static readonly MicrophoneVolume Default = new MicrophoneVolume(0d, false);

  private MicrophoneVolume(double decibels, bool off)
  {
    Decibels = decibels;
    Off = off;
  }

  public double Decibels { get; }
  public bool Off { get; }

  // OBS Gain-filter multiplier; Off is silent.
  public float Multiplier => Off ? 0f : MicrophoneGain.FromDecibels(Decibels);

  public static MicrophoneVolume Create(double decibels, bool off) =>
    new MicrophoneVolume(MicrophoneGain.Normalize(decibels), off);

  public override string ToString() => Off ? "off" : Decibels + " dB";
}
