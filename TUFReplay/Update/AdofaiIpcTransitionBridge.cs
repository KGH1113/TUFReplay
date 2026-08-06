using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityModManagerNet;

namespace TUFReplay.Update;

internal static class AdofaiIpcTransitionBridge
{
  private const string TargetVersion = "0.3.0";
  private const string ManifestUrl =
    "https://github.com/KGH1113/adofai-ipc/releases/download/v0.3.0/AdofaiIpc.update.json";
  private const long MaximumPackageBytes = 64L * 1024 * 1024;
  private const long MaximumExtractedBytes = 192L * 1024 * 1024;
  private const int MaximumEntries = 512;

  public static void TryStage(UnityModManager.ModEntry modEntry)
  {
    string assets = Path.Combine(PayloadPath(), "Transition");
    if (!Directory.Exists(assets)) return;
    try
    {
      EnsureAssets(assets);
      string launcherRoot = Path.Combine(modEntry.Path, "Launcher");
      Directory.CreateDirectory(launcherRoot);
      string transactionPath = Path.Combine(launcherRoot, "transition.json");
      TransitionState transaction = LoadTransaction(transactionPath);
      if (transaction.Phase == TransitionPhases.TufReplayCommitted) return;

      string modsPath = Directory.GetParent(Path.GetFullPath(modEntry.Path))?.FullName
        ?? throw new InvalidDataException("TUFReplay install directory has no Mods parent.");
      string adofaiIpcPath = Path.Combine(modsPath, "AdofaiIpc");
      if (transaction.Phase == TransitionPhases.Prepared)
      {
        modEntry.Info.DisplayName = Status(modEntry, "Preparing AdofaiIpc 0.3.0...");
        StageAdofaiIpc(adofaiIpcPath, modEntry);
        transaction.Phase = TransitionPhases.AdofaiIpcCommitted;
        SaveJson(transactionPath, transaction);
      }

      if (transaction.Phase == TransitionPhases.AdofaiIpcCommitted)
      {
        StageDependencyLauncher(modEntry.Path, new DirectoryInfo(PayloadPath()).Name, assets);
        transaction.Phase = TransitionPhases.TufReplayCommitted;
        SaveJson(transactionPath, transaction);
        modEntry.Logger.Log("[AutoUpdate] AdofaiIpc 0.3.0 and dependency bootstrap v2 are ready. Restart the game once to activate them.");
      }
    }
    catch (Exception exception)
    {
      modEntry.Logger.Warning("[AutoUpdate] The AdofaiIpc 0.3.0 transition was not committed; current versions remain active: " + exception);
    }
  }

