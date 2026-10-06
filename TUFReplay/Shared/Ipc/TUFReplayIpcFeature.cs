using System;
using System.Threading;
using System.Threading.Tasks;
using AdofaiIpc;
using AdofaiIpc.Core;
using TUFReplay.Activity.Ipc;
using TUFReplay.Activity.Tracking;
using TUFReplay.Calibration.Ipc;
using TUFReplay.Calibration.Models;
using TUFReplay.Composition;
using TUFReplay.Microphone.Devices;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Replay.Ipc;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Ipc;

namespace TUFReplay.Shared.Ipc;

public sealed class TUFReplayIpcFeature
{
  private const string Namespace = "tuf-replay";

  private bool _active;
  private AdofaiIpcNamespace _namespace;
  private long _activityRevision;
  private MicrophoneDevicesResponseDto _microphoneState;

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
          "https://tufreplay-dev.impl1113.dev",
          "https://tufreplay-auto.impl1113.dev",
          "http://localhost",
          "http://127.0.0.1",
        },
      }
    );

    RegisterOutcome(ipc, "health.read", "health.snapshot", HealthIpcHandlers.Get, mainThread: false, broadcast: false);
    RegisterOutcome(
      ipc,
      "activity.sessions.read",
      "activity.sessions.snapshot",
      ActivityIpcHandlers.ListAppSessions,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "activity.legacy-status.read",
      "activity.legacy-status.snapshot",
      ActivityIpcHandlers.GetLegacyReplayStatus,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "activity.level-session.read",
      "activity.level-session.snapshot",
      ActivityIpcHandlers.GetLevelSession,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "activity.level-session.runs.read",
      "activity.level-session.runs.snapshot",
      ActivityIpcHandlers.ListRuns,
      mainThread: false,
      broadcast: false
    );
    RegisterChartOutcome(
      ipc,
      "activity.level-session.chart.read",
      "activity.level-session.chart.snapshot",
      ActivityIpcHandlers.GetChart
    );
    RegisterOutcome(
      ipc,
      "activity.level.read",
      "activity.level.snapshot",
      ActivityIpcHandlers.GetLogicalLevel,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "activity.runs.read",
      "activity.runs.snapshot",
      ActivityIpcHandlers.ListLogicalLevelRuns,
      mainThread: false,
      broadcast: false
    );
    RegisterChartOutcome(
      ipc,
      "activity.chart.read",
      "activity.chart.snapshot",
      ActivityIpcHandlers.GetLogicalLevelChart
    );
    RegisterOutcome(
      ipc,
      "activity.run.remove",
      "activity.run.removed",
      ActivityIpcHandlers.DeleteRun,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "microphone.recording.remove",
      "microphone.recording.removed",
      MicrophoneRecordingIpcHandlers.Delete,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "microphone.recording.retain",
      "microphone.recording.retained",
      MicrophoneRecordingIpcHandlers.KeepPermanently,
      mainThread: false,
      broadcast: false
    );
    ipc.RegisterDownloadCommand("microphone.recording.download", MicrophoneRecordingIpcHandlers.Export);
    RegisterOutcome(
      ipc,
      "replay.start",
      "replay.state.changed",
      ReplayIpcHandlers.Play,
      mainThread: false,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "replay.state.read",
      "replay.state.changed",
      ReplayIpcHandlers.GetStatus,
      mainThread: false,
      broadcast: false
    );
    ipc.RegisterAsyncCommand("replay.level-file.choose", ChooseLevelFile);
    RegisterOutcome(
      ipc,
      "microphone.devices.refresh",
      "microphone.devices.changed",
      MicrophoneIpcHandlers.GetDevices,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "microphone.access.change",
      "microphone.devices.changed",
      MicrophoneIpcHandlers.SetEnabled,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "microphone.device.choose",
      "microphone.devices.changed",
      MicrophoneIpcHandlers.SelectDevice,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "microphone.offset.change",
      "microphone.timing.changed",
      MicrophoneIpcHandlers.SetOffset,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "microphone.volume.change",
      "microphone.timing.changed",
      MicrophoneIpcHandlers.SetVolume,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "calibration.start",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.Start,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.state.read",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.GetStatus,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.result.read",
      "calibration.result.ready",
      MicrophoneCalibrationIpcHandlers.GetResult,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.preview.start",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.PlayPreview,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.preview.stop",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.StopPreview,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.offset.change",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.SetOffset,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.volume.change",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.SetVolume,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "calibration.close",
      "calibration.state.changed",
      MicrophoneCalibrationIpcHandlers.Close,
      mainThread: true,
      broadcast: false
    );
    RegisterOutcome(
      ipc,
      "webcam.state.refresh",
      "webcam.state.changed",
      WebcamIpcHandlers.GetState,
      mainThread: true,
      broadcast: true
    );
    RegisterOutcome(
      ipc,
      "webcam.settings.change",
      "webcam.state.changed",
      WebcamIpcHandlers.UpdateSettings,
      mainThread: true,
      broadcast: true
    );
    FeatureRegistry.WebcamRecording.StateChanged += OnWebcamChanged;
    _namespace = ipc;
    ActivityChanges.Changed += OnActivityChanged;
    ReplayPlaybackCoordinator.StatusChanged += OnReplayChanged;
    FeatureRegistry.MicrophoneCalibration.StatusChanged += OnCalibrationChanged;
    FeatureRegistry.Recording.GameplayStateChanged += PublishMicrophoneAccess;
    ipc.PeerSubscribed += OnPeerSubscribed;
    ipc.MarkReady();

    Main.Instance.Log("[IPC] Namespace ready: " + Namespace);
  }

  public void Disable()
  {
    if (!_active)
      return;
    _active = false;
    if (FeatureRegistry.WebcamRecording != null)
      FeatureRegistry.WebcamRecording.StateChanged -= OnWebcamChanged;
    ActivityChanges.Changed -= OnActivityChanged;
    ReplayPlaybackCoordinator.StatusChanged -= OnReplayChanged;
    if (FeatureRegistry.Recording != null)
      FeatureRegistry.Recording.GameplayStateChanged -= PublishMicrophoneAccess;
    if (FeatureRegistry.MicrophoneCalibration != null)
      FeatureRegistry.MicrophoneCalibration.StatusChanged -= OnCalibrationChanged;
    if (_namespace != null)
      _namespace.PeerSubscribed -= OnPeerSubscribed;
    _namespace = null;

    AdofaiIpc.AdofaiIpc.UnregisterNamespace(Namespace);
    Main.Instance.Log("[IPC] Unregistered namespace: " + Namespace);
  }

  private static void RegisterChartOutcome(
    AdofaiIpcNamespace ipc,
    string commandName,
    string eventName,
    Func<IpcCommand, object> handler
  )
  {
    ipc.RegisterCommand(
      commandName,
      context =>
      {
        object result = handler(context);
        if (result is IpcDomainFailure failure)
        {
          context.Reject(failure.error.code, failure.error.message);
          return;
        }
        var chart = (ActivityChartDto)result;
        ipc.ReplyDownload(
          context,
          eventName,
          ActivityChartDownloads.Create(chart),
          new { chart.LevelSessionId, chart.FloorCount }
        );
      }
    );
  }

  private void RegisterOutcome(
    AdofaiIpcNamespace ipc,
    string commandName,
    string eventName,
    Func<IpcCommand, object> handler,
    bool mainThread = false,
    bool broadcast = false
  )
  {
    Action<IpcCommand> command = context =>
    {
      object result = handler(context);
      if (result is IpcDomainFailure failure)
      {
        context.Reject(failure.error.code, failure.error.message);
        return;
      }
      if (result is MicrophoneDevicesResponseDto devices)
        _microphoneState = devices;
      context.Reply(eventName, result);
      if (broadcast)
        ipc.Publish(eventName, result);
    };
    if (mainThread)
      ipc.RegisterMainThreadCommand(commandName, command);
    else
      ipc.RegisterCommand(commandName, command);
  }

  private async Task ChooseLevelFile(IpcCommand command)
  {
    if (!IpcParams.TryRequiredString(command, "runId", out string runId))
    {
      command.Reject("invalid_run_id", "Select a recorded run first.");
      return;
    }
    var completion = new TaskCompletionSource<ReplayLevelFilePickerResult>(
      TaskCreationOptions.RunContinuationsAsynchronously
    );
    string operationId = null;
    ReplayLevelFilePickerResult early = null;
    Action<ReplayLevelFilePickerResult> finished = result =>
    {
      if (operationId == null)
        early = result;
      else if (result.OperationId == operationId)
        completion.TrySetResult(result);
    };
    ReplayLevelFilePickerCoordinator.Completed += finished;
    try
    {
      ReplayLevelFilePickerResult initial = ReplayLevelFilePickerCoordinator.Start(runId);
      operationId = initial.OperationId;
      if (initial.Outcome != ReplayLevelFilePickerOutcomes.Picking)
        completion.TrySetResult(initial);
      else if (early?.OperationId == operationId)
        completion.TrySetResult(early);
      using (command.CancellationToken.Register(() => completion.TrySetCanceled()))
      {
        ReplayLevelFilePickerResult result = await completion.Task.ConfigureAwait(false);
        command.Reply("replay.level-file.finished", ReplayLevelFilePickerResultDto.From(result));
      }
    }
    finally
    {
      ReplayLevelFilePickerCoordinator.Completed -= finished;
      if (command.CancellationToken.IsCancellationRequested && operationId != null)
        ReplayLevelFilePickerCoordinator.Cancel(operationId);
    }
  }

  private void OnPeerSubscribed(IpcPeerInfo peer)
  {
    UnityMainThread.Post(() =>
    {
      if (!_active || _namespace == null)
        return;
      _namespace.SendToPeer(peer.PeerId, "webcam.state.changed", FeatureRegistry.WebcamRecording.GetState());
      _namespace.SendToPeer(peer.PeerId, "health.snapshot", HealthResponseDto.Create());
      _namespace.SendToPeer(
        peer.PeerId,
        "replay.state.changed",
        ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus())
      );
      _namespace.SendToPeer(
        peer.PeerId,
        "calibration.state.changed",
        MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.GetStatus())
      );
    });
  }

  private void OnWebcamChanged() =>
    UnityMainThread.Post(() =>
    {
      if (_active)
        _namespace?.Publish("webcam.state.changed", FeatureRegistry.WebcamRecording.GetState());
    });

  private void OnActivityChanged(string runId)
  {
    if (_active)
      _namespace?.Publish("activity.changed", new { revision = Interlocked.Increment(ref _activityRevision), runId });
  }

  private void OnReplayChanged(ReplayPlaybackStatus status)
  {
    if (_active)
      _namespace?.Publish("replay.state.changed", ReplayPlaybackStatusDto.From(status));
  }

  private void OnCalibrationChanged(MicrophoneCalibrationStatus status)
  {
    if (_active)
      _namespace?.Publish("calibration.state.changed", MicrophoneCalibrationStatusDto.From(status));
    UnityMainThread.Post(PublishMicrophoneAccess);
  }

  private void PublishMicrophoneAccess()
  {
    if (!_active || _namespace == null || _microphoneState == null)
      return;
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    bool locked = MicrophoneDeviceService.IsToggleLocked();
    bool enabled = settings?.MicrophoneEnabled != false;
    int offset = settings?.MicrophoneOffsetMs ?? 0;
    int volume = settings?.MicrophoneVolumeDb ?? 0;
    if (
      _microphoneState.ToggleLocked == locked
      && _microphoneState.Enabled == enabled
      && _microphoneState.MicrophoneOffsetMs == offset
      && _microphoneState.MicrophoneVolumeDb == volume
    )
      return;
    _microphoneState = new MicrophoneDevicesResponseDto
    {
      Enabled = enabled,
      ToggleLocked = locked,
      Devices = _microphoneState.Devices,
      SelectedDeviceId = settings?.MicrophoneDeviceId,
      MicrophoneOffsetMs = offset,
      MicrophoneVolumeDb = volume,
    };
    _namespace.Publish("microphone.devices.changed", _microphoneState);
  }
}
