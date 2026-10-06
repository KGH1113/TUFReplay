using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TUFReplay.Shared.Media;

// Owns only TUFReplay's downloaded executable. Never consults PATH, user settings,
// another mod's FFmpeg, or the versioned runtime payload directory.
public sealed class ManagedFfmpegInstaller : IDisposable
{
  public const long MaximumBytes = 256L * 1024 * 1024;
  public event Action Changed;
  private long _nextProgressNotification;

  private void NotifyChanged(bool progress = false)
  {
    if (progress)
    {
      long now = System.Diagnostics.Stopwatch.GetTimestamp();
      lock (_gate)
      {
        if (now < _nextProgressNotification)
          return;
        _nextProgressNotification = now + System.Diagnostics.Stopwatch.Frequency / 10;
      }
    }
    Changed?.Invoke();
  }

  private readonly object _gate = new object();
  private readonly string _directory;
  private readonly FfmpegPlatform _platform;
  private readonly Func<HttpMessageHandler> _transport;
  private CancellationTokenSource _cancel;
  private bool _disposed;
  private FfmpegInstallState _state;
  public Task Work { get; private set; }

  public ManagedFfmpegInstaller(string modDirectory, FfmpegPlatform platform, Func<HttpMessageHandler> transport = null)
  {
    _platform = platform;
    _transport = transport;
    _directory = Path.Combine(modDirectory, "FFmpeg", platform.Name);
    _state = new FfmpegInstallState
    {
      Status = "checking",
      Platform = platform.Name,
      Source = platform.Url,
      InstallationDirectory = _directory,
    };
    Work = Task.Run(() =>
    {
      try
      {
        string path = Path.Combine(_directory, platform.Executable);
        string receipt = Path.Combine(_directory, "installation.json");
        bool valid = File.Exists(receipt) && new FileInfo(receipt).Length < 65536 && File.Exists(path);
        if (valid)
        {
          var installed = JsonConvert.DeserializeObject<FfmpegInstallReceipt>(File.ReadAllText(receipt));
          valid =
            installed?.SchemaVersion == 1 && installed.Platform == platform.Name && installed.Sha256 == Hash(path);
          if (valid)
            VerifyExecutable(path, CancellationToken.None);
        }
        Publish(valid ? "ready" : "missing", valid ? path : null);
      }
      catch
      {
        Publish("missing");
      }
    });
  }

  public FfmpegInstallState Snapshot()
  {
    lock (_gate)
      return _state.Clone();
  }

  public FfmpegInstallState Request()
  {
    lock (_gate)
    {
      if (_disposed)
        throw new ObjectDisposedException(nameof(ManagedFfmpegInstaller));
      if (
        _state.Status == "missing"
        || _state.Status == "declined"
        || _state.Status == "cancelled"
        || _state.Status == "failed"
      )
      {
        _state.Status = "awaiting-consent";
        _state.Error = null;
      }
      NotifyChanged();
      return _state.Clone();
    }
  }

  public void Decline()
  {
    lock (_gate)
      if (_state.Status == "awaiting-consent")
        _state.Status = "declined";
    NotifyChanged();
  }

  public void Cancel()
  {
    lock (_gate)
    {
      _cancel?.Cancel();
      if (_state.Status == "awaiting-consent")
        _state.Status = "cancelled";
    }
    NotifyChanged();
  }

  public void Confirm()
  {
    lock (_gate)
    {
      if (_disposed || _state.Status != "awaiting-consent")
        return;
      _cancel?.Dispose();
      _cancel = new CancellationTokenSource();
      _state.Status = "downloading";
      _state.Error = null;
      _state.DownloadedBytes = 0;
      _state.TotalBytes = null;
      CancellationToken token = _cancel.Token;
      Work = Task.Run(() => InstallAsync(token));
    }
    NotifyChanged();
  }

