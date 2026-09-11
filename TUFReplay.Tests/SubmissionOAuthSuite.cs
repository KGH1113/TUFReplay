using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay;
using TUFReplay.Submission.Auth;

internal static class SubmissionOAuthSuite
{
  public static void RunAll() => Verify().GetAwaiter().GetResult();

  private static async Task Verify()
  {
    var options = new OAuthOptions(new TUFReplaySetting {
      AutoSubmissionOAuthClientId = "official-fixture",
      AutoSubmissionServerUrl = "https://submission.example",
    });
    var store = new MemoryCredentials();
    var provider = new Provider();
    using var session = new OAuthSession(options, store, provider);
    string url = await session.Begin();
    var query = new Uri(url).Query.TrimStart('?').Split('&')
      .Select(item => item.Split('=', 2)).ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
    Require(query["code_challenge_method"] == "S256", "PKCE is mandatory");
    await Reject(() => session.Complete("code", "wrong-state"));
    Require(provider.Codes == 0, "a forged callback never reaches token exchange");
    await session.Complete("code", query["state"]);
    using var hash = SHA256.Create();
    string challenge = Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(provider.Verifier)))
      .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    Require(challenge == query["code_challenge"], "the mod retains the matching PKCE verifier");
    Require(!url.Contains(provider.Verifier), "the verifier never appears in browser navigation");
    await session.Complete("code", query["state"]);
    Require(provider.Codes == 1, "a lost IPC callback response does not reuse an OAuth code");
    var originalAccount = session.Account;
    await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => session.AccessToken(CancellationToken.None)));
    Require(provider.Refreshes == 1, "concurrent API calls serialize refresh rotation");
    Require(ReferenceEquals(originalAccount, session.Account), "refresh never replaces the capturing account");
    provider.RevokeFails = true;
    try { await session.Logout(); throw new Exception("expected unavailable server"); }
    catch (HttpRequestException) { }
    Require(session.Account == null, "logout stops collection despite server failure");
    Require((bool)JObject.Parse(store.Value)["revocationPending"], "logout intent persists securely");
    await Reject(() => session.AccessToken(CancellationToken.None));
    var retryProvider = new Provider();
    using var restarted = new OAuthSession(options, store, retryProvider);
    await restarted.Restore();
    Require(restarted.Account == null && store.Value == null, "pending logout never automatically signs back in");
    Require(retryProvider.Revocations == 1 && retryProvider.Refreshes == 0, "restart retries revocation only");
    Console.WriteLine("Submission OAuth PKCE, refresh and logout recovery tests passed.");
  }

  private static async Task Reject(Func<Task> action)
  {
    try { await action(); throw new Exception("expected rejected authentication"); }
    catch (OAuthRejectedException) { }
  }
  private static void Require(bool value, string message)
  {
    if (!value) throw new InvalidOperationException(message);
  }
  private sealed class MemoryCredentials : ICredentialStore
  {
    public string Value;
    public string Load() => Value;
    public void Save(string token) => Value = token;
    public void Delete() => Value = null;
  }
  private sealed class Provider : IOAuthTokenClient
  {
    public int Codes, Refreshes, Revocations;
    public string Verifier;
    public bool RevokeFails;
    public Task<JObject> Token(Dictionary<string, string> fields)
    {
      bool code = fields["grant_type"] == "authorization_code";
      if (code) { Codes++; Verifier = fields["code_verifier"]; }
      else Refreshes++;
      return Task.FromResult(new JObject {
        ["access_token"] = "fixture-access", ["refresh_token"] = "fixture-refresh-" + Refreshes,
        ["token_type"] = "Bearer", ["expires_in"] = code ? 1 : 3600,
        ["scope"] = "User.Read.Public User.Submission.Create",
      });
    }
    public Task Revoke(string token)
    {
      Revocations++;
      if (RevokeFails) throw new HttpRequestException();
      return Task.CompletedTask;
    }
    public void Dispose() { }
  }
}
