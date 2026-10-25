namespace TUFReplay.Microphone.Models;

public sealed class MicrophoneDeviceInfo
{
  public string Id;
  public string Name;
  public int MinFrequency;
  public int MaxFrequency;

  // Opening a Bluetooth headset microphone switches the headset to its hands-free
  // profile, which lowers game audio quality while the microphone is open.
  public bool Bluetooth;
  public bool IsDefault;
}
