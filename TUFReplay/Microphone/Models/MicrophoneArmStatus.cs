namespace TUFReplay.Microphone.Models;

public enum MicrophoneArmState
{
  Idle,
  Arming,
  Armed,
  Failed,
}

public sealed class MicrophoneArmStatus
{
  public MicrophoneArmState State;
  public string Error;
}