  private async Task InstallAsync(CancellationToken token)
  {
    string temporary = null;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(_directory));
      temporary = _directory + ".install-" + Guid.NewGuid().ToString("N");
      Directory.CreateDirectory(temporary);
      string archive = Path.Combine(temporary, _platform.TarXz ? "download.tar.xz" : "download.zip");
      using (var client = _transport == null ? new HttpClient() : new HttpClient(_transport()))
      {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-FFmpeg-Installer/1.0");
        string expected = null;
        if (_platform.ChecksumUrl != null)
        {
          using var checksum = await client
            .GetAsync(_platform.ChecksumUrl, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
          checksum.EnsureSuccessStatusCode();
          using var body = await checksum.Content.ReadAsStreamAsync().ConfigureAwait(false);
          using var buffer = new MemoryStream();
          await CopyBoundedAsync(body, buffer, 4096, token).ConfigureAwait(false);
          expected = Encoding.UTF8.GetString(buffer.ToArray()).Trim().Split(' ', '\t', '\r', '\n')[0];
          if (expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("The download provider returned an invalid checksum. Try installing again.");
        }
        using var response = await client
          .GetAsync(_platform.Url, HttpCompletionOption.ResponseHeadersRead, token)
          .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage.RequestUri.Scheme != "https")
          throw new IOException("The download provider redirected to an insecure address. Try again later.");
        long? total = response.Content.Headers.ContentLength;
        if (total > MaximumBytes)
          throw new InvalidDataException("The FFmpeg download is larger than expected. Try again later.");
        lock (_gate)
          _state.TotalBytes = total;
        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var output = File.Create(archive))
          await CopyBoundedAsync(
              input,
              output,
              MaximumBytes,
              token,
              bytes =>
              {
                lock (_gate)
                  _state.DownloadedBytes = bytes;
                NotifyChanged(progress: true);
              }
            )
            .ConfigureAwait(false);
        if (expected != null && !string.Equals(Hash(archive), expected, StringComparison.OrdinalIgnoreCase))
          throw new InvalidDataException(
            "The FFmpeg download did not match the provider's checksum. Try installing again."
          );
      }
      Publish("extracting");
      string executable = Path.Combine(temporary, _platform.Executable);
      if (_platform.TarXz)
        await ExtractTarAsync(archive, executable, token).ConfigureAwait(false);
      else
        await ExtractZipAsync(archive, temporary, _platform.Executable, token).ConfigureAwait(false);
      if (_platform.Executable != "ffmpeg.exe")
        RunProcess("/bin/chmod", "755 " + Quote(executable), token);
      Publish("validating");
      string version = VerifyExecutable(executable, token);
      var receipt = new FfmpegInstallReceipt
      {
        SchemaVersion = 1,
        Platform = _platform.Name,
        Source = _platform.Url,
        Sha256 = Hash(executable),
        Version = version,
        InstalledAtUtc = DateTime.UtcNow,
      };
      File.WriteAllText(
        Path.Combine(temporary, "installation.json"),
        JsonConvert.SerializeObject(receipt, Formatting.Indented)
      );
      File.WriteAllText(
        Path.Combine(temporary, "DOWNLOAD-SOURCE.txt"),
        "Downloaded directly from: "
          + _platform.Url
          + "\nUpstream: https://ffmpeg.org/\n"
          + "This runtime download is excluded from TUFReplay release ZIPs.\n",
        new UTF8Encoding(false)
      );
      File.Delete(archive);
      token.ThrowIfCancellationRequested();
      // Publish a complete installation with its receipt, never a partial exe.
      string old = _directory + ".previous-" + Guid.NewGuid().ToString("N");
      if (Directory.Exists(_directory))
        Directory.Move(_directory, old);
      try
      {
        Directory.Move(temporary, _directory);
        temporary = null;
      }
      catch
      {
        if (Directory.Exists(old))
          Directory.Move(old, _directory);
        throw;
      }
      try
      {
        if (Directory.Exists(old))
          Directory.Delete(old, true);
      }
      catch (IOException) { }
      Publish("ready", Path.Combine(_directory, _platform.Executable));
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
      Publish("cancelled");
    }
    catch (Exception error)
    {
      string message =
        error is UnauthorizedAccessException
          ? "FFmpeg could not be saved in the TUFReplay folder. Check folder write permission, then retry."
        : error is HttpRequestException ? "FFmpeg could not be downloaded. Check your internet connection, then retry."
        : error is TaskCanceledException ? "The FFmpeg download timed out. Check your internet connection, then retry."
        : error.Message;
      Publish("failed", error: message);
    }
    finally
    {
      if (temporary != null)
        try
        {
          Directory.Delete(temporary, true);
        }
        catch (Exception) { }
    }
  }

  public static async Task ExtractZipAsync(string archive, string directory, string executable, CancellationToken token)
  {
    using var zip = ZipFile.OpenRead(archive);
    var entries = zip.Entries.Where(e => Path.GetFileName(e.FullName.Replace('\\', '/')) == executable).ToArray();
    if (entries.Length != 1)
      throw new InvalidDataException("The downloaded archive does not contain one FFmpeg executable. Try again later.");
    if (entries[0].Length == 0 || entries[0].Length > MaximumBytes)
      throw new InvalidDataException("The FFmpeg executable has an invalid size.");
    using (var input = entries[0].Open())
    using (var output = File.Create(Path.Combine(directory, executable)))
      await CopyBoundedAsync(input, output, MaximumBytes, token).ConfigureAwait(false);
    // Copy a small bounded set of vendor notices by basename; archive paths are
    // never used as filesystem destinations (including ../ or absolute paths).
    var notices = zip
      .Entries.Where(e =>
        new[] { "license", "copying", "gpl", "readme" }.Any(n =>
          Path.GetFileName(e.FullName).StartsWith(n, StringComparison.OrdinalIgnoreCase)
        )
      )
      .Take(12)
      .ToArray();
    int index = 0;
    foreach (var entry in notices)
    {
      token.ThrowIfCancellationRequested();
      if (entry.Length == 0 || entry.Length > 1024 * 1024)
        continue;
      using var input = entry.Open();
      using var output = File.Create(Path.Combine(directory, "vendor-notice-" + (++index) + ".txt"));
      await CopyBoundedAsync(input, output, 1024 * 1024, token).ConfigureAwait(false);
    }
  }

  private static async Task ExtractTarAsync(string archive, string destination, CancellationToken token)
  {
    string list = RunProcess("/usr/bin/tar", "-tJf " + Quote(archive), token);
    string[] entries = list.Split('\n')
      .Select(s => s.TrimEnd('\r'))
      .Where(s => s.EndsWith("/ffmpeg", StringComparison.Ordinal) || s == "ffmpeg")
      .ToArray();
    if (
      entries.Length != 1
      || entries[0].StartsWith("-")
      || entries[0].StartsWith("/")
      || entries[0].Split('/').Contains("..")
    )
      throw new InvalidDataException("The downloaded archive has an invalid FFmpeg entry.");
    using var process = Process.Start(
      new ProcessStartInfo("/usr/bin/tar", "-xJOf " + Quote(archive) + " " + Quote(entries[0]))
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      }
    );
    using var stop = token.Register(() =>
    {
      try
      {
        process.Kill();
      }
      catch (Exception) { }
    });
    var errors = process.StandardError.ReadToEndAsync();
    using (var output = File.Create(destination))
      await CopyBoundedAsync(process.StandardOutput.BaseStream, output, MaximumBytes, token).ConfigureAwait(false);
    process.WaitForExit();
    token.ThrowIfCancellationRequested();
    if (process.ExitCode != 0)
      throw new IOException("FFmpeg could not be extracted. Try again. " + await errors.ConfigureAwait(false));
  }

  private static string VerifyExecutable(string path, CancellationToken token)
  {
    string version = RunProcess(path, "-version", token);
    if (!version.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase))
      throw new IOException("The downloaded FFmpeg could not run. Retry installation or check your security software.");
    return version;
  }

  private static string RunProcess(string file, string arguments, CancellationToken token)
  {
    using var process = Process.Start(
      new ProcessStartInfo(file, arguments)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
      }
    );
    using var stop = token.Register(() =>
    {
      try
      {
        process.Kill();
      }
      catch (Exception) { }
    });
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(15000))
    {
      try
      {
        process.Kill();
      }
      catch (Exception) { }
      throw new IOException("FFmpeg setup did not respond. Retry installation.");
    }
    token.ThrowIfCancellationRequested();
    Task.WhenAll(output, error).GetAwaiter().GetResult();
    if (process.ExitCode != 0)
      throw new IOException("FFmpeg setup could not finish. " + error.Result);
    return output.Result;
  }

  public static async Task CopyBoundedAsync(
    Stream input,
    Stream output,
    long maximum,
    CancellationToken token,
    Action<long> progress = null
  )
  {
    byte[] buffer = new byte[65536];
    long total = 0;
    int count;
    while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
    {
      total += count;
      if (total > maximum)
        throw new InvalidDataException("The FFmpeg download exceeds its size limit.");
      await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
      progress?.Invoke(total);
    }
  }

  private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

  private static string Hash(string file)
  {
    using var sha = SHA256.Create();
    using var stream = File.OpenRead(file);
    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
  }

  private void Publish(string status, string path = null, string error = null)
  {
    lock (_gate)
    {
      _state.Status = status;
      _state.Path = path;
      _state.Error = error;
    }
    NotifyChanged();
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _disposed = true;
      _cancel?.Cancel();
    }
  }
}

