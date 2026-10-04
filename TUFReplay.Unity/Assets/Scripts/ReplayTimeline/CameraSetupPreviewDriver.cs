#if UNITY_EDITOR
using UnityEngine;

namespace TUFReplay.Unity.CameraSetup
{
  [ExecuteAlways]
  [DisallowMultipleComponent]
  public sealed class CameraSetupPreviewDriver : MonoBehaviour
  {
    [SerializeField]
    private CameraSetupView view;

    [SerializeField]
    private bool korean;

    [SerializeField]
    private bool cameraEnabled;

    [SerializeField]
    private bool liveVisible;

    public CameraSetupView View => view;

    public void Configure(CameraSetupView cameraView)
    {
      view = cameraView;
      Refresh();
    }

    private void OnEnable() => Refresh();

    private void OnValidate()
    {
      UnityEditor.EditorApplication.delayCall += () =>
      {
        if (this != null && isActiveAndEnabled)
          Refresh();
      };
    }

    public void Refresh()
    {
      if (view == null || !view.IsConfigured)
        return;
      view.Show(
        korean,
        true,
        value =>
        {
          cameraEnabled = value;
          ApplyState();
        },
        value =>
        {
          liveVisible = value;
          ApplyState();
        },
        view.Hide,
        view.Hide,
        view.Hide,
        ApplyState
      );
      ApplyState();
    }

    private void ApplyState()
    {
      view.SetState(cameraEnabled, liveVisible, true, true, "Preview camera", null, null, false);
    }
  }
}
#endif
