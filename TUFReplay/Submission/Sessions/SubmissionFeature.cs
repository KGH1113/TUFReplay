using System;
using TUFReplay.Recording.Sessions;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Debug;

namespace TUFReplay.Submission.Sessions;

/// <summary>Main-thread orchestration only. Transport and installation I/O have separate owners.</summary>
public sealed class SubmissionFeature : IDisposable
{
  public TUFReplay.Submission.Auth.OAuthCoordinator Authentication { get; } =
    new TUFReplay.Submission.Auth.OAuthCoordinator(TUFReplay.Shared.Settings.TUFReplaySettingStore.Current);
  public SubmissionRecordsClient Records { get; } = new SubmissionRecordsClient();
  public SubmissionAccount Account => Authentication.Session?.Account;
  private readonly RunIssuanceClient _api = new RunIssuanceClient();
  private SubmissionAccount _account;
  private SubmissionPreflight _preflight;
  private SubmissionAttempt _attempt;
  private RecordingSession _session;
  private bool _clearPending;
  private string _path;
  private int? _levelId;
  private DateTimeOffset _retryAt;
  private bool _disposed;
  private TUFReplay.Submission.Transport.LevelChangeListener _changes;
  private bool _pendingToast;
  private bool _preflightReadyReported;
  private bool _preflightFailureReported;

  public object Status() =>
    new
    {
      connected = _account != null,
      configured = Authentication.Configured,
      disabled = TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true,
      state = _account == null
        ? Authentication.State
        : _attempt?.State ?? (_preflight?.IsReady == true ? "ready" : _preflight?.Error ?? "preparing"),
      runId = _attempt?.RunId.ToString(),
      reason = _attempt?.Capture.Failure,
    };

  public void SetDisabled(bool disabled)
  {
    var settings = TUFReplay.Shared.Settings.TUFReplaySettingStore.Current;
    settings.AutoSubmissionDisabled = disabled;
    TUFReplay.Shared.Settings.TUFReplaySettingStore.Save();
    if (disabled)
    {
      _session?.AbortEvidence("collection_disabled");
      _session = null;
      _clearPending = false;
      _preflight?.Dispose();
      _preflight = null;
      if (_attempt?.Capture.CompletionMeta == null)
        _attempt?.Dispose();
    }
    else
      Prepare();
  }

  public void Connect(SubmissionAccount account)
  {
    Disconnect();
    _account = account;
    SubmissionDebugTelemetry.Publish("TUF account connected");
    WatchLevel();
    Prepare();
  }

  public void Disconnect()
  {
    _session?.AbortEvidence("account_disconnected");
    _attempt?.Dispose();
    _attempt = null;
    _preflight?.Dispose();
    _preflight = null;
    _account = null;
    _session = null;
    _clearPending = false;
    _changes?.Dispose();
    _changes = null;
    _pendingToast = false;
  }

  public void SetLevel(string path, int? levelId)
  {
    _session?.AbortEvidence("level_changed");
    _session = null;
    _clearPending = false;
    if (_attempt?.Capture.CompletionMeta == null)
    {
      _attempt?.Dispose();
      _attempt = null;
    }
    _preflight?.Dispose();
    _preflight = null;
    _path = path;
    _levelId = levelId;
    SubmissionDebugTelemetry.Publish(
      levelId.HasValue ? "Level opened: TUF #" + levelId.Value : "Level opened: no TUF level identity"
    );
    WatchLevel();
    Prepare();
  }

