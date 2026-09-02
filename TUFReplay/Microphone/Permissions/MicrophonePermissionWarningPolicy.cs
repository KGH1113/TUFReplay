using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Permissions;

internal sealed class MicrophonePermissionWarningPolicy
{
  internal bool HasShown { get; private set; }

  internal bool ShouldShow(MicrophonePermissionState state)
  {
    return !HasShown && IsWarningState(state);
  }

  internal void MarkShown()
  {
    HasShown = true;
  }

  internal void Reset()
  {
    HasShown = false;
  }

  internal static bool IsWarningState(MicrophonePermissionState state)
  {
    return state == MicrophonePermissionState.Denied
      || state == MicrophonePermissionState.Restricted
      || state == MicrophonePermissionState.Failed;
  }
}
