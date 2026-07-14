namespace TUFReplay.Application.Replay;

public sealed class ReplayPlaybackStatus
{
  public string OperationId;
  public string RunId;
  public string State;
  public string ErrorCode;
  public string Message;

  public static ReplayPlaybackStatus Idle()
  {
    return new ReplayPlaybackStatus { State = ReplayPlaybackStates.Idle };
  }
}

public static class ReplayPlaybackStates
{
  public const string Idle = "idle";
  public const string Preparing = "preparing";
  public const string OpeningLevel = "opening_level";
  public const string WaitingForFocus = "waiting_for_focus";
  public const string Starting = "starting";
  public const string Playing = "playing";
  public const string ReturningToEditor = "returning_to_editor";
  public const string Completed = "completed";
  public const string Cancelled = "cancelled";
  public const string Error = "error";
}
