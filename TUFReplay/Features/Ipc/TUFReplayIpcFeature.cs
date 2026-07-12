using AdofaiIpc;

namespace TUFReplay.Features.Ipc;

public sealed class TUFReplayIpcFeature
{
  private const string Namespace = "tuf-replay";

  private bool _active;

  public void Enable()
  {
    if (_active) return;
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
          "http://localhost",
          "http://127.0.0.1"
        }
      }
    );

    ipc.Register("health.get", TUFReplayIpcHandlers.Health);
    ipc.Register("activity.days.list", TUFReplayIpcHandlers.ListActivityDays);
    ipc.Register("activity.day.get", TUFReplayIpcHandlers.GetActivityDay);
    ipc.Register("activity.app-session.get", TUFReplayIpcHandlers.GetActivityAppSession);
    ipc.Register("activity.level-session.get", TUFReplayIpcHandlers.GetActivityLevelSession);
    ipc.Register("activity.level-session.segments.list", TUFReplayIpcHandlers.ListActivitySegments);
    ipc.Register("activity.level-session.runs.list", TUFReplayIpcHandlers.ListActivityRuns);
    ipc.Register("activity.level-session.segment-runs.list", TUFReplayIpcHandlers.ListActivitySegmentRuns);

    Main.Instance.Log("[IPC] Registered namespace: " + Namespace);
  }

  public void Disable()
  {
    if (!_active) return;
    _active = false;

    AdofaiIpc.AdofaiIpc.UnregisterNamespace(Namespace);
    Main.Instance.Log("[IPC] Unregistered namespace: " + Namespace);
  }
}
