using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Auth;

internal static class SubmissionOAuthSuite
{
  public static void RunAll() => Verify().GetAwaiter().GetResult();

  private static async Task Verify()
  {
    var defaults = new OAuthOptions(
      new TUFReplaySetting
      {
        AutoSubmissionOAuthClientId = "",
        AutoSubmissionTufApiUrl = "",
        AutoSubmissionServerUrl = "",
        AutoSubmissionOAuthRedirectUri = "",
      }
    );
    Require(
      defaults.ClientId == TUFReplaySetting.DefaultAutoSubmissionOAuthClientId,
      "blank legacy client ID uses the official OAuth app"
    );
    Require(
      defaults.TufApi.AbsoluteUri == TUFReplaySetting.DefaultAutoSubmissionTufApiUrl + "/",
      "blank legacy TUF API uses the official API"
    );
    Require(
      defaults.SubmissionServer.AbsoluteUri == TUFReplaySetting.DefaultAutoSubmissionServerUrl + "/",
      "blank legacy server uses the public Rust API"
    );
    Require(
      defaults.RedirectUri == TUFReplaySetting.DefaultAutoSubmissionOAuthRedirectUri,
      "blank legacy callback uses the public OAuth callback"
    );

    var custom = new OAuthOptions(
      new TUFReplaySetting
      {
        AutoSubmissionOAuthClientId = "custom-client",
        AutoSubmissionTufApiUrl = "https://tuf.example",
        AutoSubmissionServerUrl = "https://submission.example",
        AutoSubmissionOAuthRedirectUri = "https://custom.example/callback",
      }
    );
    Require(
      custom.ClientId == "custom-client" && custom.SubmissionServer.Host == "submission.example",
      "explicit custom OAuth and submission settings are preserved"
    );

    var account = new SubmissionAccount(new Uri("https://submission.example"), _ => Task.FromResult("fixture-access"));
    Require(!account.CanSubmit && account.IdentityStatus == "checking", "account eligibility starts fail-closed");
    var missingCapability = SubmissionAccountIdentity.Parse(
      new JObject
      {
        ["owner_id"] = "670cac2c-8175-46a6-87f7-b92741d4499f",
        ["grant_id"] = "00000000-0000-0000-0000-000000000042",
        ["client_id"] = "official-client",
        ["username"] = "player",
        ["nickname"] = JValue.CreateNull(),
      }
    );
    account.ApplyIdentity(missingCapability);
    Require(
      !account.CanSubmit && account.IdentityStatus == "available",
      "an account response without can_submit is denied by default"
    );
    Require(
      account.Username == "player" && account.Nickname == null,
      "account display fields are parsed without exposing credentials"
    );
    account.ApplyIdentity(
      SubmissionAccountIdentity.Parse(
        new JObject
        {
          ["owner_id"] = "670cac2c-8175-46a6-87f7-b92741d4499f",
          ["grant_id"] = "00000000-0000-0000-0000-000000000042",
          ["client_id"] = "official-client",
          ["username"] = "player",
          ["nickname"] = "Player",
          ["can_submit"] = true,
          ["denial_reason"] = JValue.CreateNull(),
        }
      )
    );
    Require(account.CanSubmit && account.Nickname == "Player", "fresh account eligibility is exposed");
    account.MarkIdentityUnavailable();
    Require(
      !account.CanSubmit && account.IdentityStatus == "unavailable",
      "an unavailable refresh immediately revokes local eligibility"
    );

    var options = new OAuthOptions(
      new TUFReplaySetting
      {
        AutoSubmissionOAuthClientId = "official-fixture",
        AutoSubmissionServerUrl = "https://submission.example",
      }
    );
    var store = new MemoryCredentials();
    var provider = new Provider();
    using var session = new OAuthSession(options, store, provider);
    string url = await session.Begin();
    var query = new Uri(url)
      .Query.TrimStart('?')
      .Split('&')
      .Select(item => item.Split('=', 2))
      .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));
    Require(query["code_challenge_method"] == "S256", "PKCE is mandatory");
    await Reject(() => session.Complete("code", "wrong-state"));
    Require(provider.Codes == 0, "a forged callback never reaches token exchange");
    await session.Complete("code", query["state"]);
    using var hash = SHA256.Create();
    string challenge = Convert
      .ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(provider.Verifier)))
      .TrimEnd('=')
      .Replace('+', '-')
      .Replace('/', '_');
    Require(challenge == query["code_challenge"], "the mod retains the matching PKCE verifier");
    Require(!url.Contains(provider.Verifier), "the verifier never appears in browser navigation");
    await session.Complete("code", query["state"]);
    Require(provider.Codes == 1, "a lost IPC callback response does not reuse an OAuth code");
    var originalAccount = session.Account;
    await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => session.AccessToken(CancellationToken.None)));
    Require(provider.Refreshes == 1, "concurrent API calls serialize refresh rotation");
    Require(ReferenceEquals(originalAccount, session.Account), "refresh never replaces the capturing account");
    provider.RevokeFails = true;
    try
    {
      await session.Logout();
      throw new Exception("expected unavailable server");
    }
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
    try
    {
      await action();
      throw new Exception("expected rejected authentication");
    }
    catch (OAuthRejectedException) { }
  }

  private static void Require(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
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
    public int Codes,
      Refreshes,
      Revocations;
    public string Verifier;
    public bool RevokeFails;

    public Task<JObject> Token(Dictionary<string, string> fields)
    {
      bool code = fields["grant_type"] == "authorization_code";
      if (code)
      {
        Codes++;
        Verifier = fields["code_verifier"];
      }
      else
        Refreshes++;
      return Task.FromResult(
        new JObject
        {
          ["access_token"] = "fixture-access",
          ["refresh_token"] = "fixture-refresh-" + Refreshes,
          ["token_type"] = "Bearer",
          ["expires_in"] = code ? 1 : 3600,
          ["scope"] = "User.Read.Public User.Submission.Create",
        }
      );
    }

    public Task Revoke(string token)
    {
      Revocations++;
      if (RevokeFails)
        throw new HttpRequestException();
      return Task.CompletedTask;
    }

    public void Dispose() { }
  }
}
