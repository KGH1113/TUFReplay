using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Shared.Media;

namespace TUFReplay.Shared.Downloads;

// Downloads the independently licensed mod; never loads it or replaces a running mod.
public sealed class ManagedRendererInstaller : IDisposable
{
  public const string ModId = "TUFReplay-Renderer";
  public const string Repository = "https://github.com/KGH1113/TUFReplay-Renderer";
  private const string ReleaseApi = "https://api.github.com/repos/KGH1113/TUFReplay-Renderer/releases?per_page=30";
  private const long MaximumBytes = 128L * 1024 * 1024;
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
  private readonly Func<HttpMessageHandler> _transport;
  private RendererInstallState _state;
  private CancellationTokenSource _cancel;
  private bool _disposed;
  public Task Work { get; private set; }

  public ManagedRendererInstaller(string modsDirectory, Func<HttpMessageHandler> transport = null)
  {
    _directory = Path.Combine(modsDirectory, ModId);
    _transport = transport;
    _state = new RendererInstallState
    {
      Status = "checking",
      InstallationDirectory = _directory,
      Source = Repository,
    };
    Work = Task.Run(() =>
    {
      try
      {
        string info = Path.Combine(_directory, "Info.json");
        bool installed =
          File.Exists(info)
          && new FileInfo(info).Length < 65536
          && File.Exists(Path.Combine(_directory, ModId + ".dll"));
        string version = installed ? (string)JObject.Parse(File.ReadAllText(info))["Version"] : null;
        installed = installed && (string)JObject.Parse(File.ReadAllText(info))["Id"] == ModId;
        lock (_gate)
        {
          _state.Status = installed ? "restart-required" : "missing";
          _state.Version = installed ? version : null;
        }
      }
      catch
      {
        Publish("missing");
      }
    });
  }

  public RendererInstallState Snapshot(bool loaded = false)
  {
    lock (_gate)
    {
      var state = _state.Clone();
      if (loaded)
        state.Status = "ready";
      return state;
    }
  }

