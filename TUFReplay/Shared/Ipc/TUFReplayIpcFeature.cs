using AdofaiIpc;
using TUFReplay.Activity.Ipc;
using TUFReplay.Calibration.Ipc;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Replay.Ipc;

namespace TUFReplay.Shared.Ipc;

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
          "https://guhyeons-macbook-pro.tail234c02.ts.net/",
        },
      }
    );

    ipc.Register("health.get", HealthIpcHandlers.Get);
    ipc.Register("activity.app-sessions.list", ActivityIpcHandlers.ListAppSessions);
    ipc.Register("activity.level-session.get", ActivityIpcHandlers.GetLevelSession);
    ipc.Register("activity.level-session.runs.list", ActivityIpcHandlers.ListRuns);
    ipc.Register("activity.level-session.chart.get", ActivityIpcHandlers.GetChart);
    ipc.Register("activity.logical-level.get", ActivityIpcHandlers.GetLogicalLevel);
    ipc.Register("activity.logical-level.runs.list", ActivityIpcHandlers.ListLogicalLevelRuns);
    ipc.Register("activity.logical-level.chart.get", ActivityIpcHandlers.GetLogicalLevelChart);
    ipc.Register("activity.run.delete", ActivityIpcHandlers.DeleteRun);
    ipc.Register("microphone.recording.delete", MicrophoneRecordingIpcHandlers.Delete);
    ipc.Register("microphone.recording.keep", MicrophoneRecordingIpcHandlers.KeepPermanently);
    ipc.Register("replay.play", ReplayIpcHandlers.Play);
    ipc.Register("replay.status.get", ReplayIpcHandlers.GetStatus);
    ipc.Register("replay.level-file.pick", ReplayIpcHandlers.PickLevelFile);
    ipc.Register("replay.level-file.status.get", ReplayIpcHandlers.GetLevelFilePickerStatus);
    ipc.RegisterMainThread("microphone.devices.get", MicrophoneIpcHandlers.GetDevices);
    ipc.RegisterMainThread("microphone.enabled.set", MicrophoneIpcHandlers.SetEnabled);
    ipc.RegisterMainThread("microphone.device.select", MicrophoneIpcHandlers.SelectDevice);
    ipc.RegisterMainThread("microphone.offset.set", MicrophoneIpcHandlers.SetOffset);
    ipc.RegisterMainThread("microphone.volume.set", MicrophoneIpcHandlers.SetVolume);
    ipc.RegisterMainThread("microphone.calibration.start", MicrophoneCalibrationIpcHandlers.Start);
    ipc.RegisterMainThread("microphone.calibration.status.get", MicrophoneCalibrationIpcHandlers.GetStatus);
    ipc.RegisterMainThread("microphone.calibration.result.get", MicrophoneCalibrationIpcHandlers.GetResult);
    ipc.RegisterMainThread("microphone.calibration.preview.play", MicrophoneCalibrationIpcHandlers.PlayPreview);
    ipc.RegisterMainThread("microphone.calibration.preview.stop", MicrophoneCalibrationIpcHandlers.StopPreview);
    ipc.RegisterMainThread("microphone.calibration.offset.set", MicrophoneCalibrationIpcHandlers.SetOffset);
    ipc.RegisterMainThread("microphone.calibration.volume.set", MicrophoneCalibrationIpcHandlers.SetVolume);
    ipc.RegisterMainThread("microphone.calibration.close", MicrophoneCalibrationIpcHandlers.Close);
    ipc.MarkReady();

    Main.Instance.Log("[IPC] Namespace ready: " + Namespace);
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
