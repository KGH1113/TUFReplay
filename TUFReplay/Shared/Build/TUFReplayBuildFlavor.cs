namespace TUFReplay.Shared.Build;

internal static class TUFReplayBuildFlavor
{
#if TUFREPLAY_BUILD_FLAVOR_AUTO_SUBMISSION
  public static readonly string Name = "auto-submission";
  public const int AutoSubmissionProtocolVersion = 2;
#else
  public static readonly string Name = "standard";
  public const int AutoSubmissionProtocolVersion = 0;
#endif
}
