using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Importing.Security;
using TUFReplay.Visual.Infrastructure.FileSystem;

namespace TUFReplay.Visual.Infrastructure.Discovery;

public sealed class VisualSourceLocator : IVisualSourceLocator
{
  private readonly IVisualFileSystem _fileSystem;
  private readonly VisualSourceRoots _roots;

  public VisualSourceLocator(IVisualFileSystem fileSystem, VisualSourceRoots roots)
  {
    _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    _roots = roots ?? throw new ArgumentNullException(nameof(roots));
  }

  public VisualSourceInstallation Find(VisualSource source)
  {
    IReadOnlyList<string> roots;
    switch (source)
    {
      case VisualSource.JipperResourcePack:
        roots = _roots.JipperResourcePack;
        break;
      case VisualSource.JipperKeyviewer:
        roots = _roots.JipperKeyviewer;
        break;
      case VisualSource.ImplResourcePack:
        roots = _roots.ImplResourcePack;
        break;
      default:
        return null;
    }
    VisualSourceInstallation best = null;
    foreach (string root in roots)
    {
      if (
        string.IsNullOrWhiteSpace(root)
        || (_fileSystem is PhysicalVisualFileSystem && !VisualPathPolicy.IsTrustedRoot(root))
      )
        continue;
      string version = ReadVersion(root, source);
      if (string.IsNullOrWhiteSpace(version) || !HasConfiguration(root, source))
        continue;
      if (best == null || CompareVersions(version, best.Version) > 0)
        best = new VisualSourceInstallation(source, root, version);
    }
    return best;
  }

  private bool HasConfiguration(string root, VisualSource source)
  {
    if (source == VisualSource.ImplResourcePack)
      return true; // The overlay has a fixed layout; unsaved settings use source defaults.
    if (source == VisualSource.JipperKeyviewer)
      return SafeExists(root, Path.Combine(root, "config", "settings.json"));
    if (source == VisualSource.JipperResourcePack)
      return SafeExists(root, Path.Combine(root, "Settings.json"));
    return false;
  }

  private string ReadVersion(string root, VisualSource source)
  {
    string infoPath = Path.Combine(root, "Info.json");
    if (SafeExists(root, infoPath) && _fileSystem.FileLength(infoPath) <= 65536)
    {
      try
      {
        JObject info = JObject.Parse(_fileSystem.ReadAllText(infoPath));
        string id = ReadString(info, "Id");
        bool validId =
          source == VisualSource.JipperKeyviewer
            ? string.Equals(id, "JipperKeyViewer", StringComparison.OrdinalIgnoreCase)
          : source == VisualSource.ImplResourcePack
            ? string.Equals(id, "ImplResourcePack", StringComparison.OrdinalIgnoreCase)
          : source == VisualSource.JipperResourcePack
            && string.Equals(id, "JipperResourcePack", StringComparison.OrdinalIgnoreCase);
        if (!validId)
          return null;
        return ReadString(info, "Version");
      }
      catch (Exception)
      {
        return null;
      }
    }

    return null;
  }

  private static string ReadString(JObject value, string name)
  {
    if (value == null)
      return null;
    foreach (JProperty property in value.Properties())
      if (
        string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
        && property.Value.Type == JTokenType.String
      )
        return property.Value.Value<string>();
    return null;
  }

  private bool SafeExists(string root, string path)
  {
    return VisualPathPolicy.IsContained(root, path) && _fileSystem.FileExists(path);
  }

  private static int CompareVersions(string left, string right)
  {
    Version leftVersion;
    Version rightVersion;
    if (
      Version.TryParse(NormalizeVersion(left), out leftVersion)
      && Version.TryParse(NormalizeVersion(right), out rightVersion)
    )
      return leftVersion.CompareTo(rightVersion);
    return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
  }

  private static string NormalizeVersion(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return "0.0.0";
    int suffix = value.IndexOf('-', StringComparison.Ordinal);
    return suffix >= 0 ? value.Substring(0, suffix) : value;
  }
}
