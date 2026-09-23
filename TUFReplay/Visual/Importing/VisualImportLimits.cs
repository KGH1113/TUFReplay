namespace TUFReplay.Visual.Importing;

public static class VisualImportLimits
{
  public const int MaxRequestBytes = 64 * 1024 * 1024;
  public const int MaxBundleBytes = 64 * 1024 * 1024;

  // Standalone JipperKeyViewer's normalized CJK font is 37,756,808 bytes.
  public const int MaxDecodedAssetBytes = 48 * 1024 * 1024;
  public const long MaxDecodedAssetBytesTotal = 128L * 1024 * 1024;
  public const int MaxAssets = 4096;
  public const int MaxJsonNodes = 32768;
  public const int MaxJsonDepth = 64;

  // Keep the local bound aligned with the API's persisted preset-name limit.
  public const int MaxNameLength = 80;
  public const int MaxFileBytes = 64 * 1024 * 1024;
}
