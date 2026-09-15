using System;
using System.Threading;
using System.Threading.Tasks;

namespace TUFReplay.Submission.Api;

/// <summary>Refresh-capable mod credentials; never serialized through IPC.</summary>
public sealed class SubmissionAccount
{
  private static readonly TimeSpan IdentityFreshness = TimeSpan.FromSeconds(30);

  private sealed class IdentitySnapshot
  {
    public readonly SubmissionAccountIdentity Value;
    public readonly DateTimeOffset RefreshedAt;
    public readonly bool Unavailable;

    public IdentitySnapshot(SubmissionAccountIdentity value, bool unavailable)
    {
      Value = value;
      RefreshedAt = unavailable ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow;
      Unavailable = unavailable;
    }
  }

  public Uri Server { get; }
  private readonly Func<CancellationToken, Task<string>> _accessToken;
  private volatile IdentitySnapshot _identity;

  public string Username => _identity?.Value.Username;
  public string Nickname => _identity?.Value.Nickname;
  public bool CanSubmit
  {
    get
    {
      IdentitySnapshot identity = _identity;
      return IsFresh(identity) && identity.Value.CanSubmit;
    }
  }
  public string DenialReason
  {
    get
    {
      IdentitySnapshot identity = _identity;
      return IsFresh(identity) ? identity.Value.DenialReason : null;
    }
  }
  public string IdentityStatus
  {
    get
    {
      IdentitySnapshot identity = _identity;
      if (identity?.Unavailable == true)
        return "unavailable";
      if (identity?.Value == null)
        return "checking";
      return IsFresh(identity) ? "available" : "stale";
    }
  }

  public SubmissionAccount(Uri server, Func<CancellationToken, Task<string>> accessToken)
  {
    Server = server;
    _accessToken = accessToken;
  }

  public void ApplyIdentity(SubmissionAccountIdentity identity)
  {
    if (identity == null)
      throw new ArgumentNullException(nameof(identity));
    _identity = new IdentitySnapshot(identity, unavailable: false);
  }

  public void MarkIdentityUnavailable() => _identity = new IdentitySnapshot(_identity?.Value, unavailable: true);

  internal Task<string> AccessToken(CancellationToken cancellation) => _accessToken(cancellation);

  private static bool IsFresh(IdentitySnapshot identity) =>
    identity != null
    && !identity.Unavailable
    && identity.Value != null
    && DateTimeOffset.UtcNow - identity.RefreshedAt <= IdentityFreshness;
}
