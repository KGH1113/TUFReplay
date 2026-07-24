namespace TUFReplay.UpdateEngine;

internal sealed class ReleaseAssets
{
  public ReleaseAssets(string expectedVersion, string manifestUrl, string packageUrl)
  {
    ExpectedVersion = expectedVersion;
    ManifestUrl = manifestUrl;
    PackageUrl = packageUrl;
  }

  public string ExpectedVersion { get; }
  public string ManifestUrl { get; }
  public string PackageUrl { get; }
}
