using System.Collections.Generic;

namespace TUFReplay.LocalServer.Routing;

public sealed class RouteMatch
{
  public RouteDefinition Route { get; }
  public Dictionary<string, string> Params { get; }

  public RouteMatch(RouteDefinition route, Dictionary<string, string> parameters)
  {
    Route = route;
    Params = parameters;
  }
}