public sealed class FfmpegInstallState
{
  public string Status;
  public string Platform;
  public string Source;
  public string InstallationDirectory;
  public string Path;
  public string Error;
  public long DownloadedBytes;
  public long? TotalBytes;
  public bool Available => Status == "ready";
  public bool Busy =>
    Status == "checking" || Status == "downloading" || Status == "extracting" || Status == "validating";

  internal FfmpegInstallState Clone() => (FfmpegInstallState)MemberwiseClone();
}

public sealed class FfmpegInstallReceipt
{
  public int SchemaVersion;
  public string Platform;
  public string Source;
  public string Sha256;
  public string Version;
  public DateTime InstalledAtUtc;
}

public sealed class FfmpegPlatform
{
  public string Name { get; }
  public string Executable { get; }
  public string Url { get; }
  public string ChecksumUrl { get; }
  public bool TarXz { get; }

  public FfmpegPlatform(string name, string executable, string url, bool tarXz = false, string checksumUrl = null)
  {
    Name = name;
    Executable = executable;
    Url = url;
    TarXz = tarXz;
    ChecksumUrl = checksumUrl;
  }

  public static FfmpegPlatform Current()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Is64BitOperatingSystem)
      return new FfmpegPlatform(
        "windows-x64",
        "ffmpeg.exe",
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
        checksumUrl: "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256"
      );
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      bool arm = RuntimeInformation.OSArchitecture == Architecture.Arm64;
      if (!arm)
        try
        {
          using var process = Process.Start(
            new ProcessStartInfo("/usr/sbin/sysctl", "-n hw.optional.arm64")
            {
              UseShellExecute = false,
              CreateNoWindow = true,
              RedirectStandardOutput = true,
            }
          );
          if (process.WaitForExit(2000))
            arm = process.ExitCode == 0 && process.StandardOutput.ReadToEnd().Trim() == "1";
          else
            process.Kill();
        }
        catch (Exception) { }
      return arm
        ? new FfmpegPlatform(
          "macos-arm64",
          "ffmpeg",
          "https://ffmpeg.martin-riedl.de/redirect/latest/macos/arm64/release/ffmpeg.zip"
        )
        : new FfmpegPlatform("macos-x64", "ffmpeg", "https://evermeet.cx/ffmpeg/getrelease/zip");
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.X64)
      return new FfmpegPlatform(
        "linux-x64",
        "ffmpeg",
        "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz",
        true
      );
    throw new PlatformNotSupportedException(
      "Automatic FFmpeg installation supports Windows 64-bit, macOS and Linux x64."
    );
  }
}
