using TUFReplay.Unity.Notifications;
using UnityEngine;

namespace TUFReplay.Shared.Notifications;

/// <summary>Presentation adapter for the configured toast prefab in the runtime AssetBundle.</summary>
internal static class AssetBundleToast
{
  private static MicrophonePermissionWarningView _view;

  internal static void Initialize(GameObject prefab, Transform parent)
  {
    var instance = Object.Instantiate(prefab, parent, false);
    instance.name = "SubmissionNotificationRuntime";
    _view = instance.GetComponent<MicrophonePermissionWarningView>();
    _view?.ResetImmediate();
  }

  internal static bool Show(string title, string message)
  {
    if (_view == null || !_view.IsConfigured) return false;
    _view.Show(title, message);
    return true;
  }
}
