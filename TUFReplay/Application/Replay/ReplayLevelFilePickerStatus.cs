namespace TUFReplay.Application.Replay;

public sealed class ReplayLevelFilePickerStatus
{
  public string OperationId;
  public string RunId;
  public string State;
  public string LevelPath;
  public string ErrorCode;
  public string Message;
}

public static class ReplayLevelFilePickerStates
{
  public const string Picking = "picking";
  public const string Selected = "selected";
  public const string Cancelled = "cancelled";
  public const string Error = "error";
}
