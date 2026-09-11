using TUFReplay.Submission.Transport;

internal static class SubmissionRecoverySuite
{
  public static void RunAll()
  {
    var now = TimeSpan.Zero;
    var window = new UploadRecoveryWindow(() => now);
    now = TimeSpan.FromHours(1);
    window.Recovered();
    Require(window.Failed() == TimeSpan.FromMilliseconds(200), "healthy time is not outage time");
    for (int i = 0; i < 12; i++)
      Require(window.Failed() <= TimeSpan.FromSeconds(2), "more than eight attempts are allowed within the deadline");
    now += TimeSpan.FromSeconds(29);
    window.Recovered();
    Require(window.Failed() == TimeSpan.FromMilliseconds(200), "recovery resets retry backoff");
    now += TimeSpan.FromSeconds(30);
    ExpectExpired(window.Recovered);
    ExpectExpired(() => window.Attempt(CancellationToken.None).Dispose());

    now = TimeSpan.Zero;
    window = new UploadRecoveryWindow(() => now);
    window.Failed();
    now = TimeSpan.FromMilliseconds(29980);
    Require(window.Failed() <= TimeSpan.FromMilliseconds(20), "backoff is capped by remaining time");
    using (var attempt = window.Attempt(CancellationToken.None))
      Require(attempt.Token.WaitHandle.WaitOne(1000), "handshake cancellation uses remaining budget, not a fresh 30 seconds");
    using (var cancelled = new CancellationTokenSource())
    {
      cancelled.Cancel();
      using var attempt = new UploadRecoveryWindow().Attempt(cancelled.Token);
      Require(attempt.IsCancellationRequested, "caller cancellation is retained");
    }
    Console.WriteLine("Submission 30-second recovery deadline tests passed.");
  }

  private static void ExpectExpired(Action action)
  {
    try { action(); }
    catch (UploadRejectedException error) when (error.Message == "upload_connection_expired") { return; }
    throw new InvalidOperationException("Recovery at or after the deadline must be rejected");
  }

  private static void Require(bool condition, string message)
  { if (!condition) throw new InvalidOperationException(message); }
}
