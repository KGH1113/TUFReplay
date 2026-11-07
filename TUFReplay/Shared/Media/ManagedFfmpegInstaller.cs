using System;
using System.Collections.Concurrent;
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

public sealed class ManagedFfmpegInstaller : IDisposable
{
  public const long MaximumBytes = 256L * 1024 * 1024;
  public const string SharedFolderName = "ffmpeg";
  public const string ReceiptFileName = "installation.json";
  public const string LicenseFileName = "LICENSE.txt";
  public const string LockFileName = ".install.lock";

  // Serialize local callers independently of the runtime's file-sharing behavior.
  private static readonly ConcurrentDictionary<string, SemaphoreSlim> InstallGates = new(
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
  );
  private readonly object _gate = new();
  private readonly string _installRoot,
    _directory;
  private readonly FfmpegPlatform _platform;
  private readonly Func<HttpMessageHandler> _transport;
  private CancellationTokenSource _cancel = new();
  private bool _disposed;
  private long _nextProgressNotification;
  private FfmpegInstallState _state;
  public event Action Changed;
  public Task Work { get; private set; }

  public ManagedFfmpegInstaller(
    string gameDirectory,
    FfmpegPlatform platform,
    Func<HttpMessageHandler> transport = null
  )
  {
    if (string.IsNullOrWhiteSpace(gameDirectory))
      throw new ArgumentException("A game directory is required.", nameof(gameDirectory));
    _platform = platform;
    _transport = transport;
    _installRoot = Path.Combine(Path.GetFullPath(gameDirectory), SharedFolderName);
    _directory = Path.Combine(_installRoot, platform.Name);
    _state = new FfmpegInstallState
    {
      Status = "checking",
      Platform = platform.Name,
      Source = platform.Url,
      InstallationDirectory = _directory,
    };
    var token = _cancel.Token;
    Work = Task.Run(() => InstallAsync(false, token));
  }

  public FfmpegInstallState Snapshot()
  {
    lock (_gate)
      return _state.Clone();
  }

  public void NotifySharedInstallationCompleted(string platform, string executablePath)
  {
    if (platform != _platform.Name || string.IsNullOrWhiteSpace(executablePath))
      return;
    try
    {
      if (
        !Path.IsPathRooted(executablePath)
        || !string.Equals(
          Path.GetFullPath(executablePath),
          Path.Combine(_directory, _platform.Executable),
          RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal
        )
      )
        return;
    }
    catch (ArgumentException)
    {
      return;
    }
    catch (NotSupportedException)
    {
      return;
    }
    lock (_gate)
    {
      if (_disposed || _state.Busy || _state.Available)
        return;
      string previousStatus = _state.Status;
      _cancel.Dispose();
      _cancel = new CancellationTokenSource();
      var token = _cancel.Token;
      _state.Status = "checking";
      // A notification is only a hint. Verify the receipt and file off the game thread.
      Work = Task.Run(() => InstallAsync(false, token, previousStatus));
    }
    NotifyChanged();
  }

  public FfmpegInstallState Request()
  {
    lock (_gate)
    {
      if (_disposed)
        throw new ObjectDisposedException(nameof(ManagedFfmpegInstaller));
      if (_state.Status is "missing" or "declined" or "cancelled" or "failed")
      {
        _state.Status = "awaiting-consent";
        _state.Error = null;
      }
    }
    NotifyChanged();
    return Snapshot();
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
    CancellationTokenSource cancel;
    lock (_gate)
    {
      if (_disposed)
        return;
      cancel = _cancel;
      if (_state.Status == "awaiting-consent")
        _state.Status = "cancelled";
    }
    try
    {
      cancel.Cancel();
    }
    catch (ObjectDisposedException) { }
    NotifyChanged();
  }

  public void Confirm()
  {
    lock (_gate)
    {
      if (_disposed || _state.Status != "awaiting-consent")
        return;
      _cancel.Dispose();
      _cancel = new CancellationTokenSource();
      var token = _cancel.Token;
      _state.Status = "downloading";
      _state.Error = null;
      _state.DownloadedBytes = 0;
      _state.TotalBytes = null;
      Work = Task.Run(() => InstallAsync(true, token));
    }
    NotifyChanged();
  }

