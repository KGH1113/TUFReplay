namespace TUFReplay.Bootstrap;

internal sealed class ReleaseAssets
{
  public ReleaseAssets(
    string expectedVersion,
    string versionUrl,
    string packageUrl,
    string checksumUrl)
  {
    ExpectedVersion = expectedVersion;
    VersionUrl = versionUrl;
    PackageUrl = packageUrl;
    ChecksumUrl = checksumUrl;
  }

  public string ExpectedVersion { get; }
  public string VersionUrl { get; }
  public string PackageUrl { get; }
  public string ChecksumUrl { get; }
}
