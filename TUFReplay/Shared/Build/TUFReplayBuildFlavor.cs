namespace TUFReplay.Shared.Build;

// WebsiteUrl must match the HomePage that scripts/lib/build-metadata.sh writes to Info.json.
internal static class TUFReplayBuildFlavor
{
#if TUFREPLAY_BUILD_FLAVOR_AUTO_SUBMISSION
  public static readonly string Name = "auto-submission";
  public const int AutoSubmissionProtocolVersion = 1;
  public const string WebsiteUrl = "https://tufreplay-auto.impl1113.dev";
#else
  public static readonly string Name = "standard";
  public const int AutoSubmissionProtocolVersion = 0;
  public const string WebsiteUrl = "https://tufreplay.impl1113.dev";
#endif
}
