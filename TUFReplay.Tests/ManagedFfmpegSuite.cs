using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using TUFReplay.Shared.Media;

internal static class ManagedFfmpegSuite
{
  public static void RunAll(string root) => RunAsync(root).GetAwaiter().GetResult();

  private static async Task RunAsync(string root)
  {
    if (OperatingSystem.IsWindows())
      return; // Shell fixture; Windows archive handling is exercised below.
    string directory = Path.Combine(root, "managed ffmpeg mod");
    byte[] archive = Zip(
      ("vendor/bin/ffmpeg", "#!/bin/sh\necho 'ffmpeg version fixture'\n"),
      ("../../outside.txt", "should not escape"),
      ("vendor/LICENSE.txt", "fixture vendor notice")
    );
    var platform = new FfmpegPlatform("fixture-macos", "ffmpeg", "https://fixture.invalid/ffmpeg.zip");
    int requests = 0;
    Func<HttpMessageHandler> transport = () =>
      new FixtureHandler(
        (request, token) =>
        {
          Interlocked.Increment(ref requests);
          return Task.FromResult(Response(request, archive));
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
      Check(path.StartsWith(Path.Combine(directory, "FFmpeg")), "installation belongs to TUFReplay root");
      Check(File.ReadAllText(path).Contains("fixture"), "extracted executable");
      Check(!File.Exists(Path.Combine(root, "outside.txt")), "archive cannot escape installation");
      Check(File.Exists(Path.Combine(Path.GetDirectoryName(path), "vendor-notice-1.txt")), "vendor notice retained");
    }
    using (var reused = new ManagedFfmpegInstaller(directory, platform, transport))
    {
      await reused.Work;
      Check(reused.Snapshot().Available && requests == 1, "verified installation reused without another download");
      File.AppendAllText(reused.Snapshot().Path, "# changed\n");
    }
    using (var changed = new ManagedFfmpegInstaller(directory, platform, transport))
    {
      await changed.Work;
      Check(changed.Snapshot().Status == "missing", "changed binary must be reinstalled");
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
        !Directory.EnumerateDirectories(Path.Combine(root, "cancelled install", "FFmpeg")).Any(),
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
            (request, token) => Task.FromResult(Response(request, Encoding.UTF8.GetBytes("not a ZIP")))
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

  private static HttpResponseMessage Response(HttpRequestMessage request, byte[] content) =>
    new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(content) };

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
