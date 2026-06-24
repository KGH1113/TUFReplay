using TUFReplay.LocalServer.Dtos;
using TUFReplay.LocalServer.Routing;

namespace TUFReplay.LocalServer.Controllers;

[Controller("/api")]
public sealed class HealthController
{
  [Get("/health")]
  public object Health()
  {
    return HealthResponseDto.Create();
  }
}
