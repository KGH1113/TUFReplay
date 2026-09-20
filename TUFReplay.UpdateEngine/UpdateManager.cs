using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Shared.Build;

namespace TUFReplay.UpdateEngine;

internal sealed class UpdateManager
{
  private const string StableReleaseBaseUrl = "https://github.com/KGH1113/TUFReplay/releases/latest/download/";
  private const string ReleasesApiUrl = "https://api.github.com/repos/KGH1113/TUFReplay/releases?per_page=20";
  private const string AutoSubmissionManifestUrl =
    "https://tufreplay-auto.impl1113.dev/updates/auto-submission/latest.json";
  private const string AutoSubmissionHost = "tufreplay-auto.impl1113.dev";
  private const string InstalledManifestAsset = ".tufreplay-update.json";
  internal const string ManifestAsset = "TUFReplay.update.json";
  internal const string PackageAsset = "TUFReplay.zip";
  internal const long MaximumPackageBytes = 128L * 1024 * 1024;
  private const long MaximumExtractedBytes = 256L * 1024 * 1024;
  private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(20);
  private static readonly Regex VersionPattern = new(
    "\\\"Version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
    RegexOptions.CultureInvariant
  );

  private readonly string _installPath;
  private readonly string _runtimeRoot;
  private readonly string _versionsRoot;
  private readonly string _preferencesPath;
  private readonly string _stableReleaseBaseUrl;
  private readonly string _releasesApiUrl;
  private readonly string _autoSubmissionManifestUrl;
  private readonly string _buildFlavor;
  private readonly bool _allowLocalTestUrls;

  public UpdateManager(string installPath)
    : this(
      installPath,
      StableReleaseBaseUrl,
      ReleasesApiUrl,
      AutoSubmissionManifestUrl,
      TUFReplayBuildFlavor.Name,
      false
    ) { }

  internal UpdateManager(
    string installPath,
    string stableReleaseBaseUrl,
    string releasesApiUrl,
    bool allowLocalTestUrls = true
  )
    : this(
      installPath,
      stableReleaseBaseUrl,
      releasesApiUrl,
      AutoSubmissionManifestUrl,
      TUFReplayBuildFlavor.Name,
      allowLocalTestUrls
    ) { }

  internal UpdateManager(
    string installPath,
    string stableReleaseBaseUrl,
    string releasesApiUrl,
    string autoSubmissionManifestUrl,
    string buildFlavor,
    bool allowLocalTestUrls = true
  )
  {
    _installPath = Path.GetFullPath(installPath ?? throw new ArgumentNullException(nameof(installPath)));
    _runtimeRoot = Path.Combine(_installPath, "Runtime");
    _versionsRoot = Path.Combine(_runtimeRoot, "versions");
    _preferencesPath = Path.Combine(_installPath, "UpdateSettings.json");
    _stableReleaseBaseUrl = stableReleaseBaseUrl;
    _releasesApiUrl = releasesApiUrl;
    _autoSubmissionManifestUrl = autoSubmissionManifestUrl;
    _buildFlavor = buildFlavor;
    _allowLocalTestUrls = allowLocalTestUrls;
  }

