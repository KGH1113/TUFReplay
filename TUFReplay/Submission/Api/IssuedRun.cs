using System;
using System.Diagnostics;

namespace TUFReplay.Submission.Api;

public sealed class IssuedRun
{
  public Guid Id { get; }
  public Uri WebSocketUrl { get; }
  internal string UploadToken { get; }
  public DateTimeOffset ExpiresAt { get; }
  private readonly TimeSpan _leaseDuration;
  private readonly long _receivedAt;

  public IssuedRun(Guid id, Uri socket, string token, DateTimeOffset expiresAt, TimeSpan leaseDuration)
  {
    Id = id;
    WebSocketUrl = socket;
    UploadToken = token;
    ExpiresAt = expiresAt;
    _leaseDuration = leaseDuration;
    _receivedAt = Stopwatch.GetTimestamp();
  }

  public bool HasRemainingLease(TimeSpan minimum)
  {
    var elapsed = TimeSpan.FromSeconds(
      (Stopwatch.GetTimestamp() - _receivedAt) / (double)Stopwatch.Frequency
    );
    return _leaseDuration - elapsed > minimum;
  }
}
