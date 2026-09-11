using TUFReplay.Unity.Notifications;
using UnityEngine;

namespace TUFReplay.Unity.ReplayTimeline
{
  [DisallowMultipleComponent]
  public sealed class MicrophonePermissionWarningPreviewDriver : MonoBehaviour
  {
    [SerializeField]
    private MicrophonePermissionWarningView view;

    public void Configure(MicrophonePermissionWarningView warningView)
    {
      view = warningView;
    }

    private void Start()
    {
      view?.Show();
    }
  }
}
