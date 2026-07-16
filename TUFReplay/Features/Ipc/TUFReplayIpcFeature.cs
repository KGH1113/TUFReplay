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

    ipc.Register("health.get", HealthIpcHandlers.Get);
    ipc.Register("activity.app-sessions.list", ActivityIpcHandlers.ListAppSessions);
    ipc.Register("activity.level-session.get", ActivityIpcHandlers.GetLevelSession);
    ipc.Register("activity.level-session.runs.list", ActivityIpcHandlers.ListRuns);
    ipc.Register("activity.level-session.chart.get", ActivityIpcHandlers.GetChart);
    ipc.Register("replay.play", ReplayIpcHandlers.Play);
    ipc.Register("replay.status.get", ReplayIpcHandlers.GetStatus);
    ipc.Register("replay.level-file.pick.start", ReplayIpcHandlers.StartLevelFilePicker);
    ipc.Register("replay.level-file.pick.status", ReplayIpcHandlers.GetLevelFilePickerStatus);
    ipc.RegisterMainThread("microphone.devices.get", MicrophoneIpcHandlers.GetDevices);
    ipc.RegisterMainThread("microphone.device.select", MicrophoneIpcHandlers.SelectDevice);

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
