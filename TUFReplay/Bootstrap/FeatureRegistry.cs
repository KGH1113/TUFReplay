using JALib.Core.Patch;
using TUFReplay.Features.Gameplay;
using TUFReplay.Features.Ipc;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;

namespace TUFReplay.Bootstrap;

public static class FeatureRegistry
{
  private static JAPatcher _patcher;

  public static TUFReplayIpcFeature Ipc { get; private set; }
  public static RecordingFeature Recording { get; private set; }
  public static ReplayFeature Replay { get; private set; }

  public static void Initialize()
  {
    if (_patcher != null)
      return;

    Ipc = new TUFReplayIpcFeature();
    Recording = new RecordingFeature();
    Replay = new ReplayFeature();

    _patcher = new JAPatcher(Main.Instance);
    _patcher.AddPatch(typeof(RecordingPatches)).AddPatch(typeof(ReplayInputPatches)).AddPatch(typeof(GameplayPatches));

    _patcher.Patch();
    Ipc.Enable();
    Recording.Enable();
    Replay.Enable();
  }

  public static void Shutdown()
  {
    Replay?.Disable();
    Recording?.Disable();
    Ipc?.Disable();

    _patcher?.Dispose();
    _patcher = null;

    Replay = null;
    Recording = null;
    Ipc = null;
  }
}