  public UpdateResult Resolve(string currentVersion)
  {
    CleanupTemporaryArtifacts();
    using CancellationTokenSource timeout = new(NetworkTimeout);
    Task<UpdateResult> operation = ResolveAsync(currentVersion, timeout.Token);
    Task deadline = Task.Delay(NetworkTimeout);
    if (Task.WhenAny(operation, deadline).GetAwaiter().GetResult() != operation)
    {
      timeout.Cancel();
      _ = operation.ContinueWith(
        completed =>
        {
          if (completed.IsFaulted)
            _ = completed.Exception;
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default
      );
      throw new TimeoutException("TUFReplay update operations timed out after 20 seconds.");
    }
    return operation.GetAwaiter().GetResult();
  }

  private async Task<UpdateResult> ResolveAsync(string currentVersion, CancellationToken cancellationToken)
  {
    SemanticVersion current = SemanticVersion.Parse(currentVersion);
    using HttpClient client = CreateClient();
    ReleaseAssets release = await ResolveReleaseAsync(client, cancellationToken).ConfigureAwait(false);
    Func<Uri, bool> manifestResponseValidator =
      _buildFlavor == "auto-submission" ? uri => IsTrustedAutoSubmissionManifestUrl(uri?.AbsoluteUri) : null;
    ReleaseManifest manifest = ReleaseManifest.Parse(
      await DownloadTextAsync(client, release.ManifestUrl, 16 * 1024, cancellationToken, manifestResponseValidator)
        .ConfigureAwait(false)
    );

    if (!string.Equals(manifest.BuildFlavor, _buildFlavor, StringComparison.Ordinal))
      throw new InvalidDataException("The update manifest belongs to a different build flavor.");

    SemanticVersion available = SemanticVersion.Parse(manifest.Version);
    if (release.ExpectedVersion != null && available.CompareTo(SemanticVersion.Parse(release.ExpectedVersion)) != 0)
      throw new InvalidDataException("The release tag and update manifest versions do not match.");

    string existing = GetVersionDirectory(manifest.Version);
    int availability = available.CompareTo(current);
    bool existingValid = TryValidateRuntime(existing, manifest.Version, manifest.BuildFlavor);
    ReleaseManifest installedManifest = Directory.Exists(existing) ? ReadInstalledManifest(existing) : null;
    if (installedManifest != null && !HasSamePackageIdentity(installedManifest, manifest))
      throw new InvalidDataException("A TUFReplay version was published with different package content.");

    if (existingValid)
    {
      if (installedManifest == null && _buildFlavor == "auto-submission" && availability > 0)
        throw new InvalidDataException(
          "An existing auto-submission runtime has no package identity and cannot be replaced safely."
        );

      if (availability == 0)
      {
        if (installedManifest == null)
          WriteInstalledManifest(existing, manifest);
        return new UpdateResult { Outcome = UpdateOutcomes.None };
      }

      if (availability < 0)
        return new UpdateResult { Outcome = UpdateOutcomes.None };

      if (installedManifest != null)
      {
        return new UpdateResult
        {
          Outcome = UpdateOutcomes.Candidate,
          Version = manifest.Version,
          RuntimePath = existing,
          DependencyBootstrapPath = Path.Combine(existing, "AdofaiIpc.Bootstrap.dll"),
        };
      }
    }

    if (availability <= 0)
      return new UpdateResult { Outcome = UpdateOutcomes.None };

    Directory.CreateDirectory(_runtimeRoot);
    string packagePath = Path.Combine(_runtimeRoot, "download-" + Guid.NewGuid().ToString("N") + ".zip");
    try
    {
      string packageUrl = ResolvePackageUrl(release, manifest);
      Func<Uri, bool> packageResponseValidator =
        _buildFlavor == "auto-submission" ? uri => IsTrustedAutoSubmissionPackageUrl(uri, manifest.Version) : null;
      await DownloadFileAsync(
          client,
          packageUrl,
          packagePath,
          manifest.PackageBytes,
          cancellationToken,
          packageResponseValidator
        )
        .ConfigureAwait(false);
      VerifyChecksum(packagePath, manifest.PackageSha256);
      string runtimePath = InstallPackage(packagePath, manifest);
      return new UpdateResult
      {
        Outcome = UpdateOutcomes.Candidate,
        Version = manifest.Version,
        RuntimePath = runtimePath,
        DependencyBootstrapPath = Path.Combine(runtimePath, "AdofaiIpc.Bootstrap.dll"),
      };
    }
    finally
    {
      TryDeleteFile(packagePath);
    }
  }

  private async Task<ReleaseAssets> ResolveReleaseAsync(HttpClient client, CancellationToken cancellationToken)
  {
    if (_buildFlavor == "auto-submission")
    {
      if (!IsTrustedAutoSubmissionManifestUrl(_autoSubmissionManifestUrl))
        throw new InvalidDataException("The auto-submission update feed URL is invalid.");
      return new ReleaseAssets(null, _autoSubmissionManifestUrl, null);
    }

    if (_buildFlavor != "standard")
      throw new InvalidDataException("The TUFReplay build flavor is not supported by the updater.");

    if (!UpdatePreferences.Load(_preferencesPath).ReceiveBetaUpdates)
    {
      return new ReleaseAssets(null, _stableReleaseBaseUrl + ManifestAsset, _stableReleaseBaseUrl + PackageAsset);
    }

    string response = await DownloadTextAsync(client, _releasesApiUrl, 4 * 1024 * 1024, cancellationToken)
      .ConfigureAwait(false);
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
    return selected
      ?? throw new InvalidDataException("No stable or beta TUFReplay release contains the required update assets.");
  }

  private string ResolvePackageUrl(ReleaseAssets release, ReleaseManifest manifest)
  {
    if (_buildFlavor == "auto-submission")
    {
      string expectedRelativeUrl = "versions/" + manifest.Version + "/" + PackageAsset;
      if (!string.Equals(manifest.PackageUrl, expectedRelativeUrl, StringComparison.Ordinal))
        throw new InvalidDataException("The auto-submission package URL does not match its version.");
      if (
        !Uri.TryCreate(release.ManifestUrl, UriKind.Absolute, out Uri manifestUri)
        || !Uri.TryCreate(manifestUri, manifest.PackageUrl, out Uri packageUri)
        || !IsTrustedAutoSubmissionPackageUrl(packageUri, manifest.Version)
      )
        throw new InvalidDataException("The auto-submission package URL is not trusted.");
      return packageUri.AbsoluteUri;
    }

    if (release.PackageUrl == null || !IsTrustedReleaseUrl(release.PackageUrl))
      throw new InvalidDataException("The release package URL is not trusted.");
    return release.PackageUrl;
  }

  private ReleaseAssets ReadReleaseAssets(string version, JArray assets)
  {
    string manifestUrl = null;
    string packageUrl = null;
    foreach (JObject asset in assets?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
    {
      string name = asset.Value<string>("name");
      string url = asset.Value<string>("browser_download_url");
      if (!IsTrustedReleaseUrl(url))
        continue;
      if (name == ManifestAsset)
        manifestUrl = url;
      else if (name == PackageAsset)
        packageUrl = url;
    }
    return manifestUrl != null && packageUrl != null ? new ReleaseAssets(version, manifestUrl, packageUrl) : null;
  }

  private string InstallPackage(string packagePath, ReleaseManifest manifest)
  {
    string extractionRoot = Path.Combine(_runtimeRoot, "extract-" + Guid.NewGuid().ToString("N"));
    string target = GetVersionDirectory(manifest.Version);
    try
    {
      ExtractPackage(packagePath, extractionRoot);
      string source = ResolveContainedPath(extractionRoot, manifest.RuntimePath);
      ValidateRuntime(source, manifest.Version, manifest.BuildFlavor);
      WriteInstalledManifest(source, manifest);
      Directory.CreateDirectory(_versionsRoot);
      if (Directory.Exists(target))
        Directory.Delete(target, true);
      Directory.Move(source, target);
      return target;
    }
    finally
    {
      TryDeleteDirectory(extractionRoot);
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
      string destinationPath = Path.GetFullPath(
        Path.Combine(destinationRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar))
      );
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
      RestoreUnixPermissions(entry, destinationPath);
    }
  }

  private string GetVersionDirectory(string version)
  {
    return Path.Combine(_versionsRoot, SemanticVersion.Parse(version).ToString());
  }

  private static bool TryValidateRuntime(string directory, string version, string buildFlavor)
  {
    try
    {
      ValidateRuntime(directory, version, buildFlavor);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static void ValidateRuntime(string directory, string expectedVersion, string expectedBuildFlavor)
  {
    string assemblyPath = Path.Combine(directory, "TUFReplay.dll");
    string enginePath = Path.Combine(directory, "TUFReplay.UpdateEngine.dll");
    string infoPath = Path.Combine(directory, "Info.json");
    string dependencyBootstrapPath = Path.Combine(directory, "AdofaiIpc.Bootstrap.dll");
    string dependencyShimPath = Path.Combine(directory, "AdofaiIpc.DependencyShim.dll");
    string migrationPath = Path.Combine(directory, "AdofaiIpc.Migration.dll");
    string dependencyManifestPath = Path.Combine(directory, "AdofaiIpcBootstrap.json");
    if (
      !File.Exists(assemblyPath)
      || !File.Exists(enginePath)
      || !File.Exists(infoPath)
      || !File.Exists(dependencyBootstrapPath)
      || !File.Exists(dependencyShimPath)
      || !File.Exists(migrationPath)
      || !File.Exists(dependencyManifestPath)
    )
      throw new InvalidDataException("The update package does not contain a complete runtime.");
    string infoText = File.ReadAllText(infoPath);
    Match match = VersionPattern.Match(infoText);
    if (
      !match.Success
      || SemanticVersion.Parse(match.Groups[1].Value).CompareTo(SemanticVersion.Parse(expectedVersion)) != 0
    )
      throw new InvalidDataException("The packaged runtime version does not match the update manifest.");
    JObject info = JObject.Parse(infoText);
    string actualBuildFlavor = info.Value<string>("BuildFlavor") ?? "standard";
    if (actualBuildFlavor != "standard" && actualBuildFlavor != "auto-submission")
      throw new InvalidDataException("The packaged runtime build flavor is invalid.");
    if (!string.Equals(actualBuildFlavor, expectedBuildFlavor, StringComparison.Ordinal))
      throw new InvalidDataException("The packaged runtime build flavor does not match the update manifest.");
  }

  private static ReleaseManifest ReadInstalledManifest(string directory)
  {
    string path = Path.Combine(directory, InstalledManifestAsset);
    if (!File.Exists(path))
      return null;
    try
    {
      return ReleaseManifest.Parse(File.ReadAllText(path));
    }
    catch (Exception exception)
    {
      throw new InvalidDataException("The installed TUFReplay update metadata is invalid.", exception);
    }
  }

  private static bool HasSamePackageIdentity(ReleaseManifest installed, ReleaseManifest available)
  {
    return string.Equals(installed.Version, available.Version, StringComparison.Ordinal)
      && string.Equals(installed.BuildFlavor, available.BuildFlavor, StringComparison.Ordinal)
      && installed.PackageBytes == available.PackageBytes
      && string.Equals(installed.PackageSha256, available.PackageSha256, StringComparison.OrdinalIgnoreCase);
  }

  private static void WriteInstalledManifest(string directory, ReleaseManifest manifest)
  {
    JObject marker = new()
    {
      ["schemaVersion"] = manifest.SchemaVersion,
      ["version"] = manifest.Version,
      ["buildFlavor"] = manifest.BuildFlavor,
      ["packageAsset"] = manifest.PackageAsset,
      ["packageUrl"] = manifest.PackageUrl,
      ["packageBytes"] = manifest.PackageBytes,
      ["packageSha256"] = manifest.PackageSha256,
      ["runtimePath"] = manifest.RuntimePath,
    };
    string path = Path.Combine(directory, InstalledManifestAsset);
    string temporary = path + ".tmp";
    File.WriteAllText(temporary, marker.ToString() + Environment.NewLine);
    if (File.Exists(path))
      File.Delete(path);
    File.Move(temporary, path);
  }

  private void CleanupTemporaryArtifacts()
  {
    if (!Directory.Exists(_runtimeRoot))
      return;
    foreach (string file in Directory.GetFiles(_runtimeRoot, "download-*.zip"))
      TryDeleteFile(file);
    foreach (string directory in Directory.GetDirectories(_runtimeRoot, "extract-*"))
      TryDeleteDirectory(directory);
  }

  private static string ResolveContainedPath(string root, string relativePath)
  {
    string fullRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
    string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
      throw new InvalidDataException("The update manifest runtime path escapes the package.");
    return fullPath;
  }

  private bool IsTrustedReleaseUrl(string value)
  {
    if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
      return false;
    if (_allowLocalTestUrls && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
      return true;
    return uri.Scheme == Uri.UriSchemeHttps
      && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase);
  }

  private bool IsTrustedAutoSubmissionManifestUrl(string value)
  {
    if (
      !Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
      || uri.UserInfo.Length != 0
      || uri.Query.Length != 0
      || uri.Fragment.Length != 0
    )
      return false;
    if (_allowLocalTestUrls && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
      return string.Equals(uri.AbsolutePath, "/updates/auto-submission/latest.json", StringComparison.Ordinal);
    return uri.Scheme == Uri.UriSchemeHttps
      && uri.IsDefaultPort
      && string.Equals(uri.Host, AutoSubmissionHost, StringComparison.OrdinalIgnoreCase)
      && string.Equals(uri.AbsolutePath, "/updates/auto-submission/latest.json", StringComparison.Ordinal);
  }

  private bool IsTrustedAutoSubmissionPackageUrl(Uri uri, string version)
  {
    if (uri == null || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
      return false;
    string expectedPath = "/updates/auto-submission/versions/" + version + "/" + PackageAsset;
    if (!string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal))
      return false;
    if (_allowLocalTestUrls && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
      return true;
    return uri.Scheme == Uri.UriSchemeHttps
      && uri.IsDefaultPort
      && string.Equals(uri.Host, AutoSubmissionHost, StringComparison.OrdinalIgnoreCase);
  }

  private static HttpClient CreateClient()
  {
    HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-AutoUpdater/2.0");
    return client;
  }

  private static async Task<string> DownloadTextAsync(
    HttpClient client,
    string url,
    int maximumBytes,
    CancellationToken cancellationToken,
    Func<Uri, bool> responseValidator = null
  )
  {
    using HttpResponseMessage response = await client
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
      .ConfigureAwait(false);
    if (responseValidator != null && !responseValidator(response.RequestMessage?.RequestUri))
      throw new InvalidDataException("The update manifest response redirected to an untrusted URL.");
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
    long expectedBytes,
    CancellationToken cancellationToken,
    Func<Uri, bool> responseValidator = null
  )
  {
    using HttpResponseMessage response = await client
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
      .ConfigureAwait(false);
    if (responseValidator != null && !responseValidator(response.RequestMessage?.RequestUri))
      throw new InvalidDataException("The update package response redirected to an untrusted URL.");
    response.EnsureSuccessStatusCode();
    if (
      response.Content.Headers.ContentLength.HasValue
      && response.Content.Headers.ContentLength.Value != expectedBytes
    )
      throw new InvalidDataException("The update package size does not match its manifest.");
    using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using FileStream destination = new(
      destinationPath,
      FileMode.CreateNew,
      FileAccess.Write,
      FileShare.None,
      81920,
      true
    );
    long copied = await CopyWithLimitAsync(source, destination, MaximumPackageBytes, cancellationToken)
      .ConfigureAwait(false);
    if (copied != expectedBytes)
      throw new InvalidDataException("The downloaded package size does not match its manifest.");
  }

  private static async Task<long> CopyWithLimitAsync(
    Stream source,
    Stream destination,
    long maximumBytes,
    CancellationToken cancellationToken
  )
  {
    byte[] buffer = new byte[81920];
    long total = 0;
    while (true)
    {
      int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
      if (read == 0)
        return total;
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
    string actual = string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
    if (!string.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The update package checksum does not match its manifest.");
  }

  private static string EnsureTrailingSeparator(string path)
  {
    return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
      ? path
      : path + Path.DirectorySeparatorChar;
  }

  private static void RestoreUnixPermissions(ZipArchiveEntry entry, string destinationPath)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return;
    uint mode = (uint)(entry.ExternalAttributes >> 16) & 0x1FF;
    if (mode != 0 && Chmod(destinationPath, mode) != 0)
      throw new IOException("Could not restore packaged file permissions: " + destinationPath);
  }

  [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
  private static extern int Chmod(string path, uint mode);

  private static void TryDeleteFile(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch { }
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, true);
    }
    catch { }
  }
}
