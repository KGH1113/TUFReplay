using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Catalog;
using TUFReplay.Submission.Logging;
using TUFReplay.Submission.Sessions;

namespace TUFReplay.Submission.Transport;

/// <summary>One worker owns the level socket, ordered run lifetimes and all network I/O.</summary>
public sealed class LevelSubmissionSession : IDisposable
{
  private readonly Func<CancellationToken, Task<IUploadConnection>> _connect;
  private readonly Func<InstalledLevelContext> _readLevel;
  private readonly string _gameVersion;
  private readonly string _modVersion;
  private readonly CancellationTokenSource _stop = new CancellationTokenSource();
  private readonly ConcurrentQueue<LevelSubmissionAttempt> _queue = new ConcurrentQueue<LevelSubmissionAttempt>();
  private readonly SemaphoreSlim _wake = new SemaphoreSlim(0);
  private readonly object _admission = new object();
  private IUploadConnection _wire;
  private LevelSubmissionAttempt _active;
  private int _count;
  private int _retiring;
  private string _state = "connecting";
  public string State => Volatile.Read(ref _state);
  public Task Completion { get; }
  internal TimeSpan StartTimeout { get; }

  public LevelSubmissionSession(
    SubmissionAccount account,
    string path,
    int levelId,
    string gameVersion,
    string modVersion
  )
    : this(
      async cancellation =>
      {
        var url = new UriBuilder(new Uri(account.Server, "api/v2/levels/" + levelId + "/runs/stream"));
        url.Scheme = url.Scheme == "https" ? "wss" : "ws";
        return new WebSocketUploadConnection(url.Uri, await account.AccessToken(cancellation).ConfigureAwait(false));
      },
      () => InstalledLevelReader.Read(path, levelId),
      gameVersion,
      modVersion
    ) { }

  public LevelSubmissionSession(
    Func<CancellationToken, Task<IUploadConnection>> connect,
    Func<InstalledLevelContext> readLevel,
    string gameVersion,
    string modVersion,
    TimeSpan? startTimeout = null
  )
  {
    _connect = connect;
    _readLevel = readLevel;
    _gameVersion = gameVersion;
    _modVersion = modVersion;
    StartTimeout = startTimeout ?? TimeSpan.FromSeconds(10);
    Completion = Task.Run(Work);
  }

  public LevelSubmissionAttempt Begin(byte[] gameplayHash)
  {
    lock (_admission)
    {
      if (_stop.IsCancellationRequested || Volatile.Read(ref _retiring) != 0)
        return null;
      if (Interlocked.Increment(ref _count) > 8)
      {
        Interlocked.Decrement(ref _count);
        return null;
      }
      var attempt = new LevelSubmissionAttempt(gameplayHash);
      if (attempt.GameplayHash == null)
      {
        Interlocked.Decrement(ref _count);
        attempt.Reject("submission_chart_identity_missing_or_unsupported");
        return attempt;
      }
      _queue.Enqueue(attempt);
      _wake.Release();
      return attempt;
    }
  }

