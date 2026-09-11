namespace TUFReplay.Replay.Models;

public static class ReplayUnavailableReasons
{
  public const string LegacyEngine = "legacy_engine";
  public const string CaptureIncomplete = "capture_incomplete";
  public const string UnsupportedEngine = "unsupported_engine";
  public const string UnsupportedFormat = "unsupported_format";
  public const string PayloadMissing = "payload_missing";
}
