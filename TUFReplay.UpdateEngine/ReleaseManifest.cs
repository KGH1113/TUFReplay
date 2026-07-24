using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TUFReplay.UpdateEngine;

internal sealed class ReleaseManifest
{
  public const int CurrentSchemaVersion = 1;

  public int SchemaVersion { get; private set; }
  public string Version { get; private set; }
  public string PackageAsset { get; private set; }
  public long PackageBytes { get; private set; }
  public string PackageSha256 { get; private set; }
  public string RuntimePath { get; private set; }

  public static ReleaseManifest Parse(string json)
  {
    JObject root = JObject.Parse(json);
    ReleaseManifest manifest = new()
    {
      SchemaVersion = root.Value<int?>("schemaVersion") ?? 0,
      Version = root.Value<string>("version"),
      PackageAsset = root.Value<string>("packageAsset"),
      PackageBytes = root.Value<long?>("packageBytes") ?? 0,
      PackageSha256 = root.Value<string>("packageSha256"),
      RuntimePath = root.Value<string>("runtimePath"),
    };

    if (manifest.SchemaVersion != CurrentSchemaVersion)
      throw new InvalidDataException("The update manifest schema is not supported.");
    SemanticVersion.Parse(manifest.Version);
    if (!string.Equals(manifest.PackageAsset, UpdateManager.PackageAsset, StringComparison.Ordinal))
      throw new InvalidDataException("The update manifest references an unexpected package asset.");
    if (manifest.PackageBytes <= 0 || manifest.PackageBytes > UpdateManager.MaximumPackageBytes)
      throw new InvalidDataException("The update manifest package size is invalid.");
    if (!IsSha256(manifest.PackageSha256))
      throw new InvalidDataException("The update manifest checksum is invalid.");
    if (string.IsNullOrWhiteSpace(manifest.RuntimePath) || Path.IsPathRooted(manifest.RuntimePath))
      throw new InvalidDataException("The update manifest runtime path is invalid.");
    return manifest;
  }

  private static bool IsSha256(string value)
  {
    if (value == null || value.Length != 64)
      return false;
    foreach (char character in value)
    {
      if (!Uri.IsHexDigit(character))
        return false;
    }
    return true;
  }
}
