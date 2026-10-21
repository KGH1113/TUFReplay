namespace TUFReplay.Replay.Models;

public static class ReplayUnavailableReasons
{
  public const string LegacyEngine = "legacy_engine";
  public const string CaptureIncomplete = "capture_incomplete";
  public const string InputQueueOverflow = "input_queue_overflow";
  public const string InputTapTimeout = "input_tap_timeout";
  public const string InputTapDisabled = "input_tap_disabled";
  public const string InputEventDelayed = "input_event_delayed";
  public const string InputSourceStopped = "input_source_stopped";
  public const string InputPermissionDenied = "input_permission_denied";
  public const string InputStartFailed = "input_start_failed";
  public const string InputReadFailed = "input_read_failed";
  public const string UnsupportedEngine = "unsupported_engine";
  public const string UnsupportedFormat = "unsupported_format";
  public const string PayloadMissing = "payload_missing";
}
