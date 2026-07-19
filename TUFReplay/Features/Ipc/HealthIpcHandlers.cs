using AdofaiIpc.Core;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class HealthIpcHandlers
{
  public static object Get(IpcRequest request) => HealthResponseDto.Create();
}
