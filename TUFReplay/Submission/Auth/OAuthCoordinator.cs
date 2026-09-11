using System;
using System.Threading;
using System.Threading.Tasks;

namespace TUFReplay.Submission.Auth;

/// <summary>Main-thread polling of background authentication; no keychain or HTTP work in Tick.</summary>
public sealed class OAuthCoordinator : IDisposable
{
  private Task _maintenance;
  private DateTimeOffset _nextCheck;
  private bool _restoreNeeded = true;
  public OAuthSession Session { get; }
  public string State { get; private set; }
  public bool Configured => Session != null;

  public OAuthCoordinator(TUFReplaySetting settings)
  {
    try
    {
      var options = new OAuthOptions(settings);
      Session = new OAuthSession(options, CredentialStore.Create(options.CredentialName), new OAuthHttpClient(options));
      State = "restoring";
      _maintenance = Task.Run(Session.Restore);
    }
    catch (Exception) { State = "oauth_not_configured"; }
  }

  public void Tick()
  {
    if (Session == null) return;
    if (_maintenance != null && !_maintenance.IsCompleted) return;
    if (_maintenance?.IsFaulted == true)
    {
      _ = _maintenance.Exception;
      State = "authentication_unavailable";
      _restoreNeeded = true;
    }
    else
    {
      if (_maintenance != null) _restoreNeeded = false;
      State = Session.Account == null ? "disconnected" : "connected";
    }
    _maintenance = null;
    if (DateTimeOffset.UtcNow < _nextCheck) return;
    _nextCheck = DateTimeOffset.UtcNow.AddSeconds(30);
    if (_restoreNeeded) _maintenance = Task.Run(Session.Restore);
    else if (Session.Account != null)
      _maintenance = Task.Run(async () => await Session.AccessToken(CancellationToken.None).ConfigureAwait(false));
  }

  public void Dispose() => Session?.Dispose();
}
