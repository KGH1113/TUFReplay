using HarmonyLib;
using TUFReplay.Features.Gameplay;
using TUFReplay.Features.Ipc;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;

namespace TUFReplay.Bootstrap;

public static class FeatureRegistry
{
  private const string HarmonyId = "TUFReplay";
  private static Harmony _harmony;

  public static TUFReplayIpcFeature Ipc { get; private set; }
  public static RecordingFeature Recording { get; private set; }
  public static ReplayFeature Replay { get; private set; }

  public static void Initialize()
  {
    if (_harmony != null)
      return;

    Ipc = new TUFReplayIpcFeature();
    Recording = new RecordingFeature();
    Replay = new ReplayFeature();

    _harmony = new Harmony(HarmonyId);
    try
    {
      _harmony.PatchAll(typeof(FeatureRegistry).Assembly);
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

    _harmony?.UnpatchAll(HarmonyId);
    _harmony = null;

    Replay = null;
    Recording = null;
    Ipc = null;
  }
}
