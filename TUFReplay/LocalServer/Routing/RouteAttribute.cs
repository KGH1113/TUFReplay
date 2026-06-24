using System;

namespace TUFReplay.LocalServer.Routing;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public abstract class RouteAttribute : Attribute
{
  public string Method { get; }
  public string Path { get; }

  protected RouteAttribute(string method, string path)
  {
    Method = method;
    Path = Normalize(path);
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrEmpty(path) || path == "/") return "";
    return path.StartsWith("/") ? path.TrimEnd('/') : "/" + path.TrimEnd('/');
  }
}
