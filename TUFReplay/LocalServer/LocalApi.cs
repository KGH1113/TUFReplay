using TUFReplay.LocalServer.Controllers;
using TUFReplay.LocalServer.Routing;

namespace TUFReplay.LocalServer;

public static class LocalApi
{
  public static LocalRouter CreateRouter()
  {
    RouteRegistry registry = new RouteRegistry();

    registry.RegisterController<HealthController>();
    registry.RegisterController<RecordsController>();

    return new LocalRouter(registry);
  }
}
