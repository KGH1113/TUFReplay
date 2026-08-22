using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;

namespace TUFReplay.Replay.Ipc;

public sealed class ReplayPlaybackStatusDto
{
  public string OperationId;
  public string RunId;
  public string State;
  public string ErrorCode;
  public string Message;

  public static ReplayPlaybackStatusDto From(ReplayPlaybackStatus status)
  {
    status ??= ReplayPlaybackStatus.Idle();
    return new ReplayPlaybackStatusDto
    {
      OperationId = status.OperationId,
      RunId = status.RunId,
      State = status.State,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
    };
  }
}

public sealed class ReplayLevelFilePickerResultDto
{
  public string OperationId;
  public string RunId;
  public string Outcome;
  public string LevelPath;
  public string ErrorCode;
  public string Message;

  public static ReplayLevelFilePickerResultDto From(ReplayLevelFilePickerResult result)
  {
    return new ReplayLevelFilePickerResultDto
    {
      OperationId = result.OperationId,
      RunId = result.RunId,
      Outcome = result.Outcome,
      LevelPath = result.LevelPath,
      ErrorCode = result.ErrorCode,
      Message = result.Message,
    };
  }
}
