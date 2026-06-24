using System.Reflection;

namespace TUFReplay.LocalServer.Routing;

public sealed class RouteDefinition
{
  public string Method { get; }
  public string Path { get; }
  public string[] Segments { get; }
  public object Controller { get; }
  public MethodInfo Handler { get; }

  public RouteDefinition(string method, string path, object controller, MethodInfo handler)
  {
    Method = method;
    Path = Normalize(path);
    Segments = Split(Path);
    Controller = controller;
    Handler = handler;
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrEmpty(path) || path == "/") return "/";
    path = path.StartsWith("/") ? path : "/" + path;
    return path.TrimEnd('/');
  }

  private static string[] Split(string path)
  {
    if (path == "/") return new string[0];
    return path.Trim('/').Split("/");
  }
}
