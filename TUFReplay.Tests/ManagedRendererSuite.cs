using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay.Shared.Downloads;

internal static class ManagedRendererSuite
{
  public static void RunAll(string root) => RunAsync(root).GetAwaiter().GetResult();

  private static async Task RunAsync(string root)
  {
    byte[] package = Zip();
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
        requests == 3 && installer.Snapshot().Status == "restart-required",
        "one verified installation for duplicate approval"
      );
      Check(installer.Snapshot(true).Status == "ready", "loaded mod state");
      Check(File.Exists(Path.Combine(mods, "TUFReplay-Renderer", "TUFReplay-Renderer.dll")), "published mod payload");
      Check(!Directory.EnumerateDirectories(mods, ".tuf-renderer-install-*").Any(), "staging cleanup after success");
    }
    using (var reused = new ManagedRendererInstaller(mods, fixture))
    {
      await reused.Work;
      reused.Request();
      reused.Confirm();
      Check(
        reused.Snapshot().Status == "restart-required" && requests == 3,
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
            request.RequestUri.AbsolutePath.EndsWith(".download.json")
              ? Encoding.UTF8.GetBytes(Manifest(package, new string('0', 64)))
              : Content(request, package)
          )
        ),
      code: "renderer_checksum_mismatch"
    );
    await Rejected(root, "traversal", Zip("TUFReplay-Renderer/../../outside.txt"));
    await Rejected(root, "userdata", Zip("TUFReplay-Renderer/Settings.json"));
    await Rejected(
      root,
      "unpublished",
      package,
      fixture: (request, token) => Task.FromResult(Response(request, Encoding.UTF8.GetBytes("[]"))),
      code: "renderer_release_unavailable"
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
    Directory.CreateDirectory(Path.Combine(occupied, "TUFReplay-Renderer"));
    File.WriteAllText(Path.Combine(occupied, "TUFReplay-Renderer", "Settings.json"), "preserve");
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
        File.ReadAllText(Path.Combine(occupied, "TUFReplay-Renderer", "Settings.json")) == "preserve",
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
    Check(!Directory.Exists(Path.Combine(mods, "TUFReplay-Renderer")), name + " does not publish partial payload");
    Check(!Directory.Exists(mods) || !Directory.EnumerateDirectories(mods).Any(), name + " cleans staging");
  }

  private static byte[] Content(HttpRequestMessage request, byte[] package)
  {
    if (request.RequestUri.AbsolutePath.EndsWith(".zip"))
      return package;
    if (request.RequestUri.AbsolutePath.EndsWith(".download.json"))
      return Encoding.UTF8.GetBytes(Manifest(package));
    string prefix = "https://github.com/KGH1113/TUFReplay-Renderer/releases/download/v0.2.0/";
    return Encoding.UTF8.GetBytes(
      new JArray(
        new JObject { ["draft"] = true, ["published_at"] = "2030-01-01T00:00:00Z" },
        new JObject
        {
          ["draft"] = false,
          ["prerelease"] = true,
          ["published_at"] = "2026-10-05T00:00:00Z",
          ["assets"] = new JArray(
            new JObject
            {
              ["name"] = "TUFReplay-Renderer.zip",
              ["browser_download_url"] = prefix + "TUFReplay-Renderer.zip",
            },
            new JObject
            {
              ["name"] = "TUFReplay-Renderer.download.json",
              ["browser_download_url"] = prefix + "TUFReplay-Renderer.download.json",
            }
          ),
        }
      ).ToString()
    );
  }

  private static string Manifest(byte[] package, string hash = null) =>
    new JObject
    {
      ["schemaVersion"] = 1,
      ["modId"] = "TUFReplay-Renderer",
      ["version"] = "0.2.0",
      ["packageAsset"] = "TUFReplay-Renderer.zip",
      ["bytes"] = package.Length,
      ["sha256"] = hash ?? Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
    }.ToString();

  private static byte[] Zip(string additional = null)
  {
    using var bytes = new MemoryStream();
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
    {
      void Add(string path, string content)
      {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open());
        writer.Write(content);
      }
      Add("TUFReplay-Renderer/Info.json", "{\"Id\":\"TUFReplay-Renderer\",\"Version\":\"0.2.0\"}");
      Add("TUFReplay-Renderer/TUFReplay-Renderer.dll", "fixture");
      Add("TUFReplay-Renderer/LICENSE.md", "fixture license");
      if (additional != null)
        Add(additional, "blocked");
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
