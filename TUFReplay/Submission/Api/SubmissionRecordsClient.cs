using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Api;

/// <summary>The mod owns API calls; only parsed result DTOs cross the web IPC boundary.</summary>
public sealed class SubmissionRecordsClient : IDisposable
{
  private readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

  public async Task<JObject> Send(
    SubmissionAccount account,
    HttpMethod method,
    string path,
    CancellationToken cancellation = default
  )
  {
    if (account == null)
      throw new InvalidOperationException("login_required");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
    using var request = new HttpRequestMessage(method, new Uri(account.Server, path));
    request.Headers.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      await account.AccessToken(linked.Token).ConfigureAwait(false)
    );
    using var response = await _http
      .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    var bytes = new byte[262144];
    int total = 0;
    while (true)
    {
      int count = await stream.ReadAsync(bytes, total, bytes.Length - total, linked.Token).ConfigureAwait(false);
      if (count == 0)
        break;
      total += count;
      if (total == bytes.Length)
        throw new InvalidOperationException("oversized_submission_response");
    }
    return JObject.Parse(Encoding.UTF8.GetString(bytes, 0, total));
  }

  public async Task<SubmissionAccountIdentity> GetAccountIdentity(
    SubmissionAccount account,
    CancellationToken cancellation = default
  )
  {
    JObject value = await Send(account, HttpMethod.Get, "api/v1/account", cancellation).ConfigureAwait(false);
    return SubmissionAccountIdentity.Parse(value);
  }

  public void Dispose() => _http.Dispose();
}
