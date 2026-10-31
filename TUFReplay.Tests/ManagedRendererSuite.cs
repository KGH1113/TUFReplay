using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay.Shared.Downloads;

internal static class ManagedRendererSuite
{
  private static string _assetUrl;
  public static void RunAll(string root) => RunAsync(root).GetAwaiter().GetResult();

  private static async Task RunAsync(string root)
  {
    using var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-Renderer-Tests/1.0");
    var release = JObject.Parse(await client.GetStringAsync("https://api.github.com/repos/imnyang/OrbitRender/releases/latest"));
    var asset = (release["assets"] as JArray)?.OfType<JObject>()
      .SingleOrDefault(item => (string)item["name"] == "OrbitRender.zip");
    _assetUrl = (string)asset?["browser_download_url"];
    Check(_assetUrl != null, "latest release contains OrbitRender.zip");
    byte[] package = await client.GetByteArrayAsync(_assetUrl);
    string actualHash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
    Check((string)asset["digest"] == "sha256:" + actualHash && (long)asset["size"] == package.Length,
      "downloaded release matches published size and checksum");
    int requests = 0;
    Func<HttpMessageHandler> fixture = () =>
      new Handler(
        (request, token) =>
        {
          Interlocked.Increment(ref requests);
          return Task.FromResult(Response(request, Content(request, package)));
        }
      );
    string mods = Path.Combine(root, "renderer consent mods");
    using (var installer = new ManagedRendererInstaller(mods, fixture))
    {
      await installer.Work;
      installer.Confirm();
      Check(requests == 0 && installer.Snapshot().Status == "missing", "confirmation requires a pending request");
      installer.Request();
      Check(
        requests == 0 && installer.Snapshot().Status == "awaiting-consent",
        "opening setup does not use the network"
      );
      installer.Cancel();
      Check(requests == 0 && installer.Snapshot().Status == "cancelled", "consent can be skipped");
      installer.Request();
      installer.Confirm();
      installer.Confirm();
      await installer.Work;
      Check(
        requests == 2 && installer.Snapshot().Status == "restart-required",
        "one verified installation for duplicate approval"
      );
      Check(installer.Snapshot(true).Status == "ready", "loaded mod state");
      Check(File.Exists(Path.Combine(mods, "OrbitRender", "OrbitRender.dll")), "published mod payload");
      Check(!Directory.EnumerateDirectories(mods, ".tuf-renderer-install-*").Any(), "staging cleanup after success");
    }
    using (var reused = new ManagedRendererInstaller(mods, fixture))
    {
      await reused.Work;
      reused.Request();
      reused.Confirm();
      Check(
        reused.Snapshot().Status == "restart-required" && requests == 2,
        "existing mod is reused without a download"
      );
    }
    await Rejected(
      root,
      "checksum",
      package,
      fixture: (request, token) =>
        Task.FromResult(
          Response(
            request,
            !request.RequestUri.AbsolutePath.EndsWith(".zip")
              ? Encoding.UTF8.GetBytes(Release(package, new string('0', 64)))
              : Content(request, package)
          )
        ),
      code: "renderer_checksum_mismatch"
    );
    await Rejected(root, "traversal", AddEntry(package, "../../outside.txt"));
    await Rejected(root, "userdata", AddEntry(package, "Settings.json"));
    await Rejected(
      root,
      "unpublished",
      package,
      fixture: (request, token) => Task.FromResult(Response(request, Encoding.UTF8.GetBytes("{}"))),
      code: "renderer_release_invalid"
    );
    await Rejected(
      root,
      "inaccessible",
      package,
      fixture: (request, token) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request }),
      code: "renderer_release_inaccessible"
    );

    string occupied = Path.Combine(root, "renderer occupied mods");
    Directory.CreateDirectory(Path.Combine(occupied, "OrbitRender"));
    File.WriteAllText(Path.Combine(occupied, "OrbitRender", "Settings.json"), "preserve");
    using (var installer = new ManagedRendererInstaller(occupied, fixture))
    {
      await installer.Work;
      installer.Request();
      installer.Confirm();
      await installer.Work;
      Check(
        installer.Snapshot().ErrorCode == "renderer_directory_exists",
        "incomplete existing directory cannot be overwritten"
      );
      Check(
        File.ReadAllText(Path.Combine(occupied, "OrbitRender", "Settings.json")) == "preserve",
        "existing user data retained"
      );
    }
    var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    string cancelled = Path.Combine(root, "renderer cancelled mods");
    using (
      var installer = new ManagedRendererInstaller(
        cancelled,
        () =>
          new Handler(
            async (request, token) =>
            {
              if (request.RequestUri.AbsolutePath.EndsWith(".zip"))
              {
                waiting.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
              }
              return Response(request, Content(request, package));
            }
          )
      )
    )
    {
      await installer.Work;
      installer.Request();
      installer.Confirm();
      await waiting.Task;
      installer.Cancel();
      await installer.Work;
      Check(installer.Snapshot().Status == "cancelled", "active download cancellation");
      Check(!Directory.EnumerateDirectories(cancelled).Any(), "cancelled download never becomes an installed mod");
    }
    Console.WriteLine(
      "PASS: Renderer web consent, verified atomic install, restart state, reuse, cancellation, missing release, checksums, traversal and user-data preservation."
    );
  }

  private static async Task Rejected(
    string root,
    string name,
    byte[] package,
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> fixture = null,
    string code = null
  )
  {
    string mods = Path.Combine(root, "renderer rejected " + name);
    using var installer = new ManagedRendererInstaller(
      mods,
      () => new Handler(fixture ?? ((request, token) => Task.FromResult(Response(request, Content(request, package)))))
    );
    await installer.Work;
    installer.Request();
    installer.Confirm();
    await installer.Work;
    Check(
      installer.Snapshot().Status == "failed" && (code == null || installer.Snapshot().ErrorCode == code),
      name + " rejected"
    );
    Check(!Directory.Exists(Path.Combine(mods, "OrbitRender")), name + " does not publish partial payload");
    Check(!Directory.Exists(mods) || !Directory.EnumerateDirectories(mods).Any(), name + " cleans staging");
  }

  private static byte[] Content(HttpRequestMessage request, byte[] package)
  {
    if (request.RequestUri.AbsolutePath.EndsWith(".zip"))
      return package;
    return Encoding.UTF8.GetBytes(Release(package));
  }

  private static string Release(byte[] package, string hash = null) =>
    new JObject
    {
      ["assets"] = new JArray(new JObject
      {
        ["name"] = "OrbitRender.zip",
        ["browser_download_url"] = _assetUrl,
        ["size"] = package.Length,
        ["digest"] = "sha256:" + (hash ?? Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant()),
      }),
    }.ToString();

  private static byte[] AddEntry(byte[] package, string additional)
  {
    using var bytes = new MemoryStream();
    bytes.Write(package, 0, package.Length);
    bytes.Position = 0;
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Update, true))
    {
      using var writer = new StreamWriter(zip.CreateEntry(additional).Open());
      writer.Write("blocked");
    }
    return bytes.ToArray();
  }

  private static HttpResponseMessage Response(HttpRequestMessage request, byte[] bytes) =>
    new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(bytes) };

  private static void Check(bool ok, string name)
  {
    if (!ok)
      throw new Exception("Renderer installer: " + name);
  }

  private sealed class Handler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _action;

    public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
    {
      _action = action;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      _action(request, token);
  }
}
