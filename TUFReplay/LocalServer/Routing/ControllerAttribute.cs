using System;

namespace TUFReplay.LocalServer.Routing;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ControllerAttribute : Attribute
{
  public string Prefix { get; }

  public ControllerAttribute(string prefix = "")
  {
    Prefix = Normalize(prefix);
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrEmpty(path)) return "";
    return path.StartsWith("/")
      ? path.TrimEnd('/')
      : "/" + path.TrimEnd('/');
  }
}
