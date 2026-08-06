using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Bootstrap;
using TUFReplay.UpdateEngine;
using TUFReplay.Update;

internal static class UpdaterTests
{
  private static int _passed;

  public static void RunAll()
  {
    Run("manifest validation", TestManifestValidation);
    Run("runtime retention and trial recovery", TestRuntimeRetention);
    Run("full runtime package installation", TestPackageInstallation);
    Run("checksum mismatch rejection", TestChecksumMismatch);
    Run("package size mismatch rejection", TestPackageSizeMismatch);
    Run("unsafe archive rejection", TestUnsafeArchive);
    Run("beta latest release selection", TestBetaReleaseSelection);
    Run("one-restart AdofaiIpc bridge staging", TestBridgeTransition);
    if (string.Equals(Environment.GetEnvironmentVariable("TUFREPLAY_RELEASE_E2E"), "1", StringComparison.Ordinal))
      Run("published release one-restart bridge", TestPublishedReleaseBridge);
    Console.WriteLine($"Passed {_passed} updater tests.");
  }

  private static void TestManifestValidation()
  {
    string valid = Manifest("0.2.0", 10, new string('a', 64));
    ReleaseManifest parsed = ReleaseManifest.Parse(valid);
    Assert(parsed.Version == "0.2.0", "Manifest version was not parsed.");
    AssertThrows<InvalidDataException>(() => ReleaseManifest.Parse(Manifest("0.2.0", 0, new string('a', 64))));
    AssertThrows<InvalidDataException>(() => ReleaseManifest.Parse(Manifest("0.2.0", 10, "bad")));
    AssertThrows<InvalidDataException>(() => ReleaseManifest.Parse(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
  }

  private static void TestRuntimeRetention()
  {
    using TemporaryDirectory temporary = new();
    CreateRuntime(temporary.Path, "0.1.0-beta.3");
    CreateRuntime(temporary.Path, "0.1.0-beta.2");
    CreateRuntime(temporary.Path, "0.1.0-beta.4");
    RuntimeStore store = new(temporary.Path);
    RuntimeState state = new()
    {
      Current = "0.1.0-beta.3",
      Previous = "0.1.0-beta.2",
      Trial = "0.1.0-beta.4",
    };
    File.WriteAllText(Path.Combine(temporary.Path, "TUFReplay.dll"), "legacy");
    Directory.CreateDirectory(Path.Combine(temporary.Path, "Assets"));
    Directory.CreateDirectory(Path.Combine(temporary.Path, "Data"));
    File.WriteAllText(Path.Combine(temporary.Path, "Data", "activity.sqlite"), "user-data");
    store.Save(state);
    RuntimeState repaired = store.LoadAndRepair();
    Assert(repaired.Trial == null, "An abandoned trial was not cleared.");
    Assert(!Directory.Exists(RuntimePath(temporary.Path, "0.1.0-beta.4")), "An abandoned trial was not deleted.");
    Assert(!File.Exists(Path.Combine(temporary.Path, "TUFReplay.dll")), "The legacy root payload was not removed.");
    Assert(!Directory.Exists(Path.Combine(temporary.Path, "Assets")), "Legacy root assets were not removed.");
    Assert(File.Exists(Path.Combine(temporary.Path, "Data", "activity.sqlite")), "Persistent data was removed.");

    CreateRuntime(temporary.Path, "0.1.0-beta.4");
    store.Promote(repaired, "0.1.0-beta.4");
    Assert(repaired.Current == "0.1.0-beta.4", "The new runtime was not promoted.");
    Assert(repaired.Previous == "0.1.0-beta.3", "The previous runtime was not retained.");
    Assert(!Directory.Exists(RuntimePath(temporary.Path, "0.1.0-beta.2")), "An obsolete runtime was retained.");

    Directory.Delete(RuntimePath(temporary.Path, "0.1.0-beta.4"), true);
    RuntimeState recovered = store.LoadAndRepair();
    Assert(recovered.Current == "0.1.0-beta.3" && recovered.Previous == null, "A damaged current runtime did not roll back.");
  }

  private static void TestPackageInstallation()
  {
    using TemporaryDirectory temporary = new();
    byte[] package = CreatePackage("0.2.0", includeResources: true);
    string checksum = Sha256(package);
    string manifest = Manifest("0.2.0", package.Length, checksum);
    using TestServer server = new(manifest, package);
    UpdateManager manager = new(temporary.Path, server.BaseUrl, server.BaseUrl + "releases");
    UpdateResult result = manager.Resolve("0.1.0");
    Assert(result.Outcome == UpdateOutcomes.Candidate, "The package was not selected as a candidate.");
    string helper = Path.Combine(result.RuntimePath, "Helpers", "mac", "helper.app", "Contents", "MacOS", "helper");
    Assert(File.Exists(helper), "The helper was not installed.");
    if (!OperatingSystem.IsWindows())
      Assert((File.GetUnixFileMode(helper) & UnixFileMode.UserExecute) != 0, "The helper executable bit was not restored.");
    Assert(File.Exists(Path.Combine(result.RuntimePath, "Assets", "asset.txt")), "Assets were not installed.");
  }

  private static void TestChecksumMismatch()
  {
    using TemporaryDirectory temporary = new();
    byte[] package = CreatePackage("0.2.0", includeResources: false);
    string manifest = Manifest("0.2.0", package.Length, new string('0', 64));
    using TestServer server = new(manifest, package);
    UpdateManager manager = new(temporary.Path, server.BaseUrl, server.BaseUrl + "releases");
    AssertThrows<InvalidDataException>(() => manager.Resolve("0.1.0"));
  }

  private static void TestPackageSizeMismatch()
  {
    using TemporaryDirectory temporary = new();
    byte[] package = CreatePackage("0.2.0", includeResources: false);
    string manifest = Manifest("0.2.0", package.Length + 1, Sha256(package));
    using TestServer server = new(manifest, package);
    UpdateManager manager = new(temporary.Path, server.BaseUrl, server.BaseUrl + "releases");
    AssertThrows<InvalidDataException>(() => manager.Resolve("0.1.0"));
  }

  private static void TestUnsafeArchive()
  {
    using TemporaryDirectory temporary = new();
    byte[] package = CreatePackage("0.2.0", includeResources: false, includeUnsafeEntry: true);
    string manifest = Manifest("0.2.0", package.Length, Sha256(package));
    using TestServer server = new(manifest, package);
    UpdateManager manager = new(temporary.Path, server.BaseUrl, server.BaseUrl + "releases");
    AssertThrows<InvalidDataException>(() => manager.Resolve("0.1.0"));
    Assert(!File.Exists(Path.Combine(temporary.Path, "escape.txt")), "An unsafe archive entry escaped extraction.");
  }

  private static void TestBetaReleaseSelection()
  {
    using TemporaryDirectory temporary = new();
    File.WriteAllText(Path.Combine(temporary.Path, "UpdateSettings.json"), "{\"ReceiveBetaUpdates\":true}");
    byte[] package = CreatePackage("0.3.0-beta.2", includeResources: false);
    string manifest = Manifest("0.3.0-beta.2", package.Length, Sha256(package));
    using TestServer server = new(manifest, package);
    server.SetReleases(JsonConvert.SerializeObject(new object[]
    {
      Release("v9.0.0", server.BaseUrl, draft: true),
      Release("v0.3.0-beta.2", server.BaseUrl, draft: false),
      Release("v0.3.0-beta.1", server.BaseUrl, draft: false),
    }));
    UpdateManager manager = new(temporary.Path, server.BaseUrl, server.BaseUrl + "releases");
    UpdateResult result = manager.Resolve("0.1.0-beta.3");
    Assert(result.Version == "0.3.0-beta.2", "The highest non-draft beta release was not selected.");
  }

  private static void TestBridgeTransition()
  {
    using TemporaryDirectory temporary = new();
    string adofaiIpc = Path.Combine(temporary.Path, "AdofaiIpc");
    string tufReplay = Path.Combine(temporary.Path, "TUFReplay");
    string assets = Path.Combine(temporary.Path, "TransitionAssets");
    Directory.CreateDirectory(adofaiIpc);
    Directory.CreateDirectory(tufReplay);
    Directory.CreateDirectory(assets);
    File.WriteAllText(Path.Combine(adofaiIpc, "Info.json"),
      "{\"Id\":\"AdofaiIpc\",\"Version\":\"0.2.0\",\"AssemblyName\":\"AdofaiIpc.dll\"}");
    File.WriteAllText(Path.Combine(adofaiIpc, "AdofaiIpc.dll"), "legacy-ipc");
    File.WriteAllText(Path.Combine(tufReplay, "Info.json"),
      "{\"Id\":\"TUFReplay\",\"Version\":\"0.1.0-beta.8\",\"AssemblyName\":\"AdofaiIpc.Bootstrap.dll\"}");
    File.WriteAllText(Path.Combine(tufReplay, "AdofaiIpc.Bootstrap.dll"), "legacy-bootstrap");
    File.WriteAllText(Path.Combine(tufReplay, "AdofaiIpcBootstrap.json"),
      "{\"MinimumAdofaiIpcVersion\":\"0.2.0\",\"DownloadUrl\":\"legacy\"}");
    File.WriteAllText(Path.Combine(assets, "AdofaiIpc.Bootstrap.dll"), "bootstrap-v2");
    File.WriteAllText(Path.Combine(assets, "TUFReplay.DependencyShim.dll"), "dependency-shim");
    File.WriteAllText(Path.Combine(assets, "AdofaiIpcBootstrap.json"),
      "{\"MinimumAdofaiIpcVersion\":\"0.3.0\"}");

    byte[] package = CreateAdofaiIpcPackage("0.3.0");
    string packagePath = Path.Combine(temporary.Path, "AdofaiIpc.zip");
    File.WriteAllBytes(packagePath, package);
    string manifest = JsonConvert.SerializeObject(new
    {
      SchemaVersion = 1,
      Version = "0.3.0",
      PackageUrl = "https://github.com/KGH1113/adofai-ipc/releases/download/v0.3.0/AdofaiIpc.zip",
      PackageSize = package.Length,
      Sha256 = Sha256(package),
    });

    AdofaiIpcTransitionBridge.InstallAdofaiIpcPackageForTests(adofaiIpc, packagePath, manifest);
    Assert(File.Exists(Path.Combine(adofaiIpc, "Runtime", "versions", "0.2.0", "AdofaiIpc.dll")),
      "The legacy AdofaiIpc runtime was not preserved.");
    Assert(File.Exists(Path.Combine(adofaiIpc, "Runtime", "versions", "0.3.0", "AdofaiIpc.dll")),
      "The new AdofaiIpc runtime was not staged.");
    Assert(File.ReadAllText(Path.Combine(adofaiIpc, "Update", "state.json")).Contains("\"Trial\": \"0.3.0\""),
      "AdofaiIpc was not marked for the next launch.");
    Assert(File.ReadAllText(Path.Combine(tufReplay, "Info.json")).Contains("AdofaiIpc.Bootstrap.dll"),
      "TUFReplay was committed before AdofaiIpc.");

    AdofaiIpcTransitionBridge.StageDependencyLauncherForTests(tufReplay, "0.1.0-beta.9", assets);
    Assert(File.Exists(Path.Combine(tufReplay, "Launcher", "versions", "legacy", "AdofaiIpc.Bootstrap.dll")),
      "The legacy dependency bootstrap was not preserved.");
    Assert(File.Exists(Path.Combine(tufReplay, "Launcher", "versions", "legacy", "AdofaiIpcBootstrap.json")),
      "The legacy dependency manifest was not preserved.");
    Assert(File.Exists(Path.Combine(tufReplay, "Launcher", "versions", "2", "AdofaiIpc.Bootstrap.dll")),
      "Dependency bootstrap v2 was not staged.");
    Assert(File.ReadAllText(Path.Combine(tufReplay, "Info.json")).Contains("TUFReplay.DependencyShim.dll"),
      "TUFReplay did not switch to the fixed dependency shim.");
  }

  private static void TestPublishedReleaseBridge()
  {
    const string adofaiIpcManifestUrl =
      "https://github.com/KGH1113/adofai-ipc/releases/download/v0.3.0/AdofaiIpc.update.json";
    const string tufReplayManifestUrl =
      "https://github.com/KGH1113/TUFReplay/releases/download/v0.1.0-beta.9/TUFReplay.update.json";

    using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };
    JObject adofaiIpcManifest = JObject.Parse(client.GetStringAsync(adofaiIpcManifestUrl).GetAwaiter().GetResult());
    byte[] adofaiIpcPackage = client.GetByteArrayAsync((string)adofaiIpcManifest["PackageUrl"]!).GetAwaiter().GetResult();
    Assert((string)adofaiIpcManifest["Version"] == "0.3.0", "The published AdofaiIpc version is not 0.3.0.");
    Assert((long)adofaiIpcManifest["PackageSize"] == adofaiIpcPackage.LongLength,
      "The published AdofaiIpc package size does not match its manifest.");
    Assert(string.Equals((string)adofaiIpcManifest["Sha256"], Sha256(adofaiIpcPackage), StringComparison.OrdinalIgnoreCase),
      "The published AdofaiIpc package hash does not match its manifest.");

    JObject tufReplayManifest = JObject.Parse(client.GetStringAsync(tufReplayManifestUrl).GetAwaiter().GetResult());
    Uri tufReplayPackageUrl = new(new Uri(tufReplayManifestUrl), (string)tufReplayManifest["packageAsset"]!);
    byte[] tufReplayPackage = client.GetByteArrayAsync(tufReplayPackageUrl).GetAwaiter().GetResult();
    Assert((string)tufReplayManifest["version"] == "0.1.0-beta.9", "The published TUFReplay version is not beta.9.");
    Assert((long)tufReplayManifest["packageBytes"] == tufReplayPackage.LongLength,
      "The published TUFReplay package size does not match its manifest.");
    Assert(string.Equals((string)tufReplayManifest["packageSha256"], Sha256(tufReplayPackage), StringComparison.OrdinalIgnoreCase),
      "The published TUFReplay package hash does not match its manifest.");

    using TemporaryDirectory temporary = new();
    string extracted = Path.Combine(temporary.Path, "release");
    string adofaiIpc = Path.Combine(temporary.Path, "Mods", "AdofaiIpc");
    string tufReplay = Path.Combine(temporary.Path, "Mods", "TUFReplay");
    Directory.CreateDirectory(extracted);
    Directory.CreateDirectory(adofaiIpc);
    Directory.CreateDirectory(tufReplay);
    using (MemoryStream archiveBytes = new(tufReplayPackage))
    using (ZipArchive archive = new(archiveBytes, ZipArchiveMode.Read))
      archive.ExtractToDirectory(extracted);

    File.WriteAllText(Path.Combine(adofaiIpc, "Info.json"),
      "{\"Id\":\"AdofaiIpc\",\"Version\":\"0.2.0\",\"AssemblyName\":\"AdofaiIpc.dll\"}");
    File.WriteAllText(Path.Combine(adofaiIpc, "AdofaiIpc.dll"), "legacy-ipc");
    File.WriteAllText(Path.Combine(tufReplay, "Info.json"),
      "{\"Id\":\"TUFReplay\",\"Version\":\"0.1.0-beta.8\",\"AssemblyName\":\"AdofaiIpc.Bootstrap.dll\"}");
    File.WriteAllText(Path.Combine(tufReplay, "AdofaiIpc.Bootstrap.dll"), "legacy-bootstrap");
    File.WriteAllText(Path.Combine(tufReplay, "AdofaiIpcBootstrap.json"),
      "{\"MinimumAdofaiIpcVersion\":\"0.2.0\",\"DownloadUrl\":\"legacy\"}");

    string adofaiIpcPackagePath = Path.Combine(temporary.Path, "AdofaiIpc.zip");
    File.WriteAllBytes(adofaiIpcPackagePath, adofaiIpcPackage);
    AdofaiIpcTransitionBridge.InstallAdofaiIpcPackageForTests(
      adofaiIpc, adofaiIpcPackagePath, adofaiIpcManifest.ToString(Formatting.None));

    string transitionAssets = Path.Combine(extracted, "TUFReplay", "Runtime", "versions", "0.1.0-beta.9", "Transition");
    AdofaiIpcTransitionBridge.StageDependencyLauncherForTests(tufReplay, "0.1.0-beta.9", transitionAssets);

    string adofaiIpcState = File.ReadAllText(Path.Combine(adofaiIpc, "Update", "state.json"));
    string dependencyState = File.ReadAllText(Path.Combine(tufReplay, "Launcher", "state.json"));
    Assert(adofaiIpcState.Contains("\"Current\": \"0.2.0\"") && adofaiIpcState.Contains("\"Trial\": \"0.3.0\""),
      "The published bridge did not retain 0.2.0 for this run and stage 0.3.0 for the next run.");
    Assert(dependencyState.Contains("\"Current\": \"legacy\"") && dependencyState.Contains("\"Trial\": \"2\""),
      "The published bridge did not retain the legacy bootstrap and stage bootstrap v2.");
    Assert(AssemblyName.GetAssemblyName(Path.Combine(adofaiIpc, "AdofaiIpc.Shim.dll")).Name == "AdofaiIpc.Shim",
      "The published fixed AdofaiIpc shim is not a loadable assembly.");
    Assert(AssemblyName.GetAssemblyName(Path.Combine(adofaiIpc, "Launcher", "versions", "0.3.0", "AdofaiIpc.Launcher.dll")).Name == "AdofaiIpc.Launcher",
      "The published versioned AdofaiIpc launcher is not a loadable assembly.");
    Assert(AssemblyName.GetAssemblyName(Path.Combine(tufReplay, "TUFReplay.DependencyShim.dll")).Name == "TUFReplay.DependencyShim",
      "The published fixed TUFReplay dependency shim is not a loadable assembly.");
  }

  private static byte[] CreateAdofaiIpcPackage(string version)
  {
    using MemoryStream buffer = new();
    using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, true))
    {
      AddEntry(archive, "AdofaiIpc/Info.json",
        "{\"Id\":\"AdofaiIpc\",\"Version\":\"" + version + "\",\"AssemblyName\":\"AdofaiIpc.Shim.dll\"}");
      AddEntry(archive, "AdofaiIpc/AdofaiIpc.Shim.dll", "shim");
      AddEntry(archive, "AdofaiIpc/AdofaiIpc.Bootstrap.dll", "bootstrap");
      AddEntry(archive, "AdofaiIpc/Launcher/versions/" + version + "/AdofaiIpc.Launcher.dll", "launcher");
      AddEntry(archive, "AdofaiIpc/Runtime/versions/" + version + "/AdofaiIpc.dll", "runtime");
    }
    return buffer.ToArray();
  }

  private static object Release(string tag, string baseUrl, bool draft)
  {
    return new
    {
      tag_name = tag,
      draft,
      assets = new object[]
      {
        new { name = "TUFReplay.update.json", browser_download_url = baseUrl + "TUFReplay.update.json" },
        new { name = "TUFReplay.zip", browser_download_url = baseUrl + "TUFReplay.zip" },
      },
    };
  }

  private static byte[] CreatePackage(string version, bool includeResources, bool includeUnsafeEntry = false)
  {
    using MemoryStream buffer = new();
    using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, true))
    {
      string root = $"TUFReplay/Runtime/versions/{version}/";
      AddEntry(archive, root + "TUFReplay.dll", "payload");
      AddEntry(archive, root + "TUFReplay.UpdateEngine.dll", "engine");
      AddEntry(archive, root + "Info.json", $"{{\"Version\":\"{version}\"}}");
      if (includeResources)
      {
        AddEntry(archive, root + "Helpers/mac/helper.app/Contents/MacOS/helper", "helper", executable: true);
        AddEntry(archive, root + "Assets/asset.txt", "asset");
      }
      if (includeUnsafeEntry)
        AddEntry(archive, "../escape.txt", "unsafe");
    }
    return buffer.ToArray();
  }

  private static void AddEntry(ZipArchive archive, string path, string content, bool executable = false)
  {
    ZipArchiveEntry entry = archive.CreateEntry(path);
    if (executable)
      entry.ExternalAttributes = Convert.ToInt32("100755", 8) << 16;
    using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
    writer.Write(content);
  }

  private static string Manifest(string version, long bytes, string checksum)
  {
    return JsonConvert.SerializeObject(new
    {
      schemaVersion = 1,
      version,
      packageAsset = "TUFReplay.zip",
      packageBytes = bytes,
      packageSha256 = checksum,
      runtimePath = $"TUFReplay/Runtime/versions/{version}",
    });
  }

  private static void CreateRuntime(string installPath, string version)
  {
    string path = RuntimePath(installPath, version);
    Directory.CreateDirectory(path);
    File.WriteAllText(Path.Combine(path, "TUFReplay.dll"), "payload");
    File.WriteAllText(Path.Combine(path, "TUFReplay.UpdateEngine.dll"), "engine");
    File.WriteAllText(Path.Combine(path, "Info.json"), $"{{\"Version\":\"{version}\"}}");
  }

  private static string RuntimePath(string installPath, string version) =>
    Path.Combine(installPath, "Runtime", "versions", version);

  private static string Sha256(byte[] bytes) =>
    string.Concat(SHA256.HashData(bytes).Select(value => value.ToString("x2")));

  private static void Run(string name, Action test)
  {
    test();
    _passed++;
    Console.WriteLine("PASS " + name);
  }

  private static void Assert(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static void AssertThrows<T>(Action action) where T : Exception
  {
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
  }

  private sealed class TemporaryDirectory : IDisposable
  {
    public TemporaryDirectory()
    {
      Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tufreplay-update-test-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Path);
    }
    public string Path { get; }
    public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
  }

  private sealed class TestServer : IDisposable
  {
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private readonly byte[] _manifest;
    private readonly byte[] _package;
    private byte[] _releases = Encoding.UTF8.GetBytes("[]");

    public TestServer(string manifest, byte[] package)
    {
      int port = FreePort();
      BaseUrl = $"http://127.0.0.1:{port}/";
      _manifest = Encoding.UTF8.GetBytes(manifest);
      _package = package;
      _listener.Prefixes.Add(BaseUrl);
      _listener.Start();
      _worker = Task.Run(Serve);
    }

    public string BaseUrl { get; }

    public void SetReleases(string releases)
    {
      _releases = Encoding.UTF8.GetBytes(releases);
    }

    private async Task Serve()
    {
      while (!_cancellation.IsCancellationRequested)
      {
        HttpListenerContext context;
        try { context = await _listener.GetContextAsync(); }
        catch when (_cancellation.IsCancellationRequested) { return; }
        string path = context.Request.Url?.AbsolutePath ?? string.Empty;
        byte[] body = path.EndsWith("TUFReplay.zip")
          ? _package
          : path.EndsWith("releases") ? _releases : _manifest;
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
      }
    }

    public void Dispose()
    {
      _cancellation.Cancel();
      _listener.Stop();
      try { _worker.Wait(TimeSpan.FromSeconds(1)); } catch { }
      _listener.Close();
      _cancellation.Dispose();
    }

    private static int FreePort()
    {
      var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      int port = ((IPEndPoint)listener.LocalEndpoint).Port;
      listener.Stop();
      return port;
    }
  }
}
