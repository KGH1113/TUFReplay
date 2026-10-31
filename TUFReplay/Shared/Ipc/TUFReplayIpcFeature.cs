using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdofaiIpc;
using TUFReplay.Activity.Ipc;
using TUFReplay.Activity.Tracking;
using TUFReplay.Calibration.Ipc;
using TUFReplay.Calibration.Models;
using TUFReplay.Composition;
using TUFReplay.Microphone.Devices;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Replay.Export;
using TUFReplay.Replay.Ipc;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Downloads;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Shared.Media;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Threading;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Ipc;
using static TUFReplay.Shared.Ipc.IpcDtoMapper;

namespace TUFReplay.Shared.Ipc;

public sealed class TUFReplayIpcFeature
{
  private bool _active;
  private ModRegistration<ReplayApiApp> _namespace;
  private long _activityRevision;
  private readonly object _activityChangeGate = new object();
  private bool _activityChangePending;
  private string _pendingActivityRunId;
  private MicrophoneDevicesResponseDto _microphoneState;

  public void Enable()
  {
    if (_active)
      return;
    _namespace = IpcRuntime.Current.RegisterMod(
      ReplayApiContract.Instance,
      "TUFReplay",
      new[]
      {
        "https://tuforums.com",
        "https://tufreplay.impl1113.dev",
        "https://tufreplay-dev.impl1113.dev",
        "https://tufreplay-auto.impl1113.dev",
        "https://guhyeons-macbook-pro.tail234c02.ts.net",
        "http://guhyeons-macbook-pro.tail234c02.ts.net",
        "http://localhost",
        "http://127.0.0.1",
        // The IPC loader matches origins exactly, port included: Vite dev (5173/5174) and preview (4173).
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174",
        "http://localhost:4173",
        "http://127.0.0.1:4173",
      }
    );
    try
    {
      RegisterHandlers();
      _namespace.AppConnected += OnAppConnected;
      _namespace.AppDisconnected += OnAppDisconnected;
      _namespace.AppEventFailed += LogNotificationFailure;
      _active = true;
      FeatureRegistry.WebcamRecording.StateChanged += OnWebcamChanged;
      RenderJobCoordinator.Changed += OnRenderStateChanged;
      FfmpegInstallCoordinator.Changed += OnDownloadsChanged;
      DownloadCenterCoordinator.Changed += OnDownloadsChanged;
      ActivityChanges.Changed += OnActivityChanged;
      ReplayPlaybackCoordinator.StatusChanged += OnReplayChanged;
      FeatureRegistry.MicrophoneCalibration.StatusChanged += OnCalibrationChanged;
      FeatureRegistry.Recording.GameplayStateChanged += PublishMicrophoneAccess;
      _namespace.MarkReady();
      Main.Instance.Log("[IPC] Typed API ready: tuf-replay");
    }
    catch
    {
      if (_active)
        Disable();
      else
      {
        _namespace.Dispose();
        _namespace = null;
      }
      throw;
    }
  }

