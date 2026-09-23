using System;
using System.Threading;
using System.Threading.Tasks;
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
  public bool CanSubmit => _account?.CanSubmit == true;
  private SubmissionAccount _account;
  private TUFReplay.Submission.Transport.LevelSubmissionSession _transport;
  private LevelSubmissionAttempt _attempt;
  private RecordingSession _session;
  private bool _clearPending;
  private string _path;
  private int? _levelId;
  private DateTimeOffset _retryAt;
  private bool _disposed;
  private TUFReplay.Submission.Transport.LevelChangeListener _changes;
  private bool _pendingToast;
  private Task<SubmissionAccountIdentity> _identityRefresh;
  private CancellationTokenSource _identityRefreshCancellation;
  private SubmissionAccount _identityRefreshAccount;
  private DateTimeOffset _identityRefreshAt;
  private bool _submissionAuthorized;

  public object Status() =>
    new
    {
      connected = _account != null,
      configured = Authentication.Configured,
      disabled = TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true,
      state = _account == null ? Authentication.State
      : !_account.CanSubmit ? "eligibility_" + _account.IdentityStatus
      : _attempt?.State ?? _transport?.State ?? "preparing",
      runId = _attempt?.RunId.ToString(),
      reason = _attempt?.Capture.Failure,
      username = _account?.Username,
      nickname = _account?.Nickname,
      accountStatus = _account?.IdentityStatus ?? "unavailable",
      canSubmit = CanSubmit,
      denialReason = _account?.DenialReason,
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
      _transport?.Dispose();
      _transport = null;
    }
    else
      Prepare();
  }

  public void Connect(SubmissionAccount account)
  {
    Disconnect();
    _account = account;
    SubmissionDebugTelemetry.Publish("TUF account connected");
    _identityRefreshAt = DateTimeOffset.MinValue;
    StartIdentityRefresh();
  }

  public void Disconnect()
  {
    _identityRefreshCancellation?.Cancel();
    _identityRefreshCancellation?.Dispose();
    _identityRefreshCancellation = null;
    _identityRefresh = null;
    _identityRefreshAccount = null;
    _identityRefreshAt = DateTimeOffset.MinValue;
    _submissionAuthorized = false;
    _session?.AbortEvidence("account_disconnected");
    _attempt = null;
    _transport?.Dispose();
    _transport = null;
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
    _attempt = null;
    _transport?.Retire();
    _transport = null;
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
    if (!_account.CanSubmit)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: trusted-tester submission access is unavailable");
      return;
    }
    if (TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: auto submission disabled");
      return;
    }
    if (_session == session)
      return;
    // Attach bounded live capture before input; run_start approval never blocks Unity.
    Prepare();
    var attempt = _transport?.Begin();
    if (attempt == null)
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: level session unavailable or attempt queue full");
      return;
    }
    if (!session.AttachEvidence(attempt.Capture))
    {
      SubmissionDebugTelemetry.Publish("Capture skipped: evidence could not attach before input");
      attempt.Capture.Invalidate("capture_attachment_failed");
      return;
    }
    _attempt = attempt;
    session.LinkSubmissionRun(attempt.RunId);
    _session = session;
    SubmissionDebugTelemetry.Publish(
      "Live capture attached; awaiting run_start for " + attempt.RunId.ToString("N").Substring(0, 8)
    );
  }

  public void Clear(RecordingSession session)
  {
    if (_session == session && _attempt != null)
    {
      _clearPending = true;
      SubmissionDebugTelemetry.Publish("Won state detected; recording continues until editor return");
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
    CompleteIdentityRefresh();
    SyncSubmissionAuthorization();
    if (_account != null && _identityRefresh == null && DateTimeOffset.UtcNow >= _identityRefreshAt)
      StartIdentityRefresh();
    if (_changes?.TakeChange() == true)
    {
      _pendingToast = true;
      // Every run_start rechecks current catalog eligibility without issuing an idle run.
    }
    if (
      _pendingToast
      && TUFReplay.Replay.Timeline.ReplayTimelineHud.ShowNotificationToast(
        "TUF chart updated",
        "This chart's TUF information changed. Submission will validate against the current official chart."
      )
    )
      _pendingToast = false;
    if (
      _account != null
      && _account.CanSubmit
      && DateTimeOffset.UtcNow >= _retryAt
      && (_transport == null || _transport.Completion.IsCompleted)
    )
      Prepare();
  }

  // Called after input capture has drained and the recording termination is fixed.
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
    if (_transport != null && !_transport.Completion.IsCompleted)
      return;
    _retryAt = DateTimeOffset.UtcNow.AddSeconds(30);
    if (
      _disposed
      || _account == null
      || !_account.CanSubmit
      || !_levelId.HasValue
      || string.IsNullOrEmpty(_path)
      || TUFReplay.Shared.Settings.TUFReplaySettingStore.Current?.AutoSubmissionDisabled == true
    )
      return;
    SubmissionDebugTelemetry.Publish("Opening reusable level session for TUF #" + _levelId.Value);
    _transport?.Dispose();
    _transport = new TUFReplay.Submission.Transport.LevelSubmissionSession(
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
    if (_account?.CanSubmit == true && _levelId.HasValue)
      _changes = new TUFReplay.Submission.Transport.LevelChangeListener(_account, _levelId.Value);
  }

  private void StartIdentityRefresh()
  {
    if (_account == null || _identityRefresh != null)
      return;

    SubmissionAccount account = _account;
    _identityRefreshAt = DateTimeOffset.UtcNow.AddSeconds(20);
    _identityRefreshAccount = account;
    _identityRefreshCancellation = new CancellationTokenSource();
    CancellationToken cancellation = _identityRefreshCancellation.Token;
    _identityRefresh = Task.Run(async () =>
      await Records.GetAccountIdentity(account, cancellation).ConfigureAwait(false)
    );
    _ = _identityRefresh.ContinueWith(
      completed => _ = completed.Exception,
      CancellationToken.None,
      TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
      TaskScheduler.Default
    );
  }

  private void CompleteIdentityRefresh()
  {
    Task<SubmissionAccountIdentity> refresh = _identityRefresh;
    if (refresh == null || !refresh.IsCompleted)
      return;

    SubmissionAccount account = _identityRefreshAccount;
    _identityRefresh = null;
    _identityRefreshAccount = null;
    _identityRefreshCancellation?.Dispose();
    _identityRefreshCancellation = null;
    if (!ReferenceEquals(account, _account))
      return;

    if (refresh.Status == TaskStatus.RanToCompletion)
    {
      account.ApplyIdentity(refresh.Result);
      SubmissionDebugTelemetry.Publish("TUF account eligibility refreshed");
    }
    else
    {
      if (refresh.IsFaulted)
        _ = refresh.Exception;
      account.MarkIdentityUnavailable();
      SubmissionDebugTelemetry.Publish("TUF account eligibility could not be confirmed");
    }
    SyncSubmissionAuthorization();
  }

  private void SyncSubmissionAuthorization()
  {
    bool canSubmit = _account?.CanSubmit == true;
    if (canSubmit == _submissionAuthorized)
      return;

    _submissionAuthorized = canSubmit;
    if (canSubmit)
    {
      SubmissionDebugTelemetry.Publish("Trusted-tester submission access confirmed");
      WatchLevel();
      Prepare();
      return;
    }

    if (_account != null)
      SubmissionDebugTelemetry.Publish("Submission access unavailable; capture is paused");
    _session?.AbortEvidence("submission_permission_unavailable");
    _session = null;
    _clearPending = false;
    _transport?.Dispose();
    _transport = null;
    _changes?.Dispose();
    _changes = null;
  }

  public void Dispose()
  {
    _disposed = true;
    Disconnect();
    Records.Dispose();
    Authentication.Dispose();
  }
}
