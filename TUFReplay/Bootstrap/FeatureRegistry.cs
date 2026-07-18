using System.Linq;
using HarmonyLib;
using TUFReplay.Features.Calibration;
using TUFReplay.Features.Gameplay;
using TUFReplay.Features.Ipc;
using TUFReplay.Features.Microphone;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;
using TUFReplay.Features.Ui;

namespace TUFReplay.Bootstrap;

public static class FeatureRegistry
{
  private const string HarmonyId = "TUFReplay";
  private static Harmony _harmony;

  public static TUFReplayIpcFeature Ipc { get; private set; }
  public static RecordingFeature Recording { get; private set; }
  public static ReplayFeature Replay { get; private set; }
  public static MicrophoneRecordingToastFeature MicrophoneRecordingToast { get; private set; }
  public static MicrophoneRecordingFeature MicrophoneRecording { get; private set; }
  public static MicrophoneCalibrationFeature MicrophoneCalibration { get; private set; }

  public static void Initialize()
  {
    if (_harmony != null)
      return;

    Ipc = new TUFReplayIpcFeature();
    Recording = new RecordingFeature();
    Replay = new ReplayFeature();
    MicrophoneRecordingToast = new MicrophoneRecordingToastFeature();
    MicrophoneRecording = new MicrophoneRecordingFeature();
    MicrophoneCalibration = new MicrophoneCalibrationFeature();

    _harmony = new Harmony(HarmonyId);
    try
    {
      _harmony.PatchAll(typeof(FeatureRegistry).Assembly);
      if (!_harmony.GetPatchedMethods().Any())
        throw new System.InvalidOperationException("Harmony did not apply any TUFReplay patches.");
      MicrophoneRecordingToast.Enable();
      MicrophoneRecording.Enable();
      MicrophoneCalibration.Enable();
      Ipc.Enable();
      Recording.Enable();
      Replay.Enable();
    }
    catch
    {
      Shutdown();
      throw;
    }
  }

  public static void Shutdown()
  {
    Replay?.Disable();
    Recording?.Disable();
    Ipc?.Disable();
    MicrophoneCalibration?.Disable();
    MicrophoneRecording?.Disable();
    MicrophoneRecordingToast?.Disable();

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;

    Replay = null;
    MicrophoneRecordingToast = null;
    MicrophoneRecording = null;
    MicrophoneCalibration = null;
    Recording = null;
    Ipc = null;
  }
}