  public void Begin(RecordingSession session)
  {
    if (_disposed || session.IsCapturingInput)
      return;
    int startTile = RecordingSession.GetCurrentTile();
    if (startTile != 0)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: run started at tile " + startTile);
      return;
    }
    SubmissionDebugTelemetry.Publish("Start tile 0 detected");
    if (_account == null)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: TUF login required");
      return;
    }
    if (TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: auto submission disabled");
      return;
    }
    if (_attempt != null && !_attempt.Finished)
      return;
    if (_session == session && _attempt?.Finished == false)
      return;
    // A prepared server lease is mandatory before capture. Never upload a completed local replay.
    if (_preflight?.IsReady != true)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: server run is not ready");
      return;
    }
    var issued = _preflight.Take();
    _preflight.Dispose();
    _preflight = null;
    if (issued == null)
      return;
    _attempt?.Dispose();
    _attempt = new SubmissionAttempt(issued);
    if (!session.AttachEvidence(_attempt.Capture))
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: evidence could not attach before input");
      _attempt.Dispose();
      return;
    }
    session.LinkSubmissionRun(issued.Id);
    _session = session;
    SubmissionDebugTelemetry.Publish("Evidence capture attached to run " + issued.Id.ToString("N").Substring(0, 8));
  }

  public void Clear(RecordingSession session)
  {
    if (_session == session && _attempt != null)
    {
      _clearPending = true;
      SubmissionDebugTelemetry.Publish("Won state detected; finishing evidence");
    }
  }

  public void Abort(RecordingSession session, string reason)
  {
    if (_session != session)
      return;
    SubmissionDebugTelemetry.Publish("Run failed: " + reason);
    session.AbortEvidence(reason);
    _session = null;
    _clearPending = false;
  }

  public void Tick()
  {
    if (_disposed)
      return;
    Authentication.Tick();
    if (!ReferenceEquals(_account, Account))
    {
      if (Account == null)
        Disconnect();
      else
        Connect(Account);
    }
    if (_changes?.TakeChange() == true)
    {
      _pendingToast = true;
      // Refresh future attempts. Submission validates against the current official chart.
      _preflight?.Dispose();
      _preflight = null;
      _retryAt = DateTimeOffset.MinValue;
    }
    if (
      _pendingToast
      && TUFReplay.Shared.Notifications.AssetBundleToast.Show(
        "TUF chart updated",
        "This chart's TUF information changed. Submission will validate against the current official chart."
      )
    )
      _pendingToast = false;
    FinalizeClear(_session);
    if (!_preflightReadyReported && _preflight?.IsReady == true)
    {
      _preflightReadyReported = true;
      SubmissionDebugTelemetry.Publish("Server run issued; ready for a tile 0 start");
    }
    if (!_preflightFailureReported && _preflight?.Finished == true && _preflight.IsReady != true)
    {
      _preflightFailureReported = true;
      SubmissionDebugTelemetry.Publish("Server run preparation failed: " + (_preflight.Error ?? "lease expired"));
    }
    if (
      _account != null
      && (_attempt == null || _attempt.Finished)
      && DateTimeOffset.UtcNow >= _retryAt
      && (_preflight == null || (_preflight.Finished && !_preflight.IsReady))
    )
      Prepare();
  }

  // Also called before recording teardown if editor return precedes the next Update.
  public void FinalizeClear(RecordingSession session)
  {
    if (!_clearPending || session == null || _session != session || _attempt == null)
      return;
    _clearPending = false;
    var metadata = session.FinishEvidence(_attempt.Capture);
    if (metadata != null)
    {
      SubmissionDebugTelemetry.Publish("Level evidence finalized; draining upload buffer");
      _attempt.Capture.Complete(metadata);
    }
    _session = null;
  }

  private void Prepare()
  {
    _retryAt = DateTimeOffset.UtcNow.AddSeconds(30);
    _preflightReadyReported = false;
    _preflightFailureReported = false;
    if (
      _disposed
      || _account == null
      || !_levelId.HasValue
      || string.IsNullOrEmpty(_path)
      || TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true
    )
      return;
    SubmissionDebugTelemetry.Publish("Preparing server run for TUF #" + _levelId.Value);
    _preflight?.Dispose();
    _preflight = new SubmissionPreflight(
      _api,
      _account,
      _path,
      _levelId.Value,
      UnityEngine.Application.version,
      Main.Instance.Version.ToString()
    );
  }

  private void WatchLevel()
  {
    _changes?.Dispose();
    _changes = null;
    if (_account != null && _levelId.HasValue)
      _changes = new TUFReplay.Submission.Transport.LevelChangeListener(_account, _levelId.Value);
  }

  public void Dispose()
  {
    _disposed = true;
    Disconnect();
    _api.Dispose();
    Records.Dispose();
    Authentication.Dispose();
  }
}
