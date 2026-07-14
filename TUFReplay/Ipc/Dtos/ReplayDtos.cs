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
      Message = status.Message
    };
  }
}
