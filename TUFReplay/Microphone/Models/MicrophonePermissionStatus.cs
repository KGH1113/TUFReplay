namespace TUFReplay.Microphone.Models;

public enum MicrophonePermissionState
{
  NotApplicable,
  Unknown,
  Requesting,
  Checking,
  Authorized,
  Denied,
  Restricted,
  Failed,
}

public sealed class MicrophonePermissionStatus
{
  public MicrophonePermissionState State;
  public string Error;
}