  private async Task Work()
  {
    try
    {
      InstalledLevelContext level = _readLevel();
      while (!_stop.IsCancellationRequested)
      {
        if (_queue.TryDequeue(out var attempt))
        {
          _active = attempt;
          if (Volatile.Read(ref _retiring) != 0 && attempt.Capture.CompletionMeta == null)
            attempt.Capture.Invalidate("level_changed");
          try
          {
            var uploader = new EvidenceUploader(
              () => new LevelRunConnection(this, attempt, level, _gameVersion, _modVersion),
              attempt.Capture,
              progress: progress => SubmissionLog.Publish(attempt.RunId, progress),
              startFailedCapture: true
            );
            await uploader.Run(_stop.Token).ConfigureAwait(false);
            attempt.SetState("sealed");
          }
          catch (Exception error)
          {
            attempt.Reject(error is UploadRejectedException ? error.Message : "level_session_unavailable");
            SubmissionLog.Publish("Attempt unavailable: " + attempt.Failure);
            if (!_stop.IsCancellationRequested && attempt.StartSent && !attempt.ServerTerminal)
              await Abandon(attempt, level).ConfigureAwait(false);
          }
          finally
          {
            _active = null;
            Interlocked.Decrement(ref _count);
          }
          continue;
        }
        if (Volatile.Read(ref _retiring) != 0)
          return;
        try
        {
          await EnsureConnected(_stop.Token).ConfigureAwait(false);
          await _wire.SendControl(new { type = "heartbeat" }, _stop.Token).ConfigureAwait(false);
          var reply = await _wire.Receive(_stop.Token).ConfigureAwait(false);
          if ((string)reply["type"] != "heartbeat")
            throw new UploadRejectedException("level_session_authorization_lost");
        }
        catch (Exception) when (!_stop.IsCancellationRequested)
        {
          ResetConnection();
        }
        await _wake.WaitAsync(1000, _stop.Token).ConfigureAwait(false);
      }
    }
    catch (Exception)
    {
      Volatile.Write(ref _state, "level_session_unavailable");
    }
    finally
    {
      lock (_admission)
      {
        Interlocked.Exchange(ref _retiring, 1);
        while (_queue.TryDequeue(out var queued))
        {
          queued.Capture.Invalidate("level_session_closed");
          queued.SetState("unavailable");
          Interlocked.Decrement(ref _count);
        }
      }
      ResetConnection();
      Volatile.Write(ref _state, "level_session_closed");
    }
  }

  private async Task Abandon(LevelSubmissionAttempt attempt, InstalledLevelContext level)
  {
    // A lost start/ACK may have left a server run alive. Close that exact attempt before the next one.
    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
    deadline.CancelAfter(TimeSpan.FromSeconds(2));
    try
    {
      using var connection = new LevelRunConnection(this, attempt, level, _gameVersion, _modVersion, cleanup: true);
      await connection.Connect(deadline.Token).ConfigureAwait(false);
      await connection
        .SendControl(new { type = "hello", last_acknowledged_sequence = -1 }, deadline.Token)
        .ConfigureAwait(false);
      var reply = await connection.Receive(deadline.Token).ConfigureAwait(false);
      if ((string)reply["type"] == "ready")
        await connection.SendControl(new { type = "fail" }, deadline.Token).ConfigureAwait(false);
    }
    catch
    {
      ResetConnection();
    }
  }

  internal async Task EnsureConnected(CancellationToken cancellation)
  {
    if (_wire != null)
      return;
    var wire = await _connect(cancellation).ConfigureAwait(false);
    try
    {
      await wire.Connect(cancellation).ConfigureAwait(false);
      await wire.SendControl(new { type = "session_hello", protocol_version = 2 }, cancellation).ConfigureAwait(false);
      var reply = await wire.Receive(cancellation).ConfigureAwait(false);
      if ((string)reply["type"] != "session_ready" || (int?)reply["protocol_version"] != 2)
        throw new UploadRejectedException("unsupported_level_session");
      _wire = wire;
      Volatile.Write(ref _state, "ready");
    }
    catch
    {
      wire.Dispose();
      throw;
    }
  }

  internal IUploadConnection Wire => _wire;

  internal void ResetConnection()
  {
    _wire?.Dispose();
    _wire = null;
    Volatile.Write(ref _state, "connecting");
  }

  /// <summary>Level changes drain completed attempts; incomplete capture is abandoned without blocking Unity.</summary>
  public void Retire()
  {
    lock (_admission)
    {
      Interlocked.Exchange(ref _retiring, 1);
      var active = Volatile.Read(ref _active);
      if (active?.Capture.CompletionMeta == null)
        active?.Capture.Invalidate("level_changed");
      foreach (var queued in _queue)
        if (queued.Capture.CompletionMeta == null)
          queued.Capture.Invalidate("level_changed");
    }
    _wake.Release();
  }

  public void Dispose()
  {
    Interlocked.Exchange(ref _retiring, 1);
    _stop.Cancel();
  }
}
