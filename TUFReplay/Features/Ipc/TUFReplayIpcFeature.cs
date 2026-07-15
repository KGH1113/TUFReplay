using AdofaiIpc;

namespace TUFReplay.Features.Ipc;

public sealed class TUFReplayIpcFeature
{
  private const string Namespace = "tuf-replay";

  private bool _active;

  public void Enable()
  {
    if (_active)
      return;
    _active = true;

    AdofaiIpcNamespace ipc = AdofaiIpc.AdofaiIpc.RegisterNamespace(
      Namespace,
      new IpcNamespaceInfo
      {
        DisplayName = "TUFReplay",
        Version = Main.Instance.Version.ToString(),
        AllowedOrigins = new[]
        {
          "https://tuforums.com",
          "https://tufreplay.impl1113.dev",
          "http://localhost",
          "http://127.0.0.1",
        },
      }
    );

    ipc.Register("health.get", TUFReplayIpcHandlers.Health);
    ipc.Register("activity.app-sessions.list", TUFReplayIpcHandlers.ListAppSessions);
    ipc.Register("activity.level-session.get", TUFReplayIpcHandlers.GetLevelSession);
    ipc.Register("activity.level-session.runs.list", TUFReplayIpcHandlers.ListRuns);
    ipc.Register("activity.level-session.chart.get", TUFReplayIpcHandlers.GetChart);
    ipc.Register("replay.play", TUFReplayIpcHandlers.PlayReplay);
    ipc.Register("replay.status.get", TUFReplayIpcHandlers.GetReplayStatus);
    ipc.Register("replay.level-file.pick.start", TUFReplayIpcHandlers.StartReplayLevelFilePicker);
    ipc.Register("replay.level-file.pick.status", TUFReplayIpcHandlers.GetReplayLevelFilePickerStatus);

    Main.Instance.Log("[IPC] Registered namespace: " + Namespace);
  }

  public void Disable()
  {
    if (!_active)
      return;
    _active = false;

    AdofaiIpc.AdofaiIpc.UnregisterNamespace(Namespace);
    Main.Instance.Log("[IPC] Unregistered namespace: " + Namespace);
  }
}
