using TUFReplay.Unity.Notifications;
using UnityEngine;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  public sealed class MicrophonePermissionWarningPreviewDriver : MonoBehaviour
  {
    [SerializeField]
    private MicrophonePermissionWarningView view;

    public void Configure(MicrophonePermissionWarningView notificationView)
    {
      view = notificationView;
    }

    private void Start()
    {
      view?.ShowPersistent(
        "Input Monitoring is required",
        "Allow A Dance of Fire and Ice in System Settings → Privacy & Security → Input Monitoring, then restart the game.",
        "Open System Settings",
        () => Debug.Log("[TUFReplay.Unity] Runtime notification action invoked.")
      );
    }
  }
}
