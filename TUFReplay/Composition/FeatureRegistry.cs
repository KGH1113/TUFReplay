using System.Linq;
using HarmonyLib;
using TUFReplay.Calibration.Sessions;
using TUFReplay.Composition;
using TUFReplay.Microphone.Recording;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Composition;

public static class FeatureRegistry
{
  private const string HarmonyId = "TUFReplay";
  private static Harmony _harmony;

  public static TUFReplayIpcFeature Ipc { get; private set; }
  public static RecordingFeature Recording { get; private set; }
  public static ReplayFeature Replay { get; private set; }
  public static MicrophoneRecordingFeature MicrophoneRecording { get; private set; }
  public static MicrophoneCalibrationFeature MicrophoneCalibration { get; private set; }

  public static void Initialize()
  {
    if (_harmony != null)
      return;

    Ipc = new TUFReplayIpcFeature();
    Recording = new RecordingFeature();
    Replay = new ReplayFeature();
    MicrophoneRecording = new MicrophoneRecordingFeature();
    MicrophoneCalibration = new MicrophoneCalibrationFeature();

    _harmony = new Harmony(HarmonyId);
    try
    {
      _harmony.PatchAll(typeof(FeatureRegistry).Assembly);
      if (!_harmony.GetPatchedMethods().Any())
        throw new System.InvalidOperationException("Harmony did not apply any TUFReplay patches.");
      MicrophoneRecording.Enable();
      MicrophoneCalibration.Enable();
      Recording.Enable();
      Replay.Enable();
      Ipc.Enable();
    }
    catch
    {
      Shutdown();
      throw;
    }
  }

  public static void Shutdown()
  {
    Ipc?.Disable();
    Replay?.Disable();
    Recording?.Disable();
    MicrophoneCalibration?.Disable();
    MicrophoneRecording?.Disable();

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;

    Replay = null;
    MicrophoneRecording = null;
    MicrophoneCalibration = null;
    Recording = null;
    Ipc = null;
  }
}