  private static void StageAdofaiIpc(string installPath, UnityModManager.ModEntry owner)
  {
    string infoPath = Path.Combine(installPath, "Info.json");
    if (!File.Exists(infoPath))
      throw new FileNotFoundException("The legacy AdofaiIpc Info.json is missing.", infoPath);
    JObject installedInfo = JObject.Parse(File.ReadAllText(infoPath));
    string installedVersion = (string)installedInfo["Version"];
    if (!System.Version.TryParse(NumericVersion(installedVersion), out _))
      throw new InvalidDataException("The installed AdofaiIpc version is invalid.");

    string statePath = Path.Combine(installPath, "Update", "state.json");
    if (IsVersionedAdofaiIpcReady(installPath, installedInfo, statePath)) return;
    string updateRoot = Path.Combine(installPath, "Update");
    if (Directory.Exists(updateRoot))
      foreach (string pending in Directory.GetDirectories(updateRoot, "bridge-pending-*")) TryDelete(pending);
    string staging = Path.Combine(installPath, "Update", "bridge-pending-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging);
    try
    {
      using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
      client.DefaultRequestHeaders.UserAgent.ParseAdd("TUFReplay-AdofaiIpc-Bridge/1.0");
      string manifestJson = client.GetStringAsync(ManifestUrl).GetAwaiter().GetResult();
      BridgeManifest manifest = BridgeManifest.Parse(manifestJson);
      if (!string.Equals(manifest.Version, TargetVersion, StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("The transition manifest does not point to AdofaiIpc " + TargetVersion + ".");
      string packagePath = Path.Combine(staging, "AdofaiIpc.zip");
      Download(client, manifest.PackageUrl, packagePath, manifest.PackageSize);
      VerifyPackage(packagePath, manifest);
      InstallAdofaiIpcPackage(installPath, installedVersion, packagePath, staging, manifest);
    }
    finally
    {
      TryDelete(staging);
    }
  }

  internal static void InstallAdofaiIpcPackageForTests(
    string installPath,
    string packagePath,
    string manifestJson)
  {
    JObject installedInfo = JObject.Parse(File.ReadAllText(Path.Combine(installPath, "Info.json")));
    string installedVersion = (string)installedInfo["Version"];
    BridgeManifest manifest = BridgeManifest.Parse(manifestJson);
    string staging = Path.Combine(installPath, "Update", "test-pending-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging);
    try
    {
      VerifyPackage(packagePath, manifest);
      InstallAdofaiIpcPackage(installPath, installedVersion, packagePath, staging, manifest);
    }
    finally
    {
      TryDelete(staging);
    }
  }

  private static void InstallAdofaiIpcPackage(
    string installPath,
    string installedVersion,
    string packagePath,
    string staging,
    BridgeManifest manifest)
  {
    string packageRoot = Extract(packagePath, staging, manifest);
    string legacyRuntime = Path.Combine(installPath, "AdofaiIpc.dll");
    string preservedRuntime = Path.Combine(installPath, "Runtime", "versions", installedVersion, "AdofaiIpc.dll");
    if (File.Exists(legacyRuntime) && !File.Exists(preservedRuntime))
    {
      Directory.CreateDirectory(Path.GetDirectoryName(preservedRuntime));
      File.Copy(legacyRuntime, preservedRuntime, false);
    }
    if (!File.Exists(preservedRuntime))
      throw new InvalidDataException("The legacy AdofaiIpc runtime could not be preserved for rollback.");

    MoveVersion(packageRoot, installPath, "Launcher", manifest.Version, "AdofaiIpc.Launcher.dll");
    MoveVersion(packageRoot, installPath, "Runtime", manifest.Version, "AdofaiIpc.dll");
    CopyIfMissing(Path.Combine(packageRoot, "AdofaiIpc.Shim.dll"), Path.Combine(installPath, "AdofaiIpc.Shim.dll"));
    CopyIfMissing(Path.Combine(packageRoot, "AdofaiIpc.Bootstrap.dll"), Path.Combine(installPath, "AdofaiIpc.Bootstrap.v3.dll"));
    SaveJson(Path.Combine(installPath, "Update", "state.json"), new VersionState
    {
      Current = installedVersion,
      Previous = null,
      Trial = manifest.Version,
    });
    SaveText(
      Path.Combine(installPath, "Info.json"),
      File.ReadAllText(Path.Combine(packageRoot, "Info.json")));
  }

  internal static void StageDependencyLauncherForTests(string installPath, string runtimeVersion, string assets) =>
    StageDependencyLauncher(installPath, runtimeVersion, assets);

  private static void StageDependencyLauncher(string installPath, string runtimeVersion, string assets)
  {
    string launcherRoot = Path.Combine(installPath, "Launcher");
    string infoPath = Path.Combine(installPath, "Info.json");
    string launcherStatePath = Path.Combine(launcherRoot, "state.json");
    if (File.Exists(infoPath) && File.Exists(launcherStatePath))
    {
      JObject committedInfo = JObject.Parse(File.ReadAllText(infoPath));
      VersionState committedState = JsonConvert.DeserializeObject<VersionState>(File.ReadAllText(launcherStatePath));
      if (string.Equals((string)committedInfo["AssemblyName"], "TUFReplay.DependencyShim.dll", StringComparison.Ordinal) &&
          committedState != null && committedState.Current == "2" && string.IsNullOrWhiteSpace(committedState.Trial))
        return;
    }
    string legacy = Path.Combine(launcherRoot, "versions", "legacy", "AdofaiIpc.Bootstrap.dll");
    string legacyManifest = Path.Combine(launcherRoot, "versions", "legacy", "AdofaiIpcBootstrap.json");
    string currentRootBootstrap = Path.Combine(installPath, "AdofaiIpc.Bootstrap.dll");
    if (!File.Exists(legacy))
    {
      if (!File.Exists(currentRootBootstrap))
        throw new FileNotFoundException("The legacy dependency bootstrap is missing.", currentRootBootstrap);
      Directory.CreateDirectory(Path.GetDirectoryName(legacy));
      File.Copy(currentRootBootstrap, legacy, false);
    }
    if (!File.Exists(legacyManifest))
    {
      string currentManifest = Path.Combine(installPath, "AdofaiIpcBootstrap.json");
      if (!File.Exists(currentManifest))
        throw new FileNotFoundException("The legacy dependency manifest is missing.", currentManifest);
      File.Copy(currentManifest, legacyManifest, false);
    }

    string version2 = Path.Combine(launcherRoot, "versions", "2", "AdofaiIpc.Bootstrap.dll");
    CopyIfMissing(Path.Combine(assets, "AdofaiIpc.Bootstrap.dll"), version2);
    CopyIfMissing(
      Path.Combine(assets, "AdofaiIpcBootstrap.json"),
      Path.Combine(launcherRoot, "versions", "2", "AdofaiIpcBootstrap.json"));
    CopyIfMissing(Path.Combine(assets, "TUFReplay.DependencyShim.dll"), Path.Combine(installPath, "TUFReplay.DependencyShim.dll"));
    SaveJson(launcherStatePath, new VersionState
    {
      Current = "legacy",
      Previous = null,
      Trial = "2",
    });
    SaveText(
      Path.Combine(installPath, "AdofaiIpcBootstrap.json"),
      File.ReadAllText(Path.Combine(assets, "AdofaiIpcBootstrap.json")));

    JObject info = JObject.Parse(File.ReadAllText(infoPath));
    info["Version"] = runtimeVersion;
    info["AssemblyName"] = "TUFReplay.DependencyShim.dll";
    info["EntryMethod"] = "TUFReplay.DependencyShim.DependencyShim.Load";
    SaveText(infoPath, info.ToString(Formatting.Indented) + Environment.NewLine);
  }

  private static bool IsVersionedAdofaiIpcReady(string installPath, JObject info, string statePath)
  {
    if (!string.Equals((string)info["AssemblyName"], "AdofaiIpc.Shim.dll", StringComparison.Ordinal) ||
        !File.Exists(Path.Combine(installPath, "AdofaiIpc.Shim.dll")) || !File.Exists(statePath))
      return false;
    VersionState state = JsonConvert.DeserializeObject<VersionState>(File.ReadAllText(statePath));
    if (state == null || state.SchemaVersion != 1 || string.IsNullOrWhiteSpace(state.Current)) return false;
    return File.Exists(Path.Combine(installPath, "Runtime", "versions", state.Current, "AdofaiIpc.dll"));
  }

  private static void Download(HttpClient client, string url, string path, long expectedBytes)
  {
    using HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
    response.EnsureSuccessStatusCode();
    using Stream source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
    using FileStream destination = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    byte[] buffer = new byte[81920];
    long total = 0;
    while (true)
    {
      int read = source.Read(buffer, 0, buffer.Length);
      if (read == 0) break;
      total += read;
      if (total > expectedBytes || total > MaximumPackageBytes)
        throw new InvalidDataException("AdofaiIpc package exceeds its declared size.");
      destination.Write(buffer, 0, read);
    }
    if (total != expectedBytes) throw new InvalidDataException("AdofaiIpc package size does not match.");
  }

  private static void VerifyPackage(string path, BridgeManifest manifest)
  {
    if (new FileInfo(path).Length != manifest.PackageSize)
      throw new InvalidDataException("AdofaiIpc package size does not match.");
    using SHA256 sha256 = SHA256.Create();
    using FileStream stream = File.OpenRead(path);
    string actual = string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
    if (!actual.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("AdofaiIpc package checksum does not match.");
  }

  private static string Extract(string packagePath, string staging, BridgeManifest manifest)
  {
    string extraction = Path.Combine(staging, "extract");
    Directory.CreateDirectory(extraction);
    string root = EnsureSeparator(Path.GetFullPath(extraction));
    long total = 0;
    int count = 0;
    using ZipArchive archive = ZipFile.OpenRead(packagePath);
    foreach (ZipArchiveEntry entry in archive.Entries)
    {
      if (++count > MaximumEntries || entry.Length > MaximumPackageBytes || total > MaximumExtractedBytes - entry.Length)
        throw new InvalidDataException("AdofaiIpc package exceeds extraction limits.");
      total += entry.Length;
      string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
      if (!target.StartsWith(root, StringComparison.Ordinal))
        throw new InvalidDataException("Unsafe AdofaiIpc archive entry: " + entry.FullName);
      if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      using Stream source = entry.Open();
      using FileStream destination = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      CopyEntry(source, destination, entry.Length);
    }
    string packageRoot = Path.Combine(extraction, "AdofaiIpc");
    JObject info = JObject.Parse(File.ReadAllText(Path.Combine(packageRoot, "Info.json")));
    if (!string.Equals((string)info["Id"], "AdofaiIpc", StringComparison.Ordinal) ||
        !string.Equals((string)info["Version"], manifest.Version, StringComparison.OrdinalIgnoreCase) ||
        !File.Exists(Path.Combine(packageRoot, "AdofaiIpc.Shim.dll")) ||
        !File.Exists(Path.Combine(packageRoot, "Launcher", "versions", manifest.Version, "AdofaiIpc.Launcher.dll")) ||
        !File.Exists(Path.Combine(packageRoot, "Runtime", "versions", manifest.Version, "AdofaiIpc.dll")))
      throw new InvalidDataException("AdofaiIpc package contents do not match the transition manifest.");
    return packageRoot;
  }

  private static void MoveVersion(string packageRoot, string installPath, string kind, string version, string required)
  {
    string source = Path.Combine(packageRoot, kind, "versions", version);
    string target = Path.Combine(installPath, kind, "versions", version);
    if (Directory.Exists(target))
    {
      if (!File.Exists(Path.Combine(target, required)))
        throw new InvalidDataException("An incomplete AdofaiIpc version directory exists: " + target);
      return;
    }
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    Directory.Move(source, target);
  }

  private static void CopyEntry(Stream source, Stream destination, long expectedBytes)
  {
    byte[] buffer = new byte[81920];
    long total = 0;
    while (true)
    {
      int read = source.Read(buffer, 0, buffer.Length);
      if (read == 0) break;
      total += read;
      if (total > expectedBytes || total > MaximumPackageBytes)
        throw new InvalidDataException("AdofaiIpc archive entry exceeds its declared size.");
      destination.Write(buffer, 0, read);
    }
    if (total != expectedBytes) throw new InvalidDataException("AdofaiIpc archive entry size does not match.");
  }

  private static void CopyIfMissing(string source, string target)
  {
    if (File.Exists(target)) return;
    if (!File.Exists(source)) throw new FileNotFoundException("Transition asset is missing.", source);
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    File.Copy(source, target, false);
  }

  private static void EnsureAssets(string assets)
  {
    foreach (string file in new[]
             {
               "TUFReplay.DependencyShim.dll",
               "AdofaiIpc.Bootstrap.dll",
               "AdofaiIpcBootstrap.json",
             })
      if (!File.Exists(Path.Combine(assets, file)))
        throw new FileNotFoundException("A transition asset is missing.", Path.Combine(assets, file));
  }

  private static TransitionState LoadTransaction(string path)
  {
    if (!File.Exists(path))
    {
      TransitionState created = new() { Phase = TransitionPhases.Prepared };
      SaveJson(path, created);
      return created;
    }
    TransitionState state = JsonConvert.DeserializeObject<TransitionState>(File.ReadAllText(path));
    if (state == null || state.SchemaVersion != 1 || !TransitionPhases.IsValid(state.Phase))
      throw new InvalidDataException("TUFReplay transition state is invalid.");
    return state;
  }

  private static void SaveJson(string path, object value) =>
    SaveText(path, JsonConvert.SerializeObject(value, Formatting.Indented) + Environment.NewLine);

  private static void SaveText(string path, string content)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
    string backup = path + ".bak";
    File.WriteAllText(temporary, content, Encoding.UTF8);
    try
    {
      if (File.Exists(path))
      {
        if (File.Exists(backup)) File.Delete(backup);
        File.Replace(temporary, path, backup, true);
      }
      else File.Move(temporary, path);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }

  private static string PayloadPath() =>
    Path.GetDirectoryName(typeof(AdofaiIpcTransitionBridge).Assembly.Location)
    ?? throw new InvalidDataException("TUFReplay payload path is unavailable.");

  private static string NumericVersion(string version) =>
    string.IsNullOrWhiteSpace(version) ? string.Empty : version.Split(new[] { '-', '+', ' ' }, 2)[0];

  private static string EnsureSeparator(string path) =>
    path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
      ? path
      : path + Path.DirectorySeparatorChar;

  private static string Status(UnityModManager.ModEntry modEntry, string status) =>
    modEntry.Info.Id + " <color=grey>[" + status + "]</color>";

  private static void TryDelete(string path)
  {
    try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
  }

  private sealed class BridgeManifest
  {
    public int SchemaVersion { get; set; }
    public string Version { get; set; }
    public string PackageUrl { get; set; }
    public long PackageSize { get; set; }
    public string Sha256 { get; set; }

    public static BridgeManifest Parse(string json)
    {
      BridgeManifest manifest = JsonConvert.DeserializeObject<BridgeManifest>(json)
        ?? throw new InvalidDataException("AdofaiIpc transition manifest is invalid.");
      if (manifest.SchemaVersion != 1 || !System.Version.TryParse(manifest.Version, out _) ||
          manifest.Version.Contains("-") || manifest.PackageSize <= 0 || manifest.PackageSize > MaximumPackageBytes ||
          string.IsNullOrWhiteSpace(manifest.Sha256) || manifest.Sha256.Length != 64 ||
          manifest.Sha256.Any(character => !Uri.IsHexDigit(character)))
        throw new InvalidDataException("AdofaiIpc transition manifest fields are invalid.");
      if (!Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out Uri packageUri) ||
          packageUri.Scheme != Uri.UriSchemeHttps ||
          !string.Equals(packageUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
          !packageUri.AbsolutePath.StartsWith("/KGH1113/adofai-ipc/releases/", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("AdofaiIpc transition package URL is not official.");
      return manifest;
    }
  }

  private sealed class VersionState
  {
    public int SchemaVersion { get; set; } = 1;
    public string Current { get; set; }
    public string Previous { get; set; }
    public string Trial { get; set; }
  }

  private sealed class TransitionState
  {
    public int SchemaVersion { get; set; } = 1;
    public string Phase { get; set; }
  }

  private static class TransitionPhases
  {
    public const string Prepared = "Prepared";
    public const string AdofaiIpcCommitted = "AdofaiIpcCommitted";
    public const string TufReplayCommitted = "TUFReplayCommitted";

    public static bool IsValid(string phase) =>
      phase == Prepared || phase == AdofaiIpcCommitted || phase == TufReplayCommitted;
  }
}
