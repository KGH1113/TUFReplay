using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Catalog;

namespace TUFReplay.Submission.Api;

public sealed class RunIssuanceClient : IDisposable
{
  private readonly HttpClient _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromMinutes(5) };

  public async Task<IssuedRun> Issue(SubmissionAccount account, InstalledLevelContext level,
    string gameVersion, string modVersion, CancellationToken cancellation)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(account.Server, "api/v1/runs"));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await account.AccessToken(cancellation).ConfigureAwait(false));
    request.Content = new StringContent(JsonConvert.SerializeObject(new {
      protocol_version = 1, client_game_version = gameVersion, client_mod_version = modVersion,
      tuf_level_id = level.LevelId, client_tuf_file_id = level.FileId,
      client_installed_payload_hash_hex = level.PayloadHash, client_payload_hash_version = 1,
      client_level_relative_path = level.RelativePath,
    }), Encoding.UTF8, "application/json");
    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    var bytes = new byte[8192]; int total = 0; int count;
    while ((count = await stream.ReadAsync(bytes, total, bytes.Length - total, cancellation).ConfigureAwait(false)) > 0)
    {
      total += count;
      if (total == bytes.Length) throw new InvalidOperationException("Oversized issuance response.");
    }
    var data = JObject.Parse(Encoding.UTF8.GetString(bytes, 0, total));
    if (!Guid.TryParse((string)data["run_id"], out var id)
        || !DateTimeOffset.TryParse((string)data["lease_expires_at"], out var expiry)
        || string.IsNullOrEmpty((string)data["upload_token"]))
      throw new InvalidOperationException("Invalid issuance response.");
    var leaseDurationMs = (long?)data["lease_duration_ms"];
    var leaseDuration = leaseDurationMs > 0
      ? TimeSpan.FromMilliseconds(leaseDurationMs.Value)
      : expiry - DateTimeOffset.UtcNow;
    if (leaseDuration <= TimeSpan.Zero)
      throw new InvalidOperationException("Expired issuance response.");
    string expectedPath = "/api/v1/runs/" + id + "/stream";
    if ((string)data["websocket_url"] != expectedPath) throw new InvalidOperationException("Unexpected upload URL.");
    var websocket = new UriBuilder(account.Server) { Scheme = account.Server.Scheme == "https" ? "wss" : "ws", Path = expectedPath };
    return new IssuedRun(id, websocket.Uri, (string)data["upload_token"], expiry, leaseDuration);
  }

  public void Dispose() => _http.Dispose();
}
