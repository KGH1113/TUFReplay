using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Features.Calibration;

public sealed class MicrophoneCalibrationTicker : MonoBehaviour
{
  public MicrophoneCalibrationFeature Feature;

  private void Update()
  {
    UnityMainThread.DrainPending();
    Feature?.Tick();
  }
}
