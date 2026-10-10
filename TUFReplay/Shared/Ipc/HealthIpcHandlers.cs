using TUFReplay.Shared.Ipc;

namespace TUFReplay.Shared.Ipc;

public static class HealthIpcHandlers
{
  public static object Get(JsonCommand request) => HealthResponseDto.Create();
}
