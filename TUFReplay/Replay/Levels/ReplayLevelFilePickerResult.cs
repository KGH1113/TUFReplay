namespace TUFReplay.Replay.Levels;

public sealed class ReplayLevelFilePickerResult
{
  public string OperationId;
  public string RunId;
  public string Outcome;
  public string LevelPath;
  public string ErrorCode;
  public string Message;
}

public static class ReplayLevelFilePickerOutcomes
{
  public const string Picking = "picking";
  public const string Selected = "selected";
  public const string Mismatch = "mismatch";
  public const string Cancelled = "cancelled";
  public const string Error = "error";
}