  public void Request()
  {
    lock (_gate)
    {
      if (_disposed)
        throw new ObjectDisposedException(nameof(ManagedRendererInstaller));
      if (_state.Status == "missing" || _state.Status == "failed" || _state.Status == "cancelled")
      {
        _state.Status = "awaiting-consent";
        _state.Error = _state.ErrorCode = null;
        _state.DownloadedBytes = 0;
        _state.TotalBytes = null;
      }
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
      _state.Status = "resolving";
      Work = Task.Run(() => InstallAsync(_cancel.Token));
    }
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

  private async Task InstallAsync(CancellationToken token)
  {
    string staging = null;
    try
    {
      using var handler = _transport?.Invoke() ?? new HttpClientHandler();
      using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
      client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-DownloadCenter/1.0");
      using var releaseResponse = await client
        .GetAsync(ReleaseApi, HttpCompletionOption.ResponseHeadersRead, token)
        .ConfigureAwait(false);
      if (releaseResponse.StatusCode == HttpStatusCode.NotFound)
        throw new InstallError(
          "renderer_release_inaccessible",
          "Renderer releases could not be accessed. The repository may be private or unavailable. Ask for a public release, then try again."
        );
      releaseResponse.EnsureSuccessStatusCode();
      JObject release = JArray
        .Parse(await ReadTextAsync(releaseResponse, token).ConfigureAwait(false))
        .OfType<JObject>()
        .Where(candidate =>
          (bool?)candidate["draft"] != true
          && candidate["published_at"]?.Type != JTokenType.Null
          && candidate["published_at"] != null
        )
        .OrderByDescending(candidate => (DateTimeOffset)candidate["published_at"])
        .FirstOrDefault();
      if (release == null)
        throw new InstallError(
          "renderer_release_unavailable",
          "No official Renderer release has been published yet. Try again after a release is published."
        );
      var assets = release["assets"] as JArray;
      string Asset(string name) =>
        (string)assets?.FirstOrDefault(a => (string)a["name"] == name)?["browser_download_url"];
      string package = Asset(ModId + ".zip"),
        manifestUrl = Asset(ModId + ".download.json");
      if (package == null || manifestUrl == null || !IsReleaseAsset(package) || !IsReleaseAsset(manifestUrl))
        throw new InstallError(
          "renderer_release_invalid",
          "The Renderer release is missing its install package or verification file. Try again after the release is corrected."
        );
      using var manifestResponse = await client
        .GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, token)
        .ConfigureAwait(false);
      manifestResponse.EnsureSuccessStatusCode();
      JObject manifest = JObject.Parse(await ReadTextAsync(manifestResponse, token).ConfigureAwait(false));
      string sha = (string)manifest["sha256"],
        version = (string)manifest["version"];
      long expectedBytes = (long?)manifest["bytes"] ?? 0;
      if (
        (int?)manifest["schemaVersion"] != 1
        || (string)manifest["modId"] != ModId
        || (string)manifest["packageAsset"] != ModId + ".zip"
        || string.IsNullOrWhiteSpace(version)
        || sha?.Length != 64
        || !sha.All(Uri.IsHexDigit)
        || expectedBytes <= 0
        || expectedBytes > MaximumBytes
      )
        throw new InstallError(
          "renderer_release_invalid",
          "The Renderer verification file is invalid. Try again after the release is corrected."
        );

      Directory.CreateDirectory(Path.GetDirectoryName(_directory));
      staging = Path.Combine(
        Path.GetDirectoryName(_directory),
        ".tuf-renderer-install-" + Guid.NewGuid().ToString("N")
      );
      Directory.CreateDirectory(staging);
      string archive = Path.Combine(staging, "package.zip");
      Publish("downloading");
      using (
        var response = await client
          .GetAsync(package, HttpCompletionOption.ResponseHeadersRead, token)
          .ConfigureAwait(false)
      )
      {
        response.EnsureSuccessStatusCode();
        if (
          response.Content.Headers.ContentLength.HasValue
          && response.Content.Headers.ContentLength.Value != expectedBytes
        )
          throw new InvalidDataException(
            "The Renderer download size does not match the published package. Retry the download."
          );
        lock (_gate)
          _state.TotalBytes = expectedBytes;
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = File.Create(archive);
        var buffer = new byte[65536];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
        {
          total += count;
          if (total > expectedBytes)
            throw new InvalidDataException("The Renderer download exceeds its published size. Retry the download.");
          await output.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
          lock (_gate)
            _state.DownloadedBytes = total;
          NotifyChanged(progress: true);
        }
        if (total != expectedBytes)
          throw new InvalidDataException("The Renderer download is incomplete. Check your connection and retry.");
      }
      Publish("validating");
      string actualHash;
      using (var digest = SHA256.Create())
      using (var stream = File.OpenRead(archive))
        actualHash = BitConverter.ToString(digest.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
      if (!string.Equals(sha, actualHash, StringComparison.OrdinalIgnoreCase))
        throw new InstallError(
          "renderer_checksum_mismatch",
          "The Renderer download does not match the published checksum. Retry the download."
        );
      token.ThrowIfCancellationRequested();
      Publish("extracting");
      string extracted = Path.Combine(staging, ModId);
      Extract(archive, staging, token);
      string infoPath = Path.Combine(extracted, "Info.json");
      if (!File.Exists(infoPath) || new FileInfo(infoPath).Length > 65536)
        throw new InstallError(
          "renderer_release_invalid",
          "The Renderer package has no valid mod metadata. Try again after the release is corrected."
        );
      var info = JObject.Parse(File.ReadAllText(infoPath));
      if (
        (string)info["Id"] != ModId
        || (string)info["Version"] != version
        || !File.Exists(Path.Combine(extracted, ModId + ".dll"))
        || !File.Exists(Path.Combine(extracted, "LICENSE.md"))
      )
        throw new InstallError(
          "renderer_release_invalid",
          "The Renderer package is incomplete or has a different version. Try again after the release is corrected."
        );
      token.ThrowIfCancellationRequested();
      // Publish atomically. Never delete an existing mod directory or user settings.
      if (Directory.Exists(_directory))
        throw new InstallError(
          "renderer_directory_exists",
          "A Renderer folder already exists. Check the installed mod and restart the game; move an incomplete installation aside before retrying."
        );
      lock (_gate)
      {
        token.ThrowIfCancellationRequested();
        Directory.Move(extracted, _directory);
        _state.Version = version;
        _state.Status = "restart-required";
      }
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested)
    {
      Publish("cancelled");
    }
    catch (Exception error)
    {
      lock (_gate)
      {
        _state.Status = "failed";
        _state.ErrorCode =
          error is InstallError known ? known.Code
          : error is TaskCanceledException ? "renderer_download_timeout"
          : "renderer_install_failed";
        _state.Error =
          error is TaskCanceledException
            ? "The Renderer download timed out. Check your connection and try again."
            : error.Message;
      }
    }
    finally
    {
      NotifyChanged();
      if (staging != null)
        try
        {
          Directory.Delete(staging, true);
        }
        catch { }
    }
  }

  private static bool IsReleaseAsset(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
    && uri.Scheme == Uri.UriSchemeHttps
    && uri.Host == "github.com"
    && uri.AbsolutePath.StartsWith("/KGH1113/TUFReplay-Renderer/releases/download/", StringComparison.Ordinal);

  private static async Task<string> ReadTextAsync(HttpResponseMessage response, CancellationToken token)
  {
    using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using var output = new MemoryStream();
    await ManagedFfmpegInstaller.CopyBoundedAsync(input, output, 1024 * 1024, token).ConfigureAwait(false);
    return System.Text.Encoding.UTF8.GetString(output.ToArray());
  }

  private static void Extract(string archive, string staging, CancellationToken token)
  {
    using var zip = ZipFile.OpenRead(archive);
    long bytes = 0;
    var paths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (zip.Entries.Count > 4096)
      throw new InvalidDataException("The Renderer package contains too many files.");
    foreach (var entry in zip.Entries)
    {
      token.ThrowIfCancellationRequested();
      string name = entry.FullName;
      if (
        !name.StartsWith(ModId + "/", StringComparison.Ordinal)
        || name.Contains("\\")
        || name.Contains(":")
        || name.Split('/').Any(s => s == "." || s == "..")
        || !paths.Add(name)
        || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
      )
        throw new InvalidDataException(
          "The Renderer package contains an unsafe file path. Try again after the release is corrected."
        );
      string relative = name.Substring(ModId.Length + 1);
      if (
        relative.Equals("Settings.json", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("FFmpeg/", StringComparison.OrdinalIgnoreCase)
        || relative.StartsWith("jobs/", StringComparison.OrdinalIgnoreCase)
      )
        throw new InvalidDataException("The Renderer package includes user data that cannot be installed.");
      string target = Path.Combine(staging, name);
      if (name.EndsWith("/", StringComparison.Ordinal))
      {
        Directory.CreateDirectory(target);
        continue;
      }
      bytes += entry.Length;
      if (bytes > MaximumBytes * 2)
        throw new InvalidDataException("The extracted Renderer package is too large.");
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      using var input = entry.Open();
      using var output = File.Create(target);
      ManagedFfmpegInstaller.CopyBoundedAsync(input, output, entry.Length, token).GetAwaiter().GetResult();
    }
  }

  private void Publish(string status)
  {
    lock (_gate)
      _state.Status = status;
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

  private sealed class InstallError : IOException
  {
    public string Code { get; }

    public InstallError(string code, string message)
      : base(message)
    {
      Code = code;
    }
  }
}

public sealed class RendererInstallState
{
  public string Status { get; set; }
  public string Source { get; set; }
  public string InstallationDirectory { get; set; }
  public string Version { get; set; }
  public string Error { get; set; }
  public string ErrorCode { get; set; }
  public long DownloadedBytes { get; set; }
  public long? TotalBytes { get; set; }

  internal RendererInstallState Clone() => (RendererInstallState)MemberwiseClone();
}
