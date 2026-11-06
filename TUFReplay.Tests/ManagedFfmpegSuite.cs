using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using TUFReplay.Shared.Media;

internal static class ManagedFfmpegSuite
{
  public static void RunAll(string root) => RunAsync(root).GetAwaiter().GetResult();

  private static async Task RunAsync(string root)
  {
    const string asset = "ffmpeg-n9.0-latest-win64-gpl-9.0.zip";
    const string url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/" + asset;
    string hash = new('a', 64);
    string manifest = new string('b', 64) + "  other.zip\n" + hash + "  " + asset + "\n";
    Check(ManagedFfmpegInstaller.ParseChecksum(manifest, url) == hash, "checksum selects exact archive");
    Check(
      ManagedFfmpegInstaller.ParseChecksum(manifest.Replace("  " + asset, " *" + asset), url) == hash,
      "binary checksum format supported"
    );
    Check(ManagedFfmpegInstaller.ParseChecksum(hash, url) == hash, "single-file checksums remain supported");
    await MustFail(
      () => Task.FromResult(ManagedFfmpegInstaller.ParseChecksum(manifest.Replace(asset, "missing.zip"), url)),
      "missing archive checksum"
    );
    await MustFail(
      () => Task.FromResult(ManagedFfmpegInstaller.ParseChecksum(manifest.Replace(hash, "invalid"), url)),
      "invalid archive checksum"
    );
    if (OperatingSystem.IsWindows())
      Check(FfmpegPlatform.Current().Url == url, "Windows downloads the BtbN release build");
    await CheckInstallGateAsync(root);
    await CheckSharedInstallationAsync(root);
    await CheckDirectoryMoveAsync(root);
    await CheckConcurrentInstallAsync(root);
    Check(
      FfmpegPlatform.ResolveGameDirectory(Path.Combine(root, "Game.app", "Contents"), true) == Path.GetFullPath(root),
      "macOS Contents resolves outside the app bundle"
    );
    Check(
      FfmpegPlatform.ResolveGameDirectory(Path.Combine(root, "Game.app", "Contents", "Resources", "Data"), true)
        == Path.GetFullPath(root),
      "macOS Resources/Data resolves outside the app bundle"
    );
    if (OperatingSystem.IsWindows())
      return; // Shell fixture; Windows archive handling is exercised below.
    string directory = Path.Combine(root, "managed ffmpeg mod");
    byte[] archive = Zip(
      ("vendor/bin/ffmpeg", "#!/bin/sh\necho 'ffmpeg version fixture'\n"),
      ("../../outside.txt", "should not escape"),
      ("vendor/LICENSE.txt", "fixture vendor notice")
    );
    var platform = FixturePlatform("fixture-macos", "ffmpeg");
    int requests = 0;
    Func<HttpMessageHandler> transport = () =>
      new FixtureHandler(
        (request, token) =>
        {
          if (request.RequestUri.AbsolutePath != "/checksum")
            Interlocked.Increment(ref requests);
          return Task.FromResult(VerifiedResponse(request, archive));
        }
      );
    using (var installer = new ManagedFfmpegInstaller(directory, platform, transport))
    {
      await installer.Work;
      Check(installer.Snapshot().Status == "missing", "missing installation");
      installer.Request();
      installer.Request();
      Check(
        installer.Snapshot().Status == "awaiting-consent" && requests == 0,
        "no download without consent and shared request"
      );
      installer.Decline();
      Check(installer.Snapshot().Status == "declined" && requests == 0, "decline without network");
      installer.Request();
      installer.Confirm();
      installer.Confirm();
      await installer.Work;
      Check(installer.Snapshot().Available && requests == 1, "one download for concurrent approval");
      string path = installer.Snapshot().Path;
      Check(
        path.StartsWith(Path.Combine(directory, "ffmpeg", platform.Name)),
        "installation belongs to the shared game directory"
      );
      Check(File.ReadAllText(path).Contains("fixture"), "extracted executable");
      Check(!File.Exists(Path.Combine(root, "outside.txt")), "archive cannot escape installation");
      Check(File.Exists(Path.Combine(Path.GetDirectoryName(path), "vendor-notice-1.txt")), "vendor notice retained");
    }
    using (var reused = new ManagedFfmpegInstaller(directory, platform, transport))
    {
      await reused.Work;
      Check(reused.Snapshot().Available && requests == 1, "verified installation reused without another download");
      File.AppendAllText(reused.Snapshot().Path, "# tampered\n");
    }
    using (var changed = new ManagedFfmpegInstaller(directory, platform, transport))
    {
      await changed.Work;
      Check(changed.Snapshot().Status == "missing", "changed executable must be reinstalled");
    }
    string legacyGame = Path.Combine(root, "legacy game");
    string legacyDirectory = Path.Combine(legacyGame, "Mods", "OrbitRender", "FFmpeg", platform.Name);
    Directory.CreateDirectory(legacyDirectory);
    string legacyExecutable = Path.Combine(legacyDirectory, "ffmpeg");
    File.WriteAllText(legacyExecutable, "#!/bin/sh\necho 'ffmpeg version legacy fixture'\n");
    File.SetUnixFileMode(legacyExecutable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.WriteAllText(Path.Combine(legacyDirectory, "LICENSE.txt"), "legacy vendor license");
    using (var migrated = new ManagedFfmpegInstaller(legacyGame, platform, transport))
    {
      await migrated.Work;
      Check(migrated.Snapshot().Status == "missing", "other mods' binaries are not adopted");
      Check(requests == 1, "startup stays offline");
      Check(File.Exists(legacyExecutable), "legacy source installation is not deleted");
      Check(
        !Directory.Exists(Path.Combine(legacyGame, "ffmpeg", platform.Name)),
        "startup never copies a legacy installation"
      );
    }
    foreach (
      string candidate in new[]
      {
        Path.Combine(legacyGame, "Mods", "TUFReplay", "FFmpeg", platform.Name, "ffmpeg"),
        Path.Combine(legacyGame, "ffmpeg", "ffmpeg"),
        Path.Combine(legacyGame, "ffmpeg", platform.Name, "ffmpeg"),
      }
    )
    {
      Directory.CreateDirectory(Path.GetDirectoryName(candidate));
      File.Copy(legacyExecutable, candidate);
      using var untrusted = new ManagedFfmpegInstaller(legacyGame, platform, transport);
      await untrusted.Work;
      Check(untrusted.Snapshot().Status == "missing", "receiptless binaries are not re-blessed: " + candidate);
      Check(!File.Exists(Path.Combine(Path.GetDirectoryName(candidate), "installation.json")), "no receipt minted");
    }
    var waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using (
      var cancelled = new ManagedFfmpegInstaller(
        Path.Combine(root, "cancelled install"),
        platform,
        () =>
          new FixtureHandler(
            async (request, token) =>
            {
              waiting.TrySetResult(true);
              await Task.Delay(Timeout.Infinite, token);
              return Response(request, archive);
            }
          )
      )
    )
    {
      await cancelled.Work;
      cancelled.Request();
      cancelled.Confirm();
      await waiting.Task;
      cancelled.Cancel();
      await cancelled.Work;
      Check(cancelled.Snapshot().Status == "cancelled", "download cancellation finishes");
      Check(
        !Directory.Exists(Path.Combine(root, "cancelled install", "ffmpeg", platform.Name)),
        "cancel cleans partial files"
      );
    }
    var checksumPlatform = new FfmpegPlatform(
      "fixture-checksum",
      "ffmpeg",
      platform.Url,
      checksumUrl: "https://fixture.invalid/checksum"
    );
    using (
      var mismatch = new ManagedFfmpegInstaller(
        Path.Combine(root, "checksum install"),
        checksumPlatform,
        () =>
          new FixtureHandler(
            (request, token) =>
              Task.FromResult(
                Response(
                  request,
                  request.RequestUri.AbsolutePath == "/checksum" ? Encoding.UTF8.GetBytes(new string('0', 64)) : archive
                )
              )
          )
      )
    )
    {
      await mismatch.Work;
      mismatch.Request();
      mismatch.Confirm();
      await mismatch.Work;
      Check(
        mismatch.Snapshot().Status == "failed" && mismatch.Snapshot().Error.Contains("checksum"),
        "provider checksum mismatch rejected"
      );
    }
    using (
      var bad = new ManagedFfmpegInstaller(
        Path.Combine(root, "failed install"),
        platform,
        () =>
          new FixtureHandler(
            (request, token) => Task.FromResult(VerifiedResponse(request, Encoding.UTF8.GetBytes("not a ZIP")))
          )
      )
    )
    {
      await bad.Work;
      bad.Request();
      bad.Confirm();
      await bad.Work;
      Check(bad.Snapshot().Status == "failed", "invalid download fails without installing");
      bad.Request();
      Check(bad.Snapshot().Status == "awaiting-consent", "retry asks for consent");
    }
    using (
      var noChecksum = new ManagedFfmpegInstaller(
        Path.Combine(root, "unchecked provider"),
        new FfmpegPlatform("unchecked-fixture", "ffmpeg", platform.Url),
        transport
      )
    )
    {
      await noChecksum.Work;
      noChecksum.Request();
      noChecksum.Confirm();
      await noChecksum.Work;
      Check(
        noChecksum.Snapshot().Status == "failed" && noChecksum.Snapshot().Error.Contains("checksum"),
        "providers without checksums fail before downloading or executing"
      );
      Check(requests == 1, "unchecked provider never contacts the network");
    }
    string duplicate = Path.Combine(root, "duplicate.zip");
    File.WriteAllBytes(duplicate, Zip(("a/ffmpeg.exe", "one"), ("b/ffmpeg.exe", "two")));
    await MustFail(
      () => ManagedFfmpegInstaller.ExtractZipAsync(duplicate, root, "ffmpeg.exe", CancellationToken.None),
      "ambiguous Windows executable"
    );
    using var input = new MemoryStream(new byte[20]);
    using var output = new MemoryStream();
    await MustFail(
      () => ManagedFfmpegInstaller.CopyBoundedAsync(input, output, 10, CancellationToken.None),
      "download size bound"
    );
    Console.WriteLine(
      "PASS: FFmpeg consent, shared install, cancellation cleanup, checksums, archive isolation, verified reuse and retry."
    );
  }

  private static async Task CheckInstallGateAsync(string root)
  {
    string game = Path.Combine(root, "lock regression");
    using var installer = new ManagedFfmpegInstaller(
      game,
      new FfmpegPlatform("lock-fixture", "ffmpeg", "https://fixture.invalid/ffmpeg.zip")
    );
    await installer.Work;
    var acquire = typeof(ManagedFfmpegInstaller).GetMethod(
      "AcquireInstallGateAsync",
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
    );
    async Task<IDisposable> Acquire(TimeSpan timeout, CancellationToken token = default)
    {
      var task = (Task)acquire.Invoke(installer, new object[] { timeout, token });
      await task.WaitAsync(TimeSpan.FromSeconds(10));
      return (IDisposable)task.GetType().GetProperty("Result").GetValue(task);
    }

    using (var first = await Acquire(TimeSpan.FromSeconds(2)))
    {
      Check(first != null, "first lock acquired");
      Check(await Acquire(TimeSpan.FromMilliseconds(100)) == null, "file lock contention expires");
      using var cancel = new CancellationTokenSource();
      var pending = Acquire(TimeSpan.FromSeconds(10), cancel.Token);
      cancel.Cancel();
      try
      {
        using var unexpected = await pending;
        throw new Exception("Expected lock cancellation");
      }
      catch (OperationCanceledException) { }
      var initializing = new ManagedFfmpegInstaller(
        game,
        new FfmpegPlatform("lock-fixture", "ffmpeg", "https://fixture.invalid/ffmpeg.zip")
      );
      int notifications = 0;
      initializing.Changed += () => Interlocked.Increment(ref notifications);
      initializing.Dispose();
      await initializing.Work.WaitAsync(TimeSpan.FromSeconds(2));
      Check(notifications == 0, "disposing cancels initialization without later notifications");
      await Task.Yield();
      int acquiredThread = Environment.CurrentManagedThreadId;
      var released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      new Thread(() =>
      {
        try
        {
          Check(Environment.CurrentManagedThreadId != acquiredThread, "release uses a different thread");
          first.Dispose();
          released.SetResult(true);
        }
        catch (Exception error)
        {
          released.SetException(error);
        }
      }).Start();
      await released.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
    using (var second = await Acquire(TimeSpan.FromSeconds(2)))
      Check(second != null, "another thread reacquires after release");
    using (
      var file = new FileStream(
        Path.Combine(game, "ffmpeg", ".install.lock"),
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None
      )
    )
      Check(await Acquire(TimeSpan.FromMilliseconds(100)) == null, "file contention expires");
    using (var final = await Acquire(TimeSpan.FromSeconds(2)))
      Check(final != null, "lock reacquired after file timeout");
    Console.WriteLine("PASS: FFmpeg cross-thread lock release, contention timeout and cancellation.");
  }

  private static async Task CheckSharedInstallationAsync(string root)
  {
    string game = Path.Combine(root, "external installation");
    var platform = new FfmpegPlatform("shared-fixture", "encoder", "https://fixture.invalid/ffmpeg.zip");
    using var observer = new ManagedFfmpegInstaller(game, platform);
    await observer.Work;
    observer.Request();
    string executable = Path.Combine(game, ManagedFfmpegInstaller.SharedFolderName, platform.Name, platform.Executable);
    var coordinator = typeof(ManagedFfmpegInstaller).Assembly.GetType(
      "TUFReplay.Shared.Media.FfmpegInstallCoordinator"
    );
    var field = coordinator.GetField(
      "_installer",
      System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
    );
    var previous = field.GetValue(null);
    field.SetValue(null, observer);
    try
    {
      TUFReplay.FfmpegApi.NotifyInstallationCompleted("wrong-platform", executable);
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, null);
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, "\0");
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(
        platform.Name,
        Path.Combine(root, "other game", platform.Executable)
      );
      Check(observer.Snapshot().Status == "awaiting-consent", "unrelated installation notifications are ignored");
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, executable);
      await observer.Work;
      Check(observer.Snapshot().Status == "awaiting-consent", "notification cannot trust a missing executable");
      Directory.CreateDirectory(Path.GetDirectoryName(executable));
      File.WriteAllText(executable, "verified fixture bytes");
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, executable);
      await observer.Work;
      Check(!observer.Snapshot().Available, "notification cannot trust a receiptless executable");
      var receipt = new FfmpegInstallReceipt
      {
        SchemaVersion = 1,
        Platform = platform.Name,
        Executable = platform.Executable,
        Sha256 = new string('0', 64),
        Version = "ffmpeg version fixture",
      };
      string receiptPath = Path.Combine(Path.GetDirectoryName(executable), "installation.json");
      File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt));
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, executable);
      await observer.Work;
      Check(!observer.Snapshot().Available, "notification rejects a checksum mismatch");
      receipt.Sha256 = FileHash(executable);
      File.WriteAllText(receiptPath, JsonConvert.SerializeObject(receipt));
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, executable);
      await observer.Work;
      Check(
        observer.Snapshot().Available && observer.Snapshot().Path == executable,
        "the C# completion API verifies the file before completing an open consent prompt"
      );
      int changes = 0;
      observer.Changed += () => Interlocked.Increment(ref changes);
      TUFReplay.FfmpegApi.NotifyInstallationCompleted(platform.Name, executable);
      Check(changes == 0, "duplicate notifications do not publish additional events");
    }
    finally
    {
      field.SetValue(null, previous);
    }
    Console.WriteLine("PASS: C# API completion verifies receipts and checksums in the background without polling.");
  }

  private static async Task CheckDirectoryMoveAsync(string root)
  {
    var fixture = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (!OperatingSystem.IsWindows())
    {
      fixture = Path.Combine(root, "fixture-encoder");
      File.WriteAllText(fixture, "#!/bin/sh\necho 'ffmpeg version publication fixture'\n");
      File.SetUnixFileMode(fixture, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    if (string.IsNullOrEmpty(fixture))
      return;
    string archive = Path.Combine(root, "download-fixture.zip");
    using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
    {
      zip.CreateEntryFromFile(fixture, "vendor/encoder.exe", CompressionLevel.Fastest);
      using var writer = new StreamWriter(zip.CreateEntry("vendor/LICENSE.txt").Open());
      writer.Write("fixture provider license");
    }
    int downloads = 0;
    using (
      var downloaded = new ManagedFfmpegInstaller(
        Path.Combine(root, "download regression"),
        FixturePlatform("fixture-platform", "encoder.exe"),
        () =>
          new FixtureHandler(
            (request, _) =>
            {
              if (request.RequestUri.AbsolutePath == "/checksum")
                return Task.FromResult(Response(request, Encoding.UTF8.GetBytes(FileHash(archive))));
              Interlocked.Increment(ref downloads);
              return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                  RequestMessage = request,
                  Content = new StreamContent(File.OpenRead(archive)),
                }
              );
            }
          )
      )
    )
    {
      await downloaded.Work;
      downloaded.Request();
      Check(downloads == 0, "Windows download waits for consent");
      downloaded.Confirm();
      downloaded.Confirm();
      await downloaded.Work;
      Check(downloaded.Snapshot().Available && downloads == 1, "Windows archive downloads and installs once");
      Check(downloaded.Snapshot().DownloadedBytes > 0, "Windows download reports byte progress");
      using var reused = new ManagedFfmpegInstaller(
        Path.Combine(root, "download regression"),
        new FfmpegPlatform("fixture-platform", "encoder.exe", "https://fixture.invalid/ffmpeg.zip")
      );
      await reused.Work;
      Check(reused.Snapshot().Available, "completed installation is reused without download or process execution");
    }
    string game = Path.Combine(root, "publication regression");
    var platform = new FfmpegPlatform("fixture-platform", "encoder.exe", "https://fixture.invalid/ffmpeg.zip");
    using var installer = new ManagedFfmpegInstaller(game, platform);
    await installer.Work;
    bool notified = false;
    installer.Changed += () =>
    {
      if (!Task.Run(installer.Snapshot).Wait(TimeSpan.FromSeconds(2)))
        throw new Exception("State notification was raised while holding the state lock");
      notified = true;
    };
    installer.Request();
    Check(notified, "state callbacks run outside the state lock");
    string staging = Path.Combine(game, "incoming");
    string destination = Path.Combine(game, "ffmpeg", platform.Name);
    Directory.CreateDirectory(staging);
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(destination, "previous.txt"), "old installation");
    File.Copy(fixture, Path.Combine(staging, platform.Executable));
    var publish = typeof(ManagedFfmpegInstaller).GetMethod(
      "PromoteStaging",
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
    );
    var executable = (string)
      publish.Invoke(installer, new object[] { staging, FileHash(fixture), CancellationToken.None });
    Check(
      File.Exists(executable) && File.Exists(Path.Combine(destination, ManagedFfmpegInstaller.ReceiptFileName)),
      "final-path verification writes completion record"
    );
    Check(!Directory.Exists(staging), "staging is moved before execution verification");
    Check(
      Directory.GetDirectories(Path.Combine(game, "ffmpeg"), "*.previous-*").Length == 0,
      "successful publication removes old backup"
    );
    var inspect = typeof(ManagedFfmpegInstaller).GetMethod(
      "ReadInstalled",
      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
    );
    using (var busy = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.None))
      Check(
        (string)inspect.Invoke(installer, null) == null,
        "an unreadable executable cannot bypass checksum verification"
      );
    Directory.CreateDirectory(staging);
    File.WriteAllText(Path.Combine(staging, platform.Executable), "invalid executable");
    try
    {
      publish.Invoke(installer, new object[] { staging, FileHash(fixture), CancellationToken.None });
      throw new Exception("Expected invalid executable rejection");
    }
    catch (System.Reflection.TargetInvocationException) { }
    Check(
      File.Exists(executable) && File.Exists(Path.Combine(destination, ManagedFfmpegInstaller.ReceiptFileName)),
      "failed verification rolls back previous installation"
    );
    notified = false;
    installer.Dispose();
    await installer.Work;
    Check(!notified, "disposal emits no state notifications");
    Console.WriteLine("PASS: final-path checksum verification, backup cleanup, rollback and callbacks outside locks.");
  }

  private static HttpResponseMessage Response(HttpRequestMessage request, byte[] content) =>
    new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(content) };

  private static FfmpegPlatform FixturePlatform(string name, string executable) =>
    new(name, executable, "https://fixture.invalid/ffmpeg.zip", checksumUrl: "https://fixture.invalid/checksum");

  private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

  private static HttpResponseMessage VerifiedResponse(HttpRequestMessage request, byte[] archive) =>
    Response(
      request,
      request.RequestUri.AbsolutePath == "/checksum"
        ? Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(archive)))
        : archive
    );

  private static async Task CheckConcurrentInstallAsync(string root)
  {
    if (OperatingSystem.IsWindows())
      return;
    string game = Path.Combine(root, "concurrent install");
    var platform = FixturePlatform("concurrent-fixture", "ffmpeg");
    byte[] archive = Zip(("ffmpeg", "#!/bin/sh\necho 'ffmpeg version concurrent fixture'\n"));
    var downloading = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var proceed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    int downloads = 0;
    Func<HttpMessageHandler> transport = () =>
      new FixtureHandler(
        async (request, token) =>
        {
          await Task.Yield();
          if (request.RequestUri.AbsolutePath != "/checksum")
          {
            Interlocked.Increment(ref downloads);
            downloading.TrySetResult(true);
            await proceed.Task.WaitAsync(token).ConfigureAwait(false);
          }
          return VerifiedResponse(request, archive);
        }
      );
    using var first = new ManagedFfmpegInstaller(game, platform, transport);
    using var second = new ManagedFfmpegInstaller(game, platform, transport);
    await Task.WhenAll(first.Work, second.Work);
    first.Request();
    first.Confirm();
    await downloading.Task.WaitAsync(TimeSpan.FromSeconds(5));
    second.Request();
    second.Confirm();
    proceed.SetResult(true);
    await Task.WhenAll(first.Work, second.Work).WaitAsync(TimeSpan.FromSeconds(10));
    Check(
      first.Snapshot().Available && second.Snapshot().Available && downloads == 1,
      "concurrent installers reuse one verified download across asynchronous continuations"
    );
  }

  private static byte[] Zip(params (string name, string content)[] files)
  {
    using var stream = new MemoryStream();
    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
      foreach (var file in files)
      {
        using var writer = new StreamWriter(zip.CreateEntry(file.name).Open());
        writer.Write(file.content);
      }
    return stream.ToArray();
  }

  private static void Check(bool ok, string name)
  {
    if (!ok)
      throw new Exception("FFmpeg installer: " + name);
  }

  private static async Task MustFail(Func<Task> action, string name)
  {
    try
    {
      await action();
    }
    catch (InvalidDataException)
    {
      return;
    }
    throw new Exception("Expected rejection: " + name);
  }

  private sealed class FixtureHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle;

    public FixtureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
    {
      handle = action;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
      handle(request, token);
  }
}
