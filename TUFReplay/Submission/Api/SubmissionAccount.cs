using System;
using System.Threading;
using System.Threading.Tasks;

namespace TUFReplay.Submission.Api;

/// <summary>Refresh-capable mod credentials; never serialized through IPC.</summary>
public sealed class SubmissionAccount
{
  public Uri Server { get; }
  private readonly Func<CancellationToken, Task<string>> _accessToken;

  public SubmissionAccount(Uri server, Func<CancellationToken, Task<string>> accessToken)
  {
    Server = server; _accessToken = accessToken;
  }
  internal Task<string> AccessToken(CancellationToken cancellation) => _accessToken(cancellation);
}
