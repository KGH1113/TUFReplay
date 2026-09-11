using TUFReplay.Shared.Unity;
using UnityEngine;

namespace TUFReplay.Calibration.Sessions;

public sealed class MicrophoneCalibrationTicker : MonoBehaviour
{
  public MicrophoneCalibrationFeature Feature;

  private void Update()
  {
    UnityMainThread.DrainPending();
    Feature?.Tick();
  }
}
