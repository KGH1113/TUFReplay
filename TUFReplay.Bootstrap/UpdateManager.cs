using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Bootstrap;

internal sealed class UpdateManager
{
  private const string ReleaseBaseUrl =
    "https://github.com/KGH1113/TUFReplay/releases/latest/download/";
  private const string ReleasesApiUrl =
    "https://api.github.com/repos/KGH1113/TUFReplay/releases?per_page=20";
  private const string VersionAsset = "TUFReplay.version";
  private const string PackageAsset = "TUFReplay.zip";
  private const string ChecksumAsset = "TUFReplay.zip.sha256";
  private const string PayloadAssemblyName = "TUFReplay.dll";
  private const long MaximumPackageBytes = 128L * 1024 * 1024;
  private const long MaximumExtractedBytes = 256L * 1024 * 1024;
  private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(20);
  private static readonly Regex VersionPattern = new(
    "\\\"Version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
    RegexOptions.CultureInvariant);

  private readonly string _cacheRoot;
  private readonly string _versionsRoot;
  private readonly string _activeVersionPath;
  private readonly string _preferencesPath;

  public UpdateManager(string modPath)
  {
    _cacheRoot = Path.Combine(modPath, ".tufreplay-update");
    _versionsRoot = Path.Combine(_cacheRoot, "versions");
    _activeVersionPath = Path.Combine(_cacheRoot, "active.version");
    _preferencesPath = Path.Combine(modPath, "UpdateSettings.json");
  }

  public PayloadCandidate GetActiveCandidate(PayloadCandidate installed)
  {
    try
    {
      if (!File.Exists(_activeVersionPath))
        return null;

      string version = File.ReadAllText(_activeVersionPath).Trim();
      PayloadCandidate candidate = GetCachedCandidate(version);
      return candidate != null && CompareVersions(candidate.Version, installed.Version) > 0
        ? candidate
        : null;
    }
    catch
    {
      return null;
    }
  }

