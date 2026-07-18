namespace TUFReplay.Application.Microphone;

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
