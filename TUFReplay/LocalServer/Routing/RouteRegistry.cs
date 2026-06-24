using System;
using System.Collections.Generic;
using System.Reflection;

namespace TUFReplay.LocalServer.Routing;

public sealed class RouteRegistry
{
  private readonly List<RouteDefinition> _routes = new List<RouteDefinition>();

  public IReadOnlyList<RouteDefinition> Routes => _routes;

  public void RegisterController<T>() where T : new()
  {
    RegisterController(typeof(T));
  }

  public void RegisterController(Type controllerType)
  {
    ControllerAttribute controllerAttribute =
      controllerType.GetCustomAttribute<ControllerAttribute>();

    if (controllerAttribute == null) return;

    object controller = Activator.CreateInstance(controllerType);
    string prefix = controllerAttribute.Prefix;

    MethodInfo[] methods = controllerType.GetMethods(
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
    );

    foreach (MethodInfo method in methods)
    {
      foreach (RouteAttribute routeAttribute in method.GetCustomAttributes<RouteAttribute>())
      {
        string fullPath = Combine(prefix, routeAttribute.Path);
        _routes.Add(new RouteDefinition(
          routeAttribute.Method,
          fullPath,
          controller,
          method
        ));
      }
    }
  }

  private static string Combine(string prefix, string path)
  {
    if (string.IsNullOrEmpty(prefix)) return string.IsNullOrEmpty(path) ? "/" : path;
    if (string.IsNullOrEmpty(path)) return prefix;
    return (prefix.TrimEnd('/')) + "/" + path.TrimStart('/').TrimEnd('/');
  }
}
