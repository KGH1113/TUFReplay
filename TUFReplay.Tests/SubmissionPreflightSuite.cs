using System.Net;
using System.Net.Http;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Sessions;

internal static class SubmissionPreflightSuite
{
  public static void RunAll(string root)
  {
    string folder = Path.Combine(root, "preflight");
    Directory.CreateDirectory(folder);
    File.WriteAllText(
      Path.Combine(folder, ".tufhelperlite-level.json"),
      "{\"Id\":8068,\"DownloadedFileId\":\"test-file\",\"InstalledPayloadHash\":\"" + new string('a', 64) + "\"}"
    );
    string chart = Path.Combine(folder, "level.adofai");
    File.WriteAllText(chart, "{\"pathData\":\"R\"}");
    var account = new SubmissionAccount(new Uri("https://example.test/"), _ => Task.FromResult("test-token"));

    foreach (
      var error in new[]
      {
        (HttpStatusCode.UnprocessableEntity, "{\"code\":\"level_not_eligible\"}", "level_not_eligible"),
        (HttpStatusCode.ServiceUnavailable, "{\"code\":\"catalog_unavailable\"}", "catalog_unavailable"),
        (
          HttpStatusCode.BadRequest,
          "{\"error\":\"Bad Request\",\"description\":\"catalog_unavailable\"}",
          "catalog_unavailable"
        ),
        (HttpStatusCode.BadGateway, "upstream unavailable", "submission_request_failed"),
        (HttpStatusCode.OK, "not valid JSON", "level_preparation_failed"),
      }
    )
    {
      using var client = new RunIssuanceClient(new ResponseHandler(error.Item1, error.Item2));
      using var preflight = new SubmissionPreflight(client, account, chart, 8068, "3.4.1", "test");
      Wait(preflight);
      TestFixture.Assert(preflight.Error == error.Item3, "Preflight did not preserve the server error: " + error.Item3);
      TestFixture.Assert(preflight.Take() == null && !preflight.IsReady, "Rejected preflight published a run.");
    }

    using (var handler = new ResponseHandler(HttpStatusCode.OK, "{}"))
    using (var client = new RunIssuanceClient(handler))
    using (var preflight = new SubmissionPreflight(client, account, chart, 9999, "3.4.1", "test"))
    {
      Wait(preflight);
      TestFixture.Assert(
        preflight.Error == "level_preparation_failed",
        "Local manifest failures need the generic preparation error."
      );
      TestFixture.Assert(handler.Calls == 0, "Invalid installation sent a request to the submission API.");
    }
    Console.WriteLine("Submission preflight HTTP error propagation tests passed.");
  }

  private static void Wait(SubmissionPreflight preflight) =>
    TestFixture.Assert(
      SpinWait.SpinUntil(() => preflight.Finished, TimeSpan.FromSeconds(5)),
      "Preflight did not finish."
    );

  private sealed class ResponseHandler : HttpMessageHandler
  {
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public int Calls { get; private set; }

    public ResponseHandler(HttpStatusCode status, string body)
    {
      _status = status;
      _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken
    )
    {
      Calls++;
      TestFixture.Assert(request.RequestUri.AbsolutePath == "/api/v1/runs", "Unexpected issuance endpoint.");
      return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
    }
  }
}
