using System.Linq;
using HarmonyLib;
using TUFReplay.Features.Gameplay;
using TUFReplay.Features.Ipc;
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

  public static void Initialize()
  {
    if (_harmony != null)
      return;

    Ipc = new TUFReplayIpcFeature();
    Recording = new RecordingFeature();
    Replay = new ReplayFeature();
    MicrophoneRecordingToast = new MicrophoneRecordingToastFeature();

    _harmony = new Harmony(HarmonyId);
    try
    {
      _harmony.PatchAll(typeof(FeatureRegistry).Assembly);
      if (!_harmony.GetPatchedMethods().Any())
        throw new System.InvalidOperationException("Harmony did not apply any TUFReplay patches.");
      Ipc.Enable();
      Recording.Enable();
      Replay.Enable();
      MicrophoneRecordingToast.Enable();
    }
    catch
    {
      Shutdown();
      throw;
    }
  }

  public static void Shutdown()
  {
    MicrophoneRecordingToast?.Disable();
    Replay?.Disable();
    Recording?.Disable();
    Ipc?.Disable();

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;

    Replay = null;
    MicrophoneRecordingToast = null;
    Recording = null;
    Ipc = null;
  }
}