  private void RegisterHandlers()
  {
    Register(ReplayApiMethods.ReadHealth, (_, _) => Map(HealthResponseDto.Create()));
    Register(
      ReplayApiMethods.ListAppSessions,
      (r, _) => ActivityIpcHandlers.ListAppSessions(Optional(r.Offset), Optional(r.Limit)).Select(Map).ToArray()
    );
    Register(ReplayApiMethods.ReadLegacyReplayStatus, (_, _) => Map(ActivityIpcHandlers.GetLegacyReplayStatus()));
    Register(
      ReplayApiMethods.ReadLevelSession,
      (r, _) => Map(ActivityIpcHandlers.GetLevelSession(Required(r.Id, "invalid_level_session_id")))
    );
    Register(
      ReplayApiMethods.ListLevelSessionRuns,
      (r, _) =>
        ActivityIpcHandlers
          .ListRuns(Required(r.Id, "invalid_level_session_id"), Optional(r.Offset), Optional(r.Limit))
          .Select(Map)
          .ToArray()
    );
    Register(
      ReplayApiMethods.ReadLevelSessionChart,
      (r, _) => Map(ActivityIpcHandlers.GetChart(Required(r.Id, "invalid_level_session_id")))
    );
    Register(
      ReplayApiMethods.ReadLogicalLevel,
      (r, _) => Map(ActivityIpcHandlers.GetLogicalLevel(Required(r.Id, "invalid_logical_level_id")))
    );
    Register(
      ReplayApiMethods.ListLogicalLevelRuns,
      (r, _) =>
        ActivityIpcHandlers
          .ListLogicalLevelRuns(
            Required(r.Id, "invalid_logical_level_id"),
            r.AppSessionIds.Select(id => Required(id, "invalid_app_session_ids"))
              .Distinct(StringComparer.Ordinal)
              .ToArray(),
            Optional(r.Offset),
            Optional(r.Limit)
          )
          .Select(Map)
          .ToArray()
    );
    Register(
      ReplayApiMethods.ReadLogicalLevelChart,
      (r, _) => Map(ActivityIpcHandlers.GetLogicalLevelChart(Required(r.Id, "invalid_logical_level_id")))
    );
    // Payload decoding allocates, so it waits until ADOFAI stops judging input.
    RegisterWhenGameplayIdle(
      ReplayApiMethods.ReadRunJudgmentTimelines,
      (r, _) =>
        RunJudgmentTimelineIpcHandlers
          .Read(
            (r.RunIds ?? Array.Empty<string>()).Select(id => Required(id)).Distinct(StringComparer.Ordinal).ToArray()
          )
          .Select(Map)
          .ToArray()
    );
    Register(ReplayApiMethods.RemoveRun, (r, _) => Map(ActivityIpcHandlers.DeleteRun(Required(r.RunId))));
    RegisterWhenGameplayIdle(
      ReplayApiMethods.RemoveAppSessionRuns,
      (r, _) =>
        Map(
          ActivityIpcHandlers.DeleteAppSessionRuns(
            (r.AppSessionIds ?? Array.Empty<string>())
              .Select(id => Required(id, "invalid_app_session_ids"))
              .Distinct(StringComparer.Ordinal)
              .ToArray(),
            r.KeepClears
          )
        )
    );
    Register(
      ReplayApiMethods.ChangeRunLock,
      (r, _) => Map(ActivityIpcHandlers.ChangeRunLock(Required(r.RunId), r.Locked))
    );
    Register(
      ReplayApiMethods.RemoveMicrophoneRecording,
      (r, _) => Map(MicrophoneRecordingIpcHandlers.Delete(Required(r.RunId)))
    );
    Register(
      ReplayApiMethods.RetainMicrophoneRecording,
      (r, _) => Map(MicrophoneRecordingIpcHandlers.KeepPermanently(Required(r.RunId)))
    );
    Register(
      ReplayApiMethods.ExportMicrophoneRecording,
      (r, c) => MicrophoneRecordingIpcHandlers.Export(Required(r.RunId), c.App.CancellationToken)
    );
    Register(
      ReplayApiMethods.RemoveCameraRecording,
      (r, _) => Map(WebcamRecordingIpcHandlers.Delete(Required(r.RunId)))
    );
    Register(
      ReplayApiMethods.RetainCameraRecording,
      (r, _) => Map(WebcamRecordingIpcHandlers.KeepPermanently(Required(r.RunId)))
    );
    Register(
      ReplayApiMethods.ExportCameraRecording,
      (r, c) => WebcamRecordingIpcHandlers.Export(Required(r.RunId), c.App.CancellationToken)
    );
    Register(
      ReplayApiMethods.StartReplay,
      (r, _) =>
        Map(ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.Play(Required(r.RunId), OptionalText(r.LevelPath))))
    );
    Register(
      ReplayApiMethods.ReadReplayState,
      (_, _) => Map(ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus()))
    );
    _namespace.Handle(ReplayApiMethods.ChooseLevelFile, ChooseLevelFile);
    Register(
      ReplayApiMethods.RefreshMicrophoneDevices,
      (_, _) => MicrophoneState(MicrophoneIpcHandlers.GetDevices()),
      true
    );
    Register(
      ReplayApiMethods.ChangeMicrophoneAccess,
      (r, _) => MicrophoneState(MicrophoneIpcHandlers.SetEnabled(r.Enabled)),
      true
    );
    Register(
      ReplayApiMethods.ChooseMicrophoneDevice,
      (r, _) =>
        MicrophoneState(
          MicrophoneIpcHandlers.SelectDevice(string.IsNullOrWhiteSpace(r.DeviceId) ? null : r.DeviceId.Trim())
        ),
      true
    );
    Register(
      ReplayApiMethods.ChangeMicrophoneOffset,
      (r, _) => MicrophoneTiming(MicrophoneIpcHandlers.SetOffset(r.OffsetMs)),
      true
    );
    Register(
      ReplayApiMethods.ChangeMicrophoneVolume,
      (r, _) => MicrophoneTiming(MicrophoneIpcHandlers.SetVolume(r.VolumeDb)),
      true
    );
    Register(ReplayApiMethods.StartCalibration, (_, _) => Map(MicrophoneCalibrationIpcHandlers.Start()), true);
    Register(
      ReplayApiMethods.ReadCalibrationState,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.GetStatus(RequiredOperation(r.OperationId))),
      true
    );
    Register(
      ReplayApiMethods.ReadCalibrationResult,
      (r, _) =>
        Map(MicrophoneCalibrationIpcHandlers.GetResult(RequiredOperation(r.OperationId), Optional(r.Revision) ?? 0)),
      true
    );
    Register(
      ReplayApiMethods.StartCalibrationPreview,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.PlayPreview(RequiredOperation(r.OperationId))),
      true
    );
    Register(
      ReplayApiMethods.StopCalibrationPreview,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.StopPreview(RequiredOperation(r.OperationId))),
      true
    );
    Register(
      ReplayApiMethods.ChangeCalibrationOffset,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.SetOffset(RequiredOperation(r.OperationId), r.OffsetMs)),
      true
    );
    Register(
      ReplayApiMethods.ChangeCalibrationVolume,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.SetVolume(RequiredOperation(r.OperationId), r.VolumeDb)),
      true
    );
    Register(
      ReplayApiMethods.CloseCalibration,
      (r, _) => Map(MicrophoneCalibrationIpcHandlers.Close(RequiredOperation(r.OperationId))),
      true
    );
    Register(
      ReplayApiMethods.RefreshWebcamState,
      (r, _) => WebcamState(WebcamIpcHandlers.GetState(r.RefreshDevices.IsSpecified && r.RefreshDevices.Value)),
      true
    );
    Register(
      ReplayApiMethods.ChangeWebcamSettings,
      (r, _) => WebcamState(WebcamIpcHandlers.UpdateSettings(r.Settings)),
      true
    );
    var preview = new WebcamPreviewIpc();
    Register(ReplayApiMethods.ReadWebcamPreview, (r, _) => preview.Read(r.DeviceId));
    Register(ReplayApiMethods.ReadFfmpegState, (_, _) => Map(FfmpegInstallCoordinator.Status()));
    Register(ReplayApiMethods.RequestFfmpeg, (_, c) => Map(FfmpegInstallCoordinator.Request(c.App.SessionId)), true);
    Register(ReplayApiMethods.ConfirmFfmpeg, (_, _) => Map(FfmpegInstallCoordinator.Confirm()), true);
    Register(ReplayApiMethods.DeclineFfmpeg, (_, _) => Map(FfmpegInstallCoordinator.Decline()), true);
    Register(ReplayApiMethods.CancelFfmpeg, (_, _) => Map(FfmpegInstallCoordinator.Cancel()), true);
    Register(
      ReplayApiMethods.ReleaseFfmpeg,
      (_, c) =>
      {
        FfmpegInstallCoordinator.CancelPendingRequest(c.App.SessionId);
        return Map(FfmpegInstallCoordinator.Status());
      },
      true
    );
    Register(ReplayApiMethods.ReadDownloadsState, (_, _) => DownloadCenterCoordinator.Status(), true);
    Register(
      ReplayApiMethods.RequestFfmpegDownload,
      (_, c) =>
      {
        FfmpegInstallCoordinator.Request(c.App.SessionId);
        return DownloadCenterCoordinator.Status();
      },
      true
    );
    Register(
      ReplayApiMethods.ConfirmFfmpegDownload,
      (_, _) =>
      {
        FfmpegInstallCoordinator.Confirm();
        return DownloadCenterCoordinator.Status();
      },
      true
    );
    Register(
      ReplayApiMethods.CancelFfmpegDownload,
      (_, _) =>
      {
        FfmpegInstallCoordinator.Cancel();
        return DownloadCenterCoordinator.Status();
      },
      true
    );
    Register(ReplayApiMethods.RequestRendererDownload, (_, _) => DownloadCenterCoordinator.RequestRenderer(), true);
    Register(ReplayApiMethods.ConfirmRendererDownload, (_, _) => DownloadCenterCoordinator.ConfirmRenderer(), true);
    Register(ReplayApiMethods.CancelRendererDownload, (_, _) => DownloadCenterCoordinator.CancelRenderer(), true);
    Register(
      ReplayApiMethods.StartRender,
      (r, _) => RenderJobCoordinator.Start(Required(r.RunId), r.Options, OptionalText(r.LevelPath)),
      true
    );
    Register(ReplayApiMethods.ReadRenderState, (_, _) => RenderJobCoordinator.Get());
    Register(
      ReplayApiMethods.CancelRender,
      (r, _) => RenderJobCoordinator.Cancel(Required(r.JobId, "invalid_render_job_id")),
      true
    );
  }

  private void Register<TRequest, TResult>(
    IpcMethod<TRequest, TResult> method,
    Func<TRequest, ModCallContext<ReplayApiApp>, TResult> handler,
    bool mainThread = false
  ) =>
    _namespace.Handle(
      method,
      (request, context) =>
        mainThread
          ? OnGameThread(() => handler(request, context), context.CancellationToken)
          : Task.FromResult(handler(request, context))
    );

  // Bulk disk work waits until ADOFAI stops judging input, then runs on the thread pool.
  private void RegisterWhenGameplayIdle<TRequest, TResult>(
    IpcMethod<TRequest, TResult> method,
    Func<TRequest, ModCallContext<ReplayApiApp>, TResult> handler
  ) =>
    _namespace.Handle(
      method,
      (request, context) => WhenGameplayIdle(() => handler(request, context), context.CancellationToken)
    );

  private static async Task<T> WhenGameplayIdle<T>(Func<T> action, CancellationToken cancellationToken)
  {
    var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (cancellationToken.Register(() => completion.TrySetCanceled()))
    {
      GameplayIdleGate.Run(() =>
      {
        if (cancellationToken.IsCancellationRequested)
        {
          completion.TrySetCanceled();
          return;
        }
        try
        {
          completion.TrySetResult(action());
        }
        catch (Exception error)
        {
          completion.TrySetException(error);
        }
      });
      return await completion.Task.ConfigureAwait(false);
    }
  }

  private static async Task<T> OnGameThread<T>(Func<T> action, CancellationToken cancellationToken)
  {
    var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (cancellationToken.Register(() => completion.TrySetCanceled()))
    {
      UnityMainThread.Post(() =>
      {
        if (cancellationToken.IsCancellationRequested)
        {
          completion.TrySetCanceled();
          return;
        }
        try
        {
          completion.TrySetResult(action());
        }
        catch (Exception error)
        {
          completion.TrySetException(error);
        }
      });
      return await completion.Task.ConfigureAwait(false);
    }
  }

  private static int? Optional(IpcOptional<int> value) => value.IsSpecified ? value.Value : (int?)null;

  private static string OptionalText(IpcOptional<string> value) => value.IsSpecified ? value.Value : null;

  private static string RequiredOperation(string value) => Required(value, "invalid_operation_id");

  private static string Required(string value, string code = "invalid_run_id") =>
    !string.IsNullOrWhiteSpace(value)
      ? value.Trim()
      : throw IpcDomainError.Create(code, "Select a valid record and try again.");

  private MicrophoneDevices MicrophoneState(MicrophoneDevicesResponseDto state)
  {
    _microphoneState = state;
    MicrophoneDevices value = Map(state);
    Publish(app =>
      app.MicrophoneDevicesChangedAsync(new MicrophoneDevicesChangedRequest { Value = value }, app.CancellationToken)
    );
    return value;
  }

  private Generated.MicrophoneTiming MicrophoneTiming(MicrophoneTimingSettingsDto state)
  {
    var value = Map(state);
    Publish(app =>
      app.MicrophoneTimingChangedAsync(new MicrophoneTimingChangedRequest { Value = value }, app.CancellationToken)
    );
    return value;
  }

  private Generated.WebcamState WebcamState(WebcamStateDto state)
  {
    var value = Map(state);
    Publish(app => app.WebcamStateChangedAsync(new WebcamStateChangedRequest { Value = value }, app.CancellationToken));
    return value;
  }

  private async Task<LevelFileResult> ChooseLevelFile(
    ChooseLevelFileRequest request,
    ModCallContext<ReplayApiApp> context
  )
  {
    context.CancellationToken.ThrowIfCancellationRequested();
    string runId = Required(request.RunId);
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
      string purpose = request.Purpose.IsSpecified ? request.Purpose.Value : "replay";
      ReplayLevelFilePickerResult initial = ReplayLevelFilePickerCoordinator.Start(
        runId,
        holdForReplay: purpose == "replay"
      );
      operationId = initial.OperationId;
      if (initial.Outcome != ReplayLevelFilePickerOutcomes.Picking)
        completion.TrySetResult(initial);
      else if (early?.OperationId == operationId)
        completion.TrySetResult(early);
      using (context.CancellationToken.Register(() => completion.TrySetCanceled()))
        return Map(ReplayLevelFilePickerResultDto.From(await completion.Task.ConfigureAwait(false)));
    }
    finally
    {
      ReplayLevelFilePickerCoordinator.Completed -= finished;
      if (context.CancellationToken.IsCancellationRequested && operationId != null)
        ReplayLevelFilePickerCoordinator.Cancel(operationId);
    }
  }

  private void OnAppConnected(ReplayApiApp app) =>
    UnityMainThread.Post(() =>
    {
      if (!_active || app.CancellationToken.IsCancellationRequested)
        return;
      Notify(() =>
        app.DownloadsStateChangedAsync(
          new DownloadsStateChangedRequest { Value = DownloadCenterCoordinator.Status() },
          app.CancellationToken
        )
      );
      Notify(() =>
        app.FfmpegStateChangedAsync(
          new FfmpegStateChangedRequest { Value = Map(FfmpegInstallCoordinator.Status()) },
          app.CancellationToken
        )
      );
      var render = RenderJobCoordinator.Get();
      if (render != null)
        Notify(() =>
          app.RenderStateChangedAsync(new RenderStateChangedRequest { Value = render }, app.CancellationToken)
        );
      Notify(() =>
        app.WebcamStateChangedAsync(
          new WebcamStateChangedRequest { Value = Map(FeatureRegistry.WebcamRecording.GetState()) },
          app.CancellationToken
        )
      );
      Notify(() =>
        app.HealthSnapshotAsync(
          new HealthSnapshotRequest { Value = Map(HealthResponseDto.Create()) },
          app.CancellationToken
        )
      );
      Notify(() =>
        app.ReplayStateChangedAsync(
          new ReplayStateChangedRequest
          {
            Value = Map(ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus())),
          },
          app.CancellationToken
        )
      );
      Notify(() =>
        app.CalibrationStateChangedAsync(
          new CalibrationStateChangedRequest
          {
            Value = Map(MicrophoneCalibrationStatusDto.From(FeatureRegistry.MicrophoneCalibration.GetStatus())),
          },
          app.CancellationToken
        )
      );
    });

  private void OnAppDisconnected(ReplayApiApp app) =>
    UnityMainThread.Post(() => FfmpegInstallCoordinator.CancelPendingRequest(app.SessionId));

  private void Publish(Func<ReplayApiApp, Task> callback)
  {
    var registration = _namespace;
    if (!_active || registration == null)
      return;
    foreach (var app in registration.Apps)
      Notify(() => callback(app));
  }

  private static async void Notify(Func<Task> callback)
  {
    try
    {
      await callback().ConfigureAwait(false);
    }
    catch (OperationCanceledException) { }
    catch (IpcCallException error) when (error.Code == "unavailable") { }
    catch (Exception error)
    {
      LogNotificationFailure(error);
    }
  }

  private static void LogNotificationFailure(Exception error) => Main.Instance?.LogException("IPC/Notification", error);

  private void OnRenderStateChanged(RenderJobState value) =>
    Publish(app => app.RenderStateChangedAsync(new RenderStateChangedRequest { Value = value }, app.CancellationToken));

  private void OnDownloadsChanged() =>
    UnityMainThread.Post(() =>
    {
      if (!_active)
        return;
      var downloads = DownloadCenterCoordinator.Status();
      var ffmpeg = Map(FfmpegInstallCoordinator.Status());
      Publish(app =>
        app.DownloadsStateChangedAsync(new DownloadsStateChangedRequest { Value = downloads }, app.CancellationToken)
      );
      Publish(app =>
        app.FfmpegStateChangedAsync(new FfmpegStateChangedRequest { Value = ffmpeg }, app.CancellationToken)
      );
    });

  private void OnWebcamChanged() =>
    UnityMainThread.Post(() =>
    {
      if (_active)
        WebcamState(FeatureRegistry.WebcamRecording.GetState());
    });

  // A change makes the companion refetch activity lists. During judged gameplay,
  // coalesce changes and publish once the attempt ends so that refetch work and its
  // garbage stay out of the attempt. RunId is null when several runs changed.
  private void OnActivityChanged(string runId)
  {
    if (!_active)
      return;
    lock (_activityChangeGate)
    {
      if (_activityChangePending)
      {
        if (!string.Equals(_pendingActivityRunId, runId, StringComparison.Ordinal))
          _pendingActivityRunId = null;
        return;
      }
      _activityChangePending = true;
      _pendingActivityRunId = runId;
    }
    GameplayIdleGate.Run(PublishPendingActivityChange);
  }

  private void PublishPendingActivityChange()
  {
    string runId;
    lock (_activityChangeGate)
    {
      if (!_activityChangePending)
        return;
      _activityChangePending = false;
      runId = _pendingActivityRunId;
      _pendingActivityRunId = null;
    }
    if (!_active)
      return;
    var value = new ActivityChanged { Revision = Interlocked.Increment(ref _activityRevision), RunId = runId };
    Publish(app => app.ActivityChangedAsync(new ActivityChangedRequest { Value = value }, app.CancellationToken));
  }

  private void OnReplayChanged(ReplayPlaybackStatus status)
  {
    var value = Map(ReplayPlaybackStatusDto.From(status));
    Publish(app => app.ReplayStateChangedAsync(new ReplayStateChangedRequest { Value = value }, app.CancellationToken));
  }

  private void OnCalibrationChanged(MicrophoneCalibrationStatus status)
  {
    var value = Map(MicrophoneCalibrationStatusDto.From(status));
    Publish(app =>
      app.CalibrationStateChangedAsync(new CalibrationStateChangedRequest { Value = value }, app.CancellationToken)
    );
    UnityMainThread.Post(PublishMicrophoneAccess);
  }

  private void PublishMicrophoneAccess()
  {
    if (!_active || _microphoneState == null)
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
    MicrophoneState(
      new MicrophoneDevicesResponseDto
      {
        Enabled = enabled,
        ToggleLocked = locked,
        Devices = _microphoneState.Devices,
        SelectedDeviceId = settings?.MicrophoneDeviceId,
        MicrophoneOffsetMs = offset,
        MicrophoneVolumeDb = volume,
      }
    );
  }

  public void Disable()
  {
    if (!_active)
      return;
    _active = false;
    if (FeatureRegistry.WebcamRecording != null)
      FeatureRegistry.WebcamRecording.StateChanged -= OnWebcamChanged;
    RenderJobCoordinator.Changed -= OnRenderStateChanged;
    FfmpegInstallCoordinator.Changed -= OnDownloadsChanged;
    DownloadCenterCoordinator.Changed -= OnDownloadsChanged;
    ActivityChanges.Changed -= OnActivityChanged;
    ReplayPlaybackCoordinator.StatusChanged -= OnReplayChanged;
    if (FeatureRegistry.Recording != null)
      FeatureRegistry.Recording.GameplayStateChanged -= PublishMicrophoneAccess;
    if (FeatureRegistry.MicrophoneCalibration != null)
      FeatureRegistry.MicrophoneCalibration.StatusChanged -= OnCalibrationChanged;
    RenderBundleExportService.Shutdown();
    foreach (var app in _namespace.Apps)
      FfmpegInstallCoordinator.CancelPendingRequest(app.SessionId);
    _namespace.AppConnected -= OnAppConnected;
    _namespace.AppDisconnected -= OnAppDisconnected;
    _namespace.AppEventFailed -= LogNotificationFailure;
    _namespace.Dispose();
    _namespace = null;
    _microphoneState = null;
    Main.Instance.Log("[IPC] Unregistered typed API: tuf-replay");
  }
}
