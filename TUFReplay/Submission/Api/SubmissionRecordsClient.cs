using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Submission.Api;

/// <summary>The mod owns API calls; only parsed result DTOs cross the web IPC boundary.</summary>
public sealed class SubmissionRecordsClient : IDisposable
{
  private readonly HttpClient _http;

  public SubmissionRecordsClient()
    : this(new HttpClientHandler { AllowAutoRedirect = false }) { }

  public SubmissionRecordsClient(HttpMessageHandler handler)
  {
    _http = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)));
    _http.Timeout = Timeout.InfiniteTimeSpan;
  }

  public async Task<JObject> Send(
    SubmissionAccount account,
    HttpMethod method,
    string path,
    CancellationToken cancellation = default
  ) => await Send(account, method, path, null, cancellation).ConfigureAwait(false);

  public async Task<JObject> Send(
    SubmissionAccount account,
    HttpMethod method,
    string path,
    JObject body,
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
    if (body != null)
    {
      string serialized = body.ToString(Formatting.None);
      if (Encoding.UTF8.GetByteCount(serialized) > VisualImportLimits.MaxRequestBytes)
        throw new SubmissionRequestException(
          "visual_payload_too_large",
          "The visual preset exceeds the permitted request size.",
          System.Net.HttpStatusCode.RequestEntityTooLarge
        );
      request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");
    }
    using var response = await _http
      .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
      .ConfigureAwait(false);
    await ThrowIfError(response, linked.Token).ConfigureAwait(false);
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

  public async Task UploadAsset(
    SubmissionAccount account,
    string sha256,
    string mediaType,
    byte[] bytes,
    CancellationToken cancellation
  )
  {
    if (account == null)
      throw new InvalidOperationException("login_required");
    if (bytes.Length == 0 || bytes.Length > VisualImportLimits.MaxDecodedAssetBytes)
      throw new InvalidOperationException("visual_payload_too_large");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
    using var request = new HttpRequestMessage(
      HttpMethod.Put,
      new Uri(account.Server, "api/v1/visual-assets/" + Uri.EscapeDataString(sha256))
    );
    request.Headers.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      await account.AccessToken(linked.Token).ConfigureAwait(false)
    );
    request.Content = new ByteArrayContent(bytes);
    request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
    using var response = await _http
      .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token)
      .ConfigureAwait(false);
    await ThrowIfError(response, linked.Token).ConfigureAwait(false);
  }

  internal static async Task ThrowIfError(HttpResponseMessage response, CancellationToken cancellation)
  {
    if (response.IsSuccessStatusCode)
      return;
    string errorBody = await ReadResponseText(response, cancellation).ConfigureAwait(false);
    throw CreateRequestException(response, errorBody);
  }

  private static async Task<string> ReadResponseText(HttpResponseMessage response, CancellationToken cancellation)
  {
    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    var bytes = new byte[262144];
    int total = 0;
    while (true)
    {
      int count = await stream.ReadAsync(bytes, total, bytes.Length - total, cancellation).ConfigureAwait(false);
      if (count == 0)
        break;
      total += count;
      if (total == bytes.Length)
        break;
    }
    return Encoding.UTF8.GetString(bytes, 0, total);
  }

  private static SubmissionRequestException CreateRequestException(HttpResponseMessage response, string body)
  {
    // Reverse proxies may reject the body before the API can return JSON.
    if (response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge)
      return new SubmissionRequestException(
        "visual_payload_too_large",
        "The upload exceeds the server limit. Reduce the preset assets and try again.",
        response.StatusCode
      );
    string code = null;
    string message = null;
    string description = null;
    try
    {
      JObject parsed = JObject.Parse(body ?? "{}");
      if (parsed["error"] is JObject nestedError)
      {
        code = (string)nestedError["code"];
        message = (string)nestedError["message"];
      }
      else if (parsed["error"]?.Type == JTokenType.String)
      {
        string errorText = parsed["error"].Value<string>();
        code = ExtractErrorCode(errorText);
        message = code == null ? null : errorText;
      }
      code = code ?? (string)parsed["code"];
      message = message ?? (string)parsed["message"];
      description = (string)parsed["description"];
    }
    catch (JsonException)
    {
      code = ExtractErrorCode(body);
      message = code == null ? null : body?.Trim();
    }
    bool descriptionIsCode = false;
    if (string.IsNullOrWhiteSpace(code))
    {
      code = ExtractErrorCode(description);
      descriptionIsCode = !string.IsNullOrWhiteSpace(code);
    }
    code = string.IsNullOrWhiteSpace(code) ? "submission_request_failed" : code;
    message = string.IsNullOrWhiteSpace(message)
      ? (descriptionIsCode ? description : "The submission server rejected the request.")
      : message;
    return new SubmissionRequestException(code, message, response.StatusCode);
  }

  private static bool IsErrorCode(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return false;
    foreach (char character in value)
      if (!(character == '_' || character >= 'a' && character <= 'z' || character >= '0' && character <= '9'))
        return false;
    return true;
  }

  private static string ExtractErrorCode(string description)
  {
    if (string.IsNullOrWhiteSpace(description))
      return null;
    string trimmed = description.Trim();
    if (IsErrorCode(trimmed))
      return trimmed;
    int separator = trimmed.IndexOf(':');
    if (separator > 0)
    {
      string prefix = trimmed.Substring(0, separator).Trim();
      if (IsErrorCode(prefix))
        return prefix;
    }
    return null;
  }
}

public sealed class SubmissionRequestException : HttpRequestException
{
  public string Code { get; }
  public System.Net.HttpStatusCode StatusCode { get; }

  public SubmissionRequestException(string code, string message, System.Net.HttpStatusCode statusCode)
    : base(message)
  {
    Code = code;
    StatusCode = statusCode;
  }
}
