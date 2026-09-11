using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;

namespace TUFReplay.Submission.Auth;

/// <summary>Serializes code exchange and refresh rotation. Tokens never enter IPC DTOs.</summary>
public sealed class OAuthSession : IDisposable
{
  private readonly OAuthOptions _options;
  private readonly ICredentialStore _store;
  private readonly IOAuthTokenClient _http;
  private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
  private string _refresh, _access, _state, _verifier;
  private string _completedState, _completedCodeHash;
  private DateTimeOffset _expires, _pendingExpires;
  private volatile SubmissionAccount _account;
  private volatile bool _disposed, _logout;
  public SubmissionAccount Account => _account;
  public OAuthSession(OAuthOptions options, ICredentialStore store, IOAuthTokenClient http)
  {
    _options = options; _store = store; _http = http;
  }

  public async Task Restore()
  {
    await _gate.WaitAsync().ConfigureAwait(false);
    try
    {
      string saved = _store.Load();
      if (string.IsNullOrEmpty(saved)) return;
      var value = JObject.Parse(saved);
      _refresh = (string)value["refreshToken"];
      _logout = (bool?)value["revocationPending"] == true;
      if (string.IsNullOrEmpty(_refresh)) return;
      if (_logout) await RevokeAndForget().ConfigureAwait(false);
      else await Refresh().ConfigureAwait(false);
    }
    finally { _gate.Release(); }
  }

  public async Task<string> Begin()
  {
    await _gate.WaitAsync().ConfigureAwait(false);
    try
    {
      if (_logout && !string.IsNullOrEmpty(_refresh)) await RevokeAndForget().ConfigureAwait(false);
      _logout = false;
      _completedState = null; _completedCodeHash = null;
      _state = RandomToken(); _verifier = RandomToken();
      _pendingExpires = DateTimeOffset.UtcNow.AddMinutes(10);
      using var sha = SHA256.Create();
      string challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(_verifier)));
      return new Uri(_options.TufApi, "oauth/authorize").AbsoluteUri
        + "?response_type=code&client_id=" + Uri.EscapeDataString(_options.ClientId)
        + "&redirect_uri=" + Uri.EscapeDataString(_options.RedirectUri)
        + "&scope=" + Uri.EscapeDataString("User.Read.Public User.Submission.Create")
        + "&state=" + _state + "&code_challenge_method=S256&code_challenge=" + challenge;
    }
    finally { _gate.Release(); }
  }

  public async Task Complete(string code, string state)
  {
    await _gate.WaitAsync().ConfigureAwait(false);
    try
    {
      if (!_logout && !_disposed && state != null && state == _completedState
          && HashCode(code) == _completedCodeHash) return;
      if (_state == null || state != _state || DateTimeOffset.UtcNow >= _pendingExpires || _logout
          || string.IsNullOrEmpty(code) || code.Length > 512) throw new OAuthRejectedException();
      string verifier = _verifier;
      _state = null; _verifier = null;
      Accept(await _http.Token(new Dictionary<string, string> {
        ["grant_type"] = "authorization_code", ["code"] = code,
        ["redirect_uri"] = _options.RedirectUri, ["code_verifier"] = verifier,
      }).ConfigureAwait(false));
      _completedState = state; _completedCodeHash = HashCode(code);
    }
    finally { _gate.Release(); }
  }

  public async Task<string> AccessToken(CancellationToken cancellation)
  {
    await _gate.WaitAsync(cancellation).ConfigureAwait(false);
    try
    {
      if (_disposed || _logout || string.IsNullOrEmpty(_refresh)) throw new OAuthRejectedException();
      if (_expires <= DateTimeOffset.UtcNow.AddSeconds(60)) await Refresh().ConfigureAwait(false);
      if (_logout) throw new OAuthRejectedException();
      return _access;
    }
    finally { _gate.Release(); }
  }

  public async Task Logout()
  {
    _logout = true; _account = null;
    await _gate.WaitAsync().ConfigureAwait(false);
    try
    {
      _state = null; _verifier = null;
      // Persist the logout intent before the network call. A failed revocation
      // is retried on the next launch rather than silently logging back in.
      SaveCredential();
      await RevokeAndForget().ConfigureAwait(false);
    }
    finally { _account = null; _gate.Release(); }
  }

  private async Task RevokeAndForget()
  {
    if (!string.IsNullOrEmpty(_refresh)) await _http.Revoke(_refresh).ConfigureAwait(false);
    _store.Delete(); _refresh = null; _access = null;
  }

  private async Task Refresh()
  {
    try
    {
      Accept(await _http.Token(new Dictionary<string, string> {
        ["grant_type"] = "refresh_token", ["refresh_token"] = _refresh,
      }).ConfigureAwait(false));
    }
    catch (OAuthRejectedException)
    {
      _account = null; _refresh = null; _access = null;
      _store.Delete(); throw;
    }
  }

  private void Accept(JObject value)
  {
    string access = (string)value["access_token"], refresh = (string)value["refresh_token"];
    int expires = (int?)value["expires_in"] ?? 0;
    string scope = (string)value["scope"] ?? "";
    if (string.IsNullOrEmpty(access) || access.Length > 8192 || string.IsNullOrEmpty(refresh)
        || refresh.Length > 2048 || expires <= 0 || (string)value["token_type"] != "Bearer"
        || !Array.Exists(scope.Split(' '), name => name == "User.Submission.Create"))
      throw new OAuthRejectedException();
    _refresh = refresh;
    SaveCredential();
    _access = access; _expires = DateTimeOffset.UtcNow.AddSeconds(expires);
    if (!_disposed && !_logout && _account == null)
      _account = new SubmissionAccount(_options.SubmissionServer, AccessToken);
  }

  private void SaveCredential() => _store.Save(new JObject {
    ["refreshToken"] = _refresh, ["revocationPending"] = _logout,
  }.ToString(Newtonsoft.Json.Formatting.None));
  private static string HashCode(string code)
  {
    using var sha = SHA256.Create();
    return Base64Url(sha.ComputeHash(Encoding.UTF8.GetBytes(code ?? "")));
  }
  private static string RandomToken()
  {
    var bytes = new byte[32];
    using var random = RandomNumberGenerator.Create(); random.GetBytes(bytes);
    return Base64Url(bytes);
  }
  private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  public void Dispose() { _disposed = true; _account = null; _http.Dispose(); }
}