  public PayloadCandidate GetLatestCandidate(PayloadCandidate current)
  {
    CancellationTokenSource timeout = new(NetworkTimeout);
    Task<PayloadCandidate> operation = GetLatestCandidateAsync(current, timeout.Token);
    Task deadline = Task.Delay(NetworkTimeout);

    if (Task.WhenAny(operation, deadline).GetAwaiter().GetResult() != operation)
    {
      timeout.Cancel();
      _ = operation.ContinueWith(
        completed =>
        {
          if (completed.IsFaulted)
            _ = completed.Exception;
          timeout.Dispose();
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
      throw new TimeoutException("TUFReplay update network operations timed out after 20 seconds.");
    }

    try
    {
      return operation.GetAwaiter().GetResult();
    }
    catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
    {
      throw new TimeoutException("TUFReplay update network operations timed out after 20 seconds.", exception);
    }
    finally
    {
      timeout.Dispose();
    }
  }

  public void MarkActive(PayloadCandidate candidate)
  {
    Directory.CreateDirectory(_cacheRoot);
    string temporaryPath = _activeVersionPath + ".tmp";
    File.WriteAllText(temporaryPath, candidate.Version + Environment.NewLine, Encoding.UTF8);

    if (File.Exists(_activeVersionPath))
      File.Delete(_activeVersionPath);
    File.Move(temporaryPath, _activeVersionPath);
  }

  private async Task<PayloadCandidate> GetLatestCandidateAsync(
    PayloadCandidate current,
    CancellationToken cancellationToken)
  {
    using HttpClient client = CreateClient();
    ReleaseAssets release = await ResolveReleaseAsync(client, cancellationToken).ConfigureAwait(false);
    string latestVersion = (await DownloadTextAsync(
      client,
      release.VersionUrl,
      256,
      cancellationToken).ConfigureAwait(false)).Trim();
    cancellationToken.ThrowIfCancellationRequested();

    SemanticVersion.Parse(latestVersion);
    if (release.ExpectedVersion != null &&
        CompareVersions(release.ExpectedVersion, latestVersion) != 0)
      throw new InvalidDataException("The release tag and version asset do not match.");

    if (CompareVersions(latestVersion, current.Version) <= 0)
      return null;

    PayloadCandidate cached = GetCachedCandidate(latestVersion);
    if (cached != null)
      return cached;

    string checksum = ParseChecksum(await DownloadTextAsync(
      client,
      release.ChecksumUrl,
      4096,
      cancellationToken).ConfigureAwait(false));
    cancellationToken.ThrowIfCancellationRequested();

    Directory.CreateDirectory(_cacheRoot);
    string packagePath = Path.Combine(_cacheRoot, "download-" + Guid.NewGuid().ToString("N") + ".zip");

    try
    {
      await DownloadFileAsync(client, release.PackageUrl, packagePath, cancellationToken).ConfigureAwait(false);
      cancellationToken.ThrowIfCancellationRequested();
      VerifyChecksum(packagePath, checksum);
      cancellationToken.ThrowIfCancellationRequested();
      return InstallPackage(packagePath, latestVersion);
    }
    finally
    {
      TryDeleteFile(packagePath);
    }
  }

  private async Task<ReleaseAssets> ResolveReleaseAsync(
    HttpClient client,
    CancellationToken cancellationToken)
  {
    UpdatePreferences preferences = UpdatePreferences.Load(_preferencesPath);
    if (!preferences.ReceiveBetaUpdates)
    {
      return new ReleaseAssets(
        null,
        ReleaseBaseUrl + VersionAsset,
        ReleaseBaseUrl + PackageAsset,
        ReleaseBaseUrl + ChecksumAsset);
    }

    string response = await DownloadTextAsync(
      client,
      ReleasesApiUrl,
      4 * 1024 * 1024,
      cancellationToken).ConfigureAwait(false);
    cancellationToken.ThrowIfCancellationRequested();

    ReleaseAssets selected = null;
    SemanticVersion selectedVersion = null;
    foreach (JObject release in JArray.Parse(response).OfType<JObject>())
    {
      if (release.Value<bool?>("draft") == true)
        continue;

      string tag = release.Value<string>("tag_name");
      if (!SemanticVersion.TryParse(tag, out SemanticVersion version))
        continue;

      ReleaseAssets assets = ReadReleaseAssets(tag, release["assets"] as JArray);
      if (assets == null || selectedVersion != null && version.CompareTo(selectedVersion) <= 0)
        continue;

      selected = assets;
      selectedVersion = version;
    }

    return selected ?? throw new InvalidDataException(
      "No stable or beta TUFReplay release contains all required update assets.");
  }

  private static ReleaseAssets ReadReleaseAssets(string version, JArray assets)
  {
    if (assets == null)
      return null;

    string versionUrl = null;
    string packageUrl = null;
    string checksumUrl = null;
    foreach (JObject asset in assets.OfType<JObject>())
    {
      string name = asset.Value<string>("name");
      string url = asset.Value<string>("browser_download_url");
      if (!IsTrustedReleaseUrl(url))
        continue;

      switch (name)
      {
        case VersionAsset:
          versionUrl = url;
          break;
        case PackageAsset:
          packageUrl = url;
          break;
        case ChecksumAsset:
          checksumUrl = url;
          break;
      }
    }

    return versionUrl != null && packageUrl != null && checksumUrl != null
      ? new ReleaseAssets(version, versionUrl, packageUrl, checksumUrl)
      : null;
  }

  private static bool IsTrustedReleaseUrl(string value)
  {
    return Uri.TryCreate(value, UriKind.Absolute, out Uri uri) &&
           uri.Scheme == Uri.UriSchemeHttps &&
           string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
  }

  private PayloadCandidate InstallPackage(string packagePath, string version)
  {
    string temporaryRoot = Path.Combine(_cacheRoot, "extract-" + Guid.NewGuid().ToString("N"));
    string targetDirectory = GetVersionDirectory(version);

    try
    {
      ExtractPackage(packagePath, temporaryRoot);
      string extractedMod = Path.Combine(temporaryRoot, "TUFReplay");
      ValidatePayload(extractedMod, version);

      Directory.CreateDirectory(_versionsRoot);
      if (Directory.Exists(targetDirectory))
        Directory.Delete(targetDirectory, recursive: true);
      Directory.Move(extractedMod, targetDirectory);

      return new PayloadCandidate(version, Path.Combine(targetDirectory, PayloadAssemblyName));
    }
    finally
    {
      TryDeleteDirectory(temporaryRoot);
    }
  }

  private static void ExtractPackage(string packagePath, string destinationRoot)
  {
    Directory.CreateDirectory(destinationRoot);
    string rootPrefix = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));

    using FileStream package = File.OpenRead(packagePath);
    using ZipArchive archive = new(package, ZipArchiveMode.Read);
    long extractedBytes = 0;
    foreach (ZipArchiveEntry entry in archive.Entries)
    {
      extractedBytes = checked(extractedBytes + entry.Length);
      if (extractedBytes > MaximumExtractedBytes)
        throw new InvalidDataException("The extracted update package is too large.");

      string destinationPath = Path.GetFullPath(Path.Combine(
        destinationRoot,
        entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

      if (!destinationPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        throw new InvalidDataException("The update package contains an unsafe path.");

      if (string.IsNullOrEmpty(entry.Name))
      {
        Directory.CreateDirectory(destinationPath);
        continue;
      }

      Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
      using Stream source = entry.Open();
      using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      source.CopyTo(destination);
    }
  }

  private PayloadCandidate GetCachedCandidate(string version)
  {
    try
    {
      string directory = GetVersionDirectory(version);
      ValidatePayload(directory, version);
      return new PayloadCandidate(version, Path.Combine(directory, PayloadAssemblyName));
    }
    catch
    {
      return null;
    }
  }

  private string GetVersionDirectory(string version)
  {
    SemanticVersion parsed = SemanticVersion.Parse(version);
    return Path.Combine(_versionsRoot, parsed.ToString());
  }

  private static void ValidatePayload(string directory, string expectedVersion)
  {
    string assemblyPath = Path.Combine(directory, PayloadAssemblyName);
    string infoPath = Path.Combine(directory, "Info.json");
    if (!File.Exists(assemblyPath) || !File.Exists(infoPath))
      throw new InvalidDataException("The update package does not contain a complete TUFReplay payload.");

    Match match = VersionPattern.Match(File.ReadAllText(infoPath));
    if (!match.Success || CompareVersions(match.Groups[1].Value, expectedVersion) != 0)
      throw new InvalidDataException("The update package version does not match the release version.");
  }

  private static HttpClient CreateClient()
  {
    HttpClient client = new();
    client.Timeout = Timeout.InfiniteTimeSpan;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-AutoUpdater/1.0");
    return client;
  }

  private static async Task<string> DownloadTextAsync(
    HttpClient client,
    string url,
    int maximumBytes,
    CancellationToken cancellationToken)
  {
    using HttpResponseMessage response = await client.GetAsync(
      url,
      HttpCompletionOption.ResponseHeadersRead,
      cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using MemoryStream buffer = new();
    await CopyWithLimitAsync(stream, buffer, maximumBytes, cancellationToken).ConfigureAwait(false);
    return Encoding.UTF8.GetString(buffer.ToArray());
  }

  private static async Task DownloadFileAsync(
    HttpClient client,
    string url,
    string destinationPath,
    CancellationToken cancellationToken)
  {
    using HttpResponseMessage response = await client.GetAsync(
      url,
      HttpCompletionOption.ResponseHeadersRead,
      cancellationToken).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    if (response.Content.Headers.ContentLength > MaximumPackageBytes)
      throw new InvalidDataException("The update package is too large.");

    using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using FileStream destination = new(
      destinationPath,
      FileMode.CreateNew,
      FileAccess.Write,
      FileShare.None,
      81920,
      useAsync: true);
    await CopyWithLimitAsync(source, destination, MaximumPackageBytes, cancellationToken).ConfigureAwait(false);
  }

  private static async Task CopyWithLimitAsync(
    Stream source,
    Stream destination,
    long maximumBytes,
    CancellationToken cancellationToken)
  {
    byte[] buffer = new byte[81920];
    long total = 0;
    while (true)
    {
      int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
      if (read == 0)
        break;

      total += read;
      if (total > maximumBytes)
        throw new InvalidDataException("The downloaded update asset is too large.");

      await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
    }
  }

  private static void VerifyChecksum(string path, string expectedChecksum)
  {
    using SHA256 sha256 = SHA256.Create();
    using FileStream stream = File.OpenRead(path);
    string actual = ToHex(sha256.ComputeHash(stream));
    if (!string.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The TUFReplay update checksum does not match.");
  }

  private static string ParseChecksum(string value)
  {
    string trimmed = value.Trim();
    if (trimmed.Length < 64)
      throw new InvalidDataException("The TUFReplay checksum asset is invalid.");

    string checksum = trimmed.Substring(0, 64);
    for (int index = 0; index < checksum.Length; index++)
    {
      if (!Uri.IsHexDigit(checksum[index]))
        throw new InvalidDataException("The TUFReplay checksum asset is invalid.");
    }
    return checksum;
  }

  private static int CompareVersions(string left, string right)
  {
    return SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));
  }

  private static string ToHex(byte[] bytes)
  {
    StringBuilder result = new(bytes.Length * 2);
    foreach (byte value in bytes)
      result.Append(value.ToString("x2"));
    return result.ToString();
  }

  private static string EnsureTrailingSeparator(string path)
  {
    return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
      ? path
      : path + Path.DirectorySeparatorChar;
  }

  private static void TryDeleteFile(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch
    {
      // Cleanup failure must not prevent loading the mod.
    }
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, recursive: true);
    }
    catch
    {
      // Cleanup failure must not prevent loading the mod.
    }
  }
}
