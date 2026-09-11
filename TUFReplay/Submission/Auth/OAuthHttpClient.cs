using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Auth;

public sealed class OAuthRejectedException : Exception { }

public sealed class OAuthHttpClient : IOAuthTokenClient
{
  private readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(15) };
  private readonly OAuthOptions _options;
  public OAuthHttpClient(OAuthOptions options) => _options = options;

  public async Task<JObject> Token(Dictionary<string, string> fields)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    fields["client_id"] = _options.ClientId;
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.TufApi, "oauth/token"))
      { Content = new FormUrlEncodedContent(fields) };
    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest
        || response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new OAuthRejectedException();
    response.EnsureSuccessStatusCode();
    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    var bytes = new byte[16384]; int total = 0;
    while (true)
    {
      int count = await stream.ReadAsync(bytes, total, bytes.Length - total, timeout.Token).ConfigureAwait(false);
      if (count == 0) break;
      total += count;
      if (total == bytes.Length) throw new InvalidOperationException("invalid_oauth_response");
    }
    return JObject.Parse(Encoding.UTF8.GetString(bytes, 0, total));
  }

  public async Task Revoke(string token)
  {
    using var content = new FormUrlEncodedContent(new Dictionary<string, string> {
      ["client_id"] = _options.ClientId, ["token"] = token,
    });
    using var response = await _http.PostAsync(new Uri(_options.TufApi, "oauth/revoke"), content).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
  }
  public void Dispose() => _http.Dispose();
}
