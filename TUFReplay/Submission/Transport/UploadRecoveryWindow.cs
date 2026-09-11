using System;
using System.Diagnostics;
using System.Threading;

namespace TUFReplay.Submission.Transport;

/// <summary>One outage deadline, shared by reconnect backoff and the entire handshake.</summary>
public sealed class UploadRecoveryWindow
{
  private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);
  private readonly Func<TimeSpan> _now;
  private TimeSpan? _started;
  private int _retries;

  public UploadRecoveryWindow(Func<TimeSpan> now = null)
  {
    var clock = Stopwatch.StartNew();
    _now = now ?? (() => clock.Elapsed);
  }

  public void Recovered()
  {
    EnsureWithinDeadline();
    _started = null;
    _retries = 0;
  }

  public void EnsureWithinDeadline()
  {
    if (_started.HasValue && Remaining <= TimeSpan.Zero)
      throw new UploadRejectedException("upload_connection_expired");
  }

  public TimeSpan Failed()
  {
    if (!_started.HasValue) _started = _now();
    EnsureWithinDeadline();
    _retries = Math.Min(_retries + 1, 10);
    return TimeSpan.FromMilliseconds(Math.Min(200 * _retries, Remaining.TotalMilliseconds));
  }

  public CancellationTokenSource Attempt(CancellationToken cancellation)
  {
    EnsureWithinDeadline();
    var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    if (_started.HasValue) deadline.CancelAfter(Remaining);
    return deadline;
  }

  private TimeSpan Remaining => Limit - (_now() - _started.Value);
}
