using System;
using System.Collections.Generic;
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

// Owns the FFmpeg installation in <game>/ffmpeg/<platform>/. OrbitRender uses
// the same layout, receipt and lock file so either mod can reuse the other's
// verified installation without downloading it again.
public sealed class ManagedFfmpegInstaller : IDisposable
{
  public const long MaximumBytes = 256L * 1024 * 1024;
  public const string SharedFolderName = "ffmpeg";
  public const string ReceiptFileName = "installation.json";
  public const string LicenseFileName = "LICENSE.txt";
  public const string LockFileName = ".install.lock";
  public const string MutexName = "Adofai.SharedFFmpeg.Install";
  private const int SchemaVersion = 1;
  private static readonly TimeSpan InstallLockTimeout = TimeSpan.FromMinutes(15);
  private static readonly TimeSpan InitialInspectTimeout = TimeSpan.FromSeconds(5);
  private static readonly TimeSpan InstallLockPoll = TimeSpan.FromMilliseconds(250);
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
  private readonly string _gameDirectory;
  private readonly string _installRoot;
  private readonly string _stagingRoot;
  private readonly string _lockPath;
  private readonly string _directory;
  private readonly FfmpegPlatform _platform;
  private readonly Func<HttpMessageHandler> _transport;
  private CancellationTokenSource _cancel;
  private bool _disposed;
  private FfmpegInstallState _state;
  public Task Work { get; private set; }

  public ManagedFfmpegInstaller(
    string gameDirectory,
    FfmpegPlatform platform,
    Func<HttpMessageHandler> transport = null
  )
  {
    if (string.IsNullOrWhiteSpace(gameDirectory))
      throw new ArgumentException("A game directory is required.", nameof(gameDirectory));
    _gameDirectory = Path.GetFullPath(gameDirectory);
    _platform = platform;
    _transport = transport;
    _installRoot = Path.Combine(_gameDirectory, SharedFolderName);
    _stagingRoot = Path.Combine(_installRoot, ".staging");
    _lockPath = Path.Combine(_installRoot, LockFileName);
    _directory = Path.Combine(_installRoot, platform.Name);
    _state = new FfmpegInstallState
    {
      Status = "checking",
      Platform = platform.Name,
      Source = platform.Url,
      InstallationDirectory = _directory,
    };
    Work = Task.Run(InitializeAsync);
  }

  private async Task InitializeAsync()
  {
    try
    {
      if (TryInspect(_directory, true, out var path, out _))
      {
        Publish("ready", path);
        return;
      }

      // Promote an older per-mod installation without asking for consent.
      // When another mod is already installing, leave the state as missing;
      // Request/Confirm will wait for the shared lock and reuse its result.
      using (
        var gate = await AcquireInstallGateAsync(InitialInspectTimeout, CancellationToken.None).ConfigureAwait(false)
      )
      {
        if (gate == null)
        {
          Publish("missing");
          return;
        }
        if (gate.Ready)
        {
          Publish("ready", gate.ReadyPath);
          return;
        }
        if (TryInspect(_directory, true, out path, out _))
        {
          Publish("ready", path);
          return;
        }
        if (TryPromoteLegacy(CancellationToken.None, out path))
        {
          Publish("ready", path);
          return;
        }
      }
      Publish("missing");
    }
    catch
    {
      Publish("missing");
    }
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
    CancellationToken token;
    lock (_gate)
    {
      if (_disposed || _state.Status != "awaiting-consent")
        return;
      _cancel?.Dispose();
      _cancel = new CancellationTokenSource();
      token = _cancel.Token;
      _state.Status = "downloading";
      _state.Error = null;
      _state.DownloadedBytes = 0;
      _state.TotalBytes = null;
      Work = Task.Run(() => InstallAsync(token));
    }
    NotifyChanged();
  }