  private async Task InstallAsync(bool allowDownload, CancellationToken token, string unavailableStatus = "missing")
  {
    string staging = null;
    try
    {
      using var gate = await AcquireInstallGateAsync(TimeSpan.FromSeconds(allowDownload ? 900 : 5), token)
        .ConfigureAwait(false);
      if (gate == null)
      {
        if (allowDownload)
          throw new TimeoutException(
            "Another FFmpeg installation is still running. Wait for it to finish, then retry."
          );
        Publish(unavailableStatus);
        return;
      }
      var installed = ReadInstalled();
      if (installed != null)
      {
        Publish("ready", installed);
        return;
      }
      if (!allowDownload)
      {
        Publish(unavailableStatus);
        return;
      }
      staging = Path.Combine(_installRoot, ".staging-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(staging);
      var executable = Path.Combine(staging, _platform.Executable);
      await DownloadAsync(staging, token).ConfigureAwait(false);
      token.ThrowIfCancellationRequested();
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        RunProcess("/bin/chmod", "755 " + Quote(executable), token);
      Publish("validating");
      installed = PromoteStaging(staging, Hash(executable), token);
      Publish("ready", installed);
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
      Publish("cancelled");
    }
    catch (Exception error)
    {
      Publish("failed", error: FriendlyError(error));
    }
    finally
    {
      if (staging != null)
        TryDeleteDirectory(staging);
    }
  }

  private async Task<IDisposable> AcquireInstallGateAsync(TimeSpan timeout, CancellationToken token)
  {
    Directory.CreateDirectory(_installRoot);
    var elapsed = Stopwatch.StartNew();
    var semaphore = InstallGates.GetOrAdd(_installRoot, _ => new SemaphoreSlim(1, 1));
    if (!await semaphore.WaitAsync(timeout, token).ConfigureAwait(false))
      return null;
    try
    {
      while (true)
      {
        token.ThrowIfCancellationRequested();
        try
        {
          var file = new FileStream(
            Path.Combine(_installRoot, LockFileName),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
          );
          try
          {
            // Mono only enforces FileShare inside its process on Unix. flock is
            // held by the open file description, so async thread changes are safe.
            if (
              !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
              && Flock(file.SafeFileHandle.DangerousGetHandle().ToInt32(), 2 | 4) != 0
            )
            {
              int error = Marshal.GetLastWin32Error();
              if (error == 11 || error == 35) // EWOULDBLOCK on Linux / macOS
                throw new IOException("Another FFmpeg installer holds the file lock.");
              throw new System.ComponentModel.Win32Exception(
                error,
                "FFmpeg could not lock the game folder. Check its permissions, then retry."
              );
            }
          }
          catch
          {
            file.Dispose();
            throw;
          }
          var gate = new InstallGate(file, semaphore);
          semaphore = null;
          return gate;
        }
        catch (IOException)
        {
          if (elapsed.Elapsed >= timeout)
            return null;
          await Task.Delay(100, token).ConfigureAwait(false);
        }
      }
    }
    finally
    {
      semaphore?.Release();
    }
  }

  [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
  private static extern int Flock(int descriptor, int operation);

  private sealed class InstallGate : IDisposable
  {
    private FileStream _file;
    private readonly SemaphoreSlim _semaphore;

    public InstallGate(FileStream file, SemaphoreSlim semaphore)
    {
      _file = file;
      _semaphore = semaphore;
    }

    public void Dispose()
    {
      var file = Interlocked.Exchange(ref _file, null);
      if (file == null)
        return;
      try
      {
        file.Dispose();
      }
      finally
      {
        _semaphore.Release();
      }
    }
  }

  private string ReadInstalled()
  {
    try
    {
      var executable = Path.Combine(_directory, _platform.Executable);
      var receipt = ReadReceipt(_directory);
      return
        File.Exists(executable)
        && new FileInfo(executable).Length > 0
        && receipt != null
        && receipt.SchemaVersion == 1
        && receipt.Platform == _platform.Name
        && (string.IsNullOrEmpty(receipt.Executable) || receipt.Executable == _platform.Executable)
        && IsSha256(receipt.Sha256)
        && receipt.Version?.TrimStart().StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase) == true
        && string.Equals(Hash(executable), receipt.Sha256, StringComparison.OrdinalIgnoreCase)
        ? executable
        : null;
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }

  private async Task DownloadAsync(string staging, CancellationToken token)
  {
    if (string.IsNullOrWhiteSpace(_platform.ChecksumUrl))
      throw new InvalidDataException(
        "This FFmpeg provider has no checksum. Update TUFReplay, then retry installation."
      );
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
    timeout.CancelAfter(TimeSpan.FromMinutes(5));
    var downloadToken = timeout.Token;
    using var client = _transport == null ? new HttpClient() : new HttpClient(_transport());
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-FFmpeg-Installer/2.0");
    string expected = null;
    if (_platform.ChecksumUrl != null)
    {
      using var checksum = await client
        .GetAsync(_platform.ChecksumUrl, HttpCompletionOption.ResponseHeadersRead, downloadToken)
        .ConfigureAwait(false);
      RequireResponse(checksum);
      using var body = await checksum.Content.ReadAsStreamAsync().ConfigureAwait(false);
      using var buffer = new MemoryStream();
      await CopyBoundedAsync(body, buffer, 65536, downloadToken).ConfigureAwait(false);
      expected = ParseChecksum(Encoding.UTF8.GetString(buffer.ToArray()), _platform.Url);
    }
    var archive = Path.Combine(staging, _platform.TarXz ? "download.tar.xz" : "download.zip");
    using (
      var response = await client
        .GetAsync(_platform.Url, HttpCompletionOption.ResponseHeadersRead, downloadToken)
        .ConfigureAwait(false)
    )
    {
      RequireResponse(response);
      var total = response.Content.Headers.ContentLength;
      if (total > MaximumBytes)
        throw new InvalidDataException("The FFmpeg download exceeds its size limit.");
      lock (_gate)
        _state.TotalBytes = total;
      using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
      using var output = File.Create(archive);
      await CopyBoundedAsync(
          input,
          output,
          MaximumBytes,
          downloadToken,
          bytes =>
          {
            lock (_gate)
              _state.DownloadedBytes = bytes;
            NotifyChanged(true);
          }
        )
        .ConfigureAwait(false);
    }
    if (expected != null && !string.Equals(Hash(archive), expected, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The FFmpeg download did not match the provider's checksum.");
    Publish("extracting");
    if (_platform.TarXz)
      await ExtractTarAsync(archive, Path.Combine(staging, _platform.Executable), token).ConfigureAwait(false);
    else
      await ExtractZipAsync(archive, staging, _platform.Executable, token).ConfigureAwait(false);
    File.Delete(archive);
  }

  private static void RequireResponse(HttpResponseMessage response)
  {
    response.EnsureSuccessStatusCode();
    if (response.RequestMessage.RequestUri.Scheme != "https")
      throw new IOException("The provider redirected to an insecure address.");
  }

  private string PromoteStaging(string staging, string expectedHash, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    var backup = _directory + ".previous-" + Guid.NewGuid().ToString("N");
    bool hadPrevious = Directory.Exists(_directory),
      published = false;
    if (hadPrevious)
      Directory.Move(_directory, backup);
    try
    {
      Directory.Move(staging, _directory);
      published = true;
      var executable = Path.Combine(_directory, _platform.Executable);
      if (!IsSha256(expectedHash) || !string.Equals(Hash(executable), expectedHash, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("The FFmpeg executable changed during installation. Retry installation.");
      var version = RunProcess(executable, "-version", token);
      if (!version.TrimStart().StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase))
        throw new IOException("The FFmpeg executable could not be verified.");
      token.ThrowIfCancellationRequested();
      var receipt = new FfmpegInstallReceipt
      {
        SchemaVersion = 1,
        Platform = _platform.Name,
        Executable = _platform.Executable,
        Source = _platform.Url,
        Sha256 = expectedHash,
        Version = version,
        InstalledAtUtc = DateTime.UtcNow,
      };
      File.WriteAllText(
        Path.Combine(_directory, "DOWNLOAD-SOURCE.txt"),
        "Provider: " + _platform.Url + "\nUpstream: https://ffmpeg.org/\n"
      );
      var record = Path.Combine(_directory, ReceiptFileName);
      File.WriteAllText(record + ".tmp", JsonConvert.SerializeObject(receipt, Formatting.Indented));
      File.Move(record + ".tmp", record);
      if (hadPrevious)
        TryDeleteDirectory(backup);
      FfmpegApi.NotifyOtherMod(_platform.Name, executable);
      return executable;
    }
    catch
    {
      if (published)
        TryDeleteDirectory(_directory);
      if (hadPrevious && !Directory.Exists(_directory))
        Directory.Move(backup, _directory);
      throw;
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
      .Where(s =>
        s.EndsWith("/" + Path.GetFileName(destination), StringComparison.Ordinal) || s == Path.GetFileName(destination)
      )
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

  private static bool IsSha256(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

  public static string ParseChecksum(string text, string archiveUrl)
  {
    var name = Path.GetFileName(new Uri(archiveUrl).AbsolutePath);
    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
      var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length == 0 || !IsSha256(parts[0]))
        continue;
      if ((lines.Length == 1 && parts.Length == 1) || (parts.Length == 2 && parts[1].TrimStart('*') == name))
        return parts[0];
    }
    throw new InvalidDataException("The provider checksum does not contain the selected FFmpeg archive.");
  }

  private static FfmpegInstallReceipt ReadReceipt(string directory)
  {
    var path = Path.Combine(directory, ReceiptFileName);
    try
    {
      return new FileInfo(path).Length <= 65536
        ? JsonConvert.DeserializeObject<FfmpegInstallReceipt>(File.ReadAllText(path))
        : null;
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private static void TryDeleteDirectory(string directory)
  {
    try
    {
      Directory.Delete(directory, true);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private void NotifyChanged(bool progress = false)
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      if (progress)
      {
        long now = Stopwatch.GetTimestamp();
        if (now < _nextProgressNotification)
          return;
        _nextProgressNotification = now + Stopwatch.Frequency / 10;
      }
    }
    Changed?.Invoke();
  }

  private void Publish(string status, string path = null, string error = null)
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      _state.Status = status;
      _state.Path = path;
      _state.Error = error;
    }
    NotifyChanged();
  }

  private static string FriendlyError(Exception error) =>
    error switch
    {
      UnauthorizedAccessException => "FFmpeg could not be saved. Check the game folder's write permission, then retry.",
      HttpRequestException => "FFmpeg could not be downloaded. Check your internet connection, then retry.",
      OperationCanceledException => "The FFmpeg download timed out. Check your internet connection, then retry.",
      _ => error.Message,
    };

  public void Dispose()
  {
    CancellationTokenSource cancel;
    Task work;
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      cancel = _cancel;
      work = Work;
    }
    cancel.Cancel();
    _ = work.ContinueWith(_ => cancel.Dispose(), TaskScheduler.Default);
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
  public string Executable;
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

  public static string GameDirectory
  {
    get
    {
      string dataPath = Path.GetFullPath(UnityEngine.Application.dataPath);
      bool macOS =
        UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer
        || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor;
      return ResolveGameDirectory(dataPath, macOS);
    }
  }

  public static string ResolveGameDirectory(string dataPath, bool macOS)
  {
    var data = new DirectoryInfo(Path.GetFullPath(dataPath));
    if (macOS)
      for (var parent = data; parent != null; parent = parent.Parent)
        if (parent.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
          return parent.Parent.FullName;
    return data.Parent.FullName;
  }

  public static FfmpegPlatform Current()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Is64BitOperatingSystem)
      return new FfmpegPlatform(
        "windows-x64",
        "ffmpeg.exe",
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-gpl-9.0.zip",
        checksumUrl: "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256"
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
          "https://ffmpeg.martin-riedl.de/download/macos/arm64/1789931890_9.0.2/ffmpeg.zip",
          checksumUrl: "https://ffmpeg.martin-riedl.de/download/macos/arm64/1789931890_9.0.2/ffmpeg.zip.sha256"
        )
        : new FfmpegPlatform(
          "macos-x64",
          "ffmpeg",
          "https://ffmpeg.martin-riedl.de/download/macos/amd64/1789931006_9.0.2/ffmpeg.zip",
          checksumUrl: "https://ffmpeg.martin-riedl.de/download/macos/amd64/1789931006_9.0.2/ffmpeg.zip.sha256"
        );
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.OSArchitecture == Architecture.X64)
      return new FfmpegPlatform(
        "linux-x64",
        "ffmpeg",
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz",
        true,
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256"
      );
    throw new PlatformNotSupportedException(
      "Automatic FFmpeg installation supports Windows 64-bit, macOS and Linux x64."
    );
  }
}
