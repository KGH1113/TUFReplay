using TUFReplay.Application.Replay;

namespace TUFReplay.Ipc.Dtos;

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

public sealed class ReplayLevelFilePickerStatusDto
{
  public string OperationId;
  public string RunId;
  public string State;
  public string LevelPath;
  public string ErrorCode;
  public string Message;

  public static ReplayLevelFilePickerStatusDto From(ReplayLevelFilePickerStatus status)
  {
    return new ReplayLevelFilePickerStatusDto
    {
      OperationId = status.OperationId,
      RunId = status.RunId,
      State = status.State,
      LevelPath = status.LevelPath,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
    };
  }
}