  private async Task InstallAsync(CancellationToken token)
  {
    try
    {
      using (var gate = await AcquireInstallGateAsync(InstallLockTimeout, token).ConfigureAwait(false))
      {
        if (gate.Ready)
        {
          Publish("ready", gate.ReadyPath);
          return;
        }

        if (TryInspect(_directory, true, out var readyPath, out _))
        {
          Publish("ready", readyPath);
          return;
        }

        if (TryPromoteLegacy(token, out readyPath))
        {
          Publish("ready", readyPath);
          return;
        }

        var installed = await DownloadAndInstallAsync(token).ConfigureAwait(false);
        Publish("ready", installed);
      }
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
      Publish("cancelled");
    }
    catch (Exception error)
    {
      Publish("failed", error: FriendlyError(error));
    }
  }

  private async Task<string> DownloadAndInstallAsync(CancellationToken token)
  {
    string staging = null;
    try
    {
      Directory.CreateDirectory(_stagingRoot);
      staging = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(staging);
      string archive = Path.Combine(staging, _platform.TarXz ? "download.tar.xz" : "download.zip");
      using (var client = _transport == null ? new HttpClient() : new HttpClient(_transport()))
      {
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-FFmpeg-Installer/2.0");
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
      string executable = Path.Combine(staging, _platform.Executable);
      if (_platform.TarXz)
        await ExtractTarAsync(archive, executable, token).ConfigureAwait(false);
      else
        await ExtractZipAsync(archive, staging, _platform.Executable, token).ConfigureAwait(false);
      if (_platform.Executable != "ffmpeg.exe")
        RunProcess("/bin/chmod", "755 " + Quote(executable), token);
      Publish("validating");
      string version = VerifyExecutable(executable, token);
      var receipt = new FfmpegInstallReceipt
      {
        SchemaVersion = SchemaVersion,
        Platform = _platform.Name,
        Executable = _platform.Executable,
        Source = _platform.Url,
        Sha256 = Hash(executable),
        Version = version,
        InstalledAtUtc = DateTime.UtcNow,
      };
      WriteReceipt(staging, receipt);
      WriteDownloadSource(staging, null);
      EnsureLicense(staging);
      File.Delete(archive);
      token.ThrowIfCancellationRequested();
      string published = PromoteStaging(staging, token);
      staging = null;
      return published;
    }
    finally
    {
      if (staging != null)
        try
        {
          Directory.Delete(staging, true);
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

  private string VerifyExecutable(string path, CancellationToken token)
  {
    string version = RunProcess(path, "-version", token);
    if (!version.TrimStart().StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase))
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

  private static bool IsSha256(string value)
  {
    if (value == null || value.Length != 64)
      return false;
    foreach (var character in value)
      if (!Uri.IsHexDigit(character))
        return false;
    return true;
  }

  private bool TryInspect(string directory, bool repairMetadata, out string executable, out string error)
  {
    executable = null;
    error = null;
    try
    {
      string path = Path.Combine(directory, _platform.Executable);
      if (!File.Exists(path) || new FileInfo(path).Length <= 0)
      {
        error = "The FFmpeg executable is missing.";
        return false;
      }

      var receipt = ReadReceipt(Path.Combine(directory, ReceiptFileName));
      bool validReceipt =
        receipt != null
        && receipt.SchemaVersion == SchemaVersion
        && receipt.Platform == _platform.Name
        && (string.IsNullOrEmpty(receipt.Executable) || receipt.Executable == _platform.Executable);
      if (validReceipt && !string.IsNullOrEmpty(receipt.Sha256))
      {
        if (!string.Equals(Hash(path), receipt.Sha256, StringComparison.OrdinalIgnoreCase))
        {
          error = "The FFmpeg executable does not match its installation record.";
          return false;
        }
      }
      else
      {
        validReceipt = false;
      }

      string version;
      try
      {
        version = VerifyExecutable(path, CancellationToken.None);
      }
      catch (Exception exception)
      {
        error = exception.Message;
        return false;
      }

      if (!validReceipt && repairMetadata)
        WriteReceipt(
          directory,
          new FfmpegInstallReceipt
          {
            SchemaVersion = SchemaVersion,
            Platform = _platform.Name,
            Executable = _platform.Executable,
            Source = _platform.Url,
            Sha256 = Hash(path),
            Version = version,
            InstalledAtUtc = DateTime.UtcNow,
          }
        );
      if (repairMetadata)
        EnsureLicense(directory);
      executable = path;
      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      return false;
    }
  }

  private bool TryPromoteLegacy(CancellationToken token, out string installedPath)
  {
    installedPath = null;
    foreach (var candidate in LegacyCandidates())
    {
      token.ThrowIfCancellationRequested();
      if (!File.Exists(candidate.Executable))
        continue;
      try
      {
        var receipt = ReadReceipt(Path.Combine(candidate.Directory, ReceiptFileName));
        if (
          receipt != null
          && receipt.SchemaVersion == SchemaVersion
          && receipt.Platform == _platform.Name
          && !string.IsNullOrEmpty(receipt.Sha256)
          && IsSha256(receipt.Sha256)
          && !string.Equals(Hash(candidate.Executable), receipt.Sha256, StringComparison.OrdinalIgnoreCase)
        )
          continue;

        VerifyExecutable(candidate.Executable, token);
        string staging = CreateStagingDirectory();
        try
        {
          string stagedExecutable = Path.Combine(staging, _platform.Executable);
          File.Copy(candidate.Executable, stagedExecutable, false);
          if (_platform.Executable != "ffmpeg.exe")
            RunProcess("/bin/chmod", "755 " + Quote(stagedExecutable), token);
          string version = VerifyExecutable(stagedExecutable, token);
          WriteReceipt(
            staging,
            new FfmpegInstallReceipt
            {
              SchemaVersion = SchemaVersion,
              Platform = _platform.Name,
              Executable = _platform.Executable,
              Source = _platform.Url,
              Sha256 = Hash(stagedExecutable),
              Version = version,
              InstalledAtUtc = DateTime.UtcNow,
            }
          );
          CopyLicense(candidate.Directory, staging);
          EnsureLicense(staging);
          WriteDownloadSource(staging, candidate.Executable);
          installedPath = PromoteStaging(staging, token);
          staging = null;
          return true;
        }
        finally
        {
          if (staging != null)
            try
            {
              Directory.Delete(staging, true);
            }
            catch (Exception) { }
        }
      }
      catch (Exception)
      {
        // Try the next existing candidate.
      }
    }
    return false;
  }

  private IEnumerable<LegacyCandidate> LegacyCandidates()
  {
    string mods = Path.Combine(_gameDirectory, "Mods");
    yield return new LegacyCandidate(
      Path.Combine(mods, "OrbitRender", "FFmpeg", _platform.Name),
      Path.Combine(mods, "OrbitRender", "FFmpeg", _platform.Name, _platform.Executable)
    );
    yield return new LegacyCandidate(
      Path.Combine(mods, "TUFReplay", "FFmpeg", _platform.Name),
      Path.Combine(mods, "TUFReplay", "FFmpeg", _platform.Name, _platform.Executable)
    );
    // Some game distributions already ship ffmpeg next to the game executable.
    yield return new LegacyCandidate(_installRoot, Path.Combine(_installRoot, _platform.Executable));
  }

  private string PromoteStaging(string staging, CancellationToken token)
  {
    token.ThrowIfCancellationRequested();
    if (TryInspect(_directory, true, out var existing, out _))
    {
      try
      {
        Directory.Delete(staging, true);
      }
      catch (Exception) { }
      return existing;
    }

    Directory.CreateDirectory(_installRoot);
    string backup = null;
    if (Directory.Exists(_directory))
    {
      backup =
        _directory
        + ".previous-"
        + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")
        + "-"
        + Guid.NewGuid().ToString("N").Substring(0, 8);
      try
      {
        Directory.Move(_directory, backup);
      }
      catch (Exception error)
      {
        throw new IOException(
          "The existing FFmpeg installation is in use and was not overwritten. "
            + "Close any running FFmpeg processes and try again.",
          error
        );
      }
    }

    try
    {
      Directory.Move(staging, _directory);
    }
    catch
    {
      if (!Directory.Exists(_directory) && backup != null && Directory.Exists(backup))
        try
        {
          Directory.Move(backup, _directory);
        }
        catch (Exception) { }
      throw;
    }
    return Path.Combine(_directory, _platform.Executable);
  }

  private string CreateStagingDirectory()
  {
    Directory.CreateDirectory(_stagingRoot);
    string staging = Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging);
    return staging;
  }

  private async Task<InstallGate> AcquireInstallGateAsync(TimeSpan timeout, CancellationToken token)
  {
    var deadline = DateTime.UtcNow + timeout;
    var mutex = new Mutex(false, MutexName);
    bool mutexHeld = false;
    FileStream file = null;
    try
    {
      while (true)
      {
        token.ThrowIfCancellationRequested();
        try
        {
          mutexHeld = mutex.WaitOne(InstallLockPoll);
        }
        catch (AbandonedMutexException)
        {
          mutexHeld = true;
        }
        if (mutexHeld)
          break;
        if (TryReadPublishedInstallation(out var readyPath))
          return new InstallGate(null, mutex, false, readyPath);
        if (timeout == TimeSpan.Zero || DateTime.UtcNow >= deadline)
        {
          mutex.Dispose();
          return null;
        }
        await Task.Delay(InstallLockPoll, token).ConfigureAwait(false);
      }

      Directory.CreateDirectory(_installRoot);
      while (true)
      {
        token.ThrowIfCancellationRequested();
        try
        {
          file = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
          break;
        }
        catch (IOException)
        {
          if (TryReadPublishedInstallation(out var readyPath))
            return new InstallGate(null, mutex, mutexHeld, readyPath);
          if (timeout == TimeSpan.Zero || DateTime.UtcNow >= deadline)
          {
            if (timeout == TimeSpan.Zero)
            {
              mutex.ReleaseMutex();
              mutex.Dispose();
              return null;
            }
            throw new TimeoutException("Timed out waiting for another FFmpeg installation to finish.");
          }
          await Task.Delay(InstallLockPoll, token).ConfigureAwait(false);
        }
      }

      if (TryReadPublishedInstallation(out var ready))
        return new InstallGate(file, mutex, mutexHeld, ready);
      return new InstallGate(file, mutex, mutexHeld, null);
    }
    catch
    {
      if (file != null)
        try
        {
          file.Dispose();
        }
        catch (Exception) { }
      if (mutexHeld)
        try
        {
          mutex.ReleaseMutex();
        }
        catch (Exception) { }
      try
      {
        mutex.Dispose();
      }
      catch (Exception) { }
      throw;
    }
  }

  private bool TryReadPublishedInstallation(out string path)
  {
    path = null;
    try
    {
      if (!File.Exists(Path.Combine(_directory, _platform.Executable)))
        return false;
      if (!File.Exists(Path.Combine(_directory, ReceiptFileName)))
        return false;
      return TryInspect(_directory, true, out path, out _);
    }
    catch
    {
      return false;
    }
  }

  private FfmpegInstallReceipt ReadReceipt(string path)
  {
    try
    {
      if (!File.Exists(path))
        return null;
      var info = new FileInfo(path);
      if (info.Length <= 0 || info.Length > 64 * 1024)
        return null;
      return JsonConvert.DeserializeObject<FfmpegInstallReceipt>(File.ReadAllText(path));
    }
    catch
    {
      return null;
    }
  }

  private static void WriteReceipt(string directory, FfmpegInstallReceipt receipt)
  {
    string path = Path.Combine(directory, ReceiptFileName);
    string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
    try
    {
      File.WriteAllText(temporary, JsonConvert.SerializeObject(receipt, Formatting.Indented), new UTF8Encoding(false));
      if (!File.Exists(path))
      {
        File.Move(temporary, path);
        return;
      }
      try
      {
        File.Replace(temporary, path, null);
      }
      catch
      {
        File.Copy(temporary, path, true);
      }
    }
    finally
    {
      try
      {
        if (File.Exists(temporary))
          File.Delete(temporary);
      }
      catch (Exception) { }
    }
  }

  private void WriteDownloadSource(string directory, string migratedFrom)
  {
    var text =
      "Downloaded directly from: "
      + _platform.Url
      + "\nUpstream: https://ffmpeg.org/\n"
      + "Installed in the A Dance of Fire and Ice/ffmpeg folder.\n";
    if (!string.IsNullOrEmpty(migratedFrom))
      text += "Migrated from: " + migratedFrom + "\n";
    File.WriteAllText(Path.Combine(directory, "DOWNLOAD-SOURCE.txt"), text, new UTF8Encoding(false));
  }

  private void EnsureLicense(string directory)
  {
    string target = Path.Combine(directory, LicenseFileName);
    if (File.Exists(target))
      return;
    CopyLicense(directory, directory);
    if (!File.Exists(target))
    {
      var text =
        "FFmpeg is distributed under the GNU GPLv3 license.\n"
        + "Full license: https://www.gnu.org/licenses/gpl-3.0.txt\n"
        + "Source archive: "
        + _platform.Url
        + "\nInstalled in the A Dance of Fire and Ice/ffmpeg folder.\n";
      File.WriteAllText(target, text, new UTF8Encoding(false));
    }
  }

  private static void CopyLicense(string sourceDirectory, string destinationDirectory)
  {
    string target = Path.Combine(destinationDirectory, LicenseFileName);
    if (File.Exists(target))
      return;
    foreach (
      var name in new[]
      {
        "FFmpeg-LICENSE.txt",
        "LICENSE.txt",
        "LICENSE",
        "GPLv3.txt",
        "COPYING",
        "vendor-notice-1.txt",
      }
    )
    {
      string source = Path.Combine(sourceDirectory, name);
      if (!File.Exists(source) || string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        continue;
      try
      {
        File.Copy(source, target, false);
      }
      catch (Exception) { }
      if (File.Exists(target))
        return;
    }
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

  private static string FriendlyError(Exception error)
  {
    return error is UnauthorizedAccessException
        ? "FFmpeg could not be saved in the game's ffmpeg folder. Check folder write permission, then retry."
      : error is HttpRequestException ? "FFmpeg could not be downloaded. Check your internet connection, then retry."
      : error is TaskCanceledException ? "The FFmpeg download timed out. Check your internet connection, then retry."
      : error.Message;
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _disposed = true;
      _cancel?.Cancel();
    }
  }

  private sealed class LegacyCandidate
  {
    public readonly string Directory;
    public readonly string Executable;

    public LegacyCandidate(string directory, string executable)
    {
      Directory = directory;
      Executable = executable;
    }
  }

  private sealed class InstallGate : IDisposable
  {
    private readonly FileStream _file;
    private readonly Mutex _mutex;
    private readonly bool _mutexHeld;

    public readonly string ReadyPath;
    public bool Ready => ReadyPath != null;

    public InstallGate(FileStream file, Mutex mutex, bool mutexHeld, string readyPath)
    {
      _file = file;
      _mutex = mutex;
      _mutexHeld = mutexHeld;
      ReadyPath = readyPath;
    }

    public void Dispose()
    {
      if (_file != null)
        try
        {
          _file.Dispose();
        }
        catch (Exception) { }
      if (_mutexHeld)
        try
        {
          _mutex.ReleaseMutex();
        }
        catch (Exception) { }
      try
      {
        _mutex.Dispose();
      }
      catch (Exception) { }
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
      var data = new DirectoryInfo(dataPath);
      bool macOS =
        UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer
        || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor;
      return macOS && string.Equals(data.Name, "Contents", StringComparison.OrdinalIgnoreCase)
        ? data.FullName
        : data.Parent.FullName;
    }
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
