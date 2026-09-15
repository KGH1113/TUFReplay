using System;

namespace TUFReplay.Submission.Auth;

public sealed class OAuthOptions
{
  public string ClientId { get; }
  public Uri TufApi { get; }
  public Uri SubmissionServer { get; }
  public string RedirectUri { get; }
  public string CredentialName => TufApi.AbsoluteUri + "|" + ClientId;

  public OAuthOptions(TUFReplaySetting settings)
  {
    ClientId = ValueOrDefault(
      settings.AutoSubmissionOAuthClientId,
      TUFReplaySetting.DefaultAutoSubmissionOAuthClientId
    );
    TufApi = Origin(ValueOrDefault(settings.AutoSubmissionTufApiUrl, TUFReplaySetting.DefaultAutoSubmissionTufApiUrl));
    SubmissionServer = Origin(
      ValueOrDefault(settings.AutoSubmissionServerUrl, TUFReplaySetting.DefaultAutoSubmissionServerUrl)
    );
    RedirectUri = ValueOrDefault(
      settings.AutoSubmissionOAuthRedirectUri,
      TUFReplaySetting.DefaultAutoSubmissionOAuthRedirectUri
    );
    if (
      string.IsNullOrWhiteSpace(ClientId)
      || ClientId.Length > 64
      || !Uri.TryCreate(RedirectUri, UriKind.Absolute, out var callback)
      || (callback.Scheme != "https" && !(callback.Scheme == "http" && callback.IsLoopback))
    )
      throw new InvalidOperationException("oauth_not_configured");
  }

  private static string ValueOrDefault(string value, string fallback) =>
    string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

  private static Uri Origin(string value)
  {
    if (
      !Uri.TryCreate(value, UriKind.Absolute, out var uri)
      || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
      || uri.AbsolutePath != "/"
      || !string.IsNullOrEmpty(uri.UserInfo)
      || !string.IsNullOrEmpty(uri.Query)
      || !string.IsNullOrEmpty(uri.Fragment)
    )
      throw new InvalidOperationException("oauth_not_configured");
    return uri;
  }
}
