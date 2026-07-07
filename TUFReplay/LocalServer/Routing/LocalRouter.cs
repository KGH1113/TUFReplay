using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TUFReplay.LocalServer.Binding;

namespace TUFReplay.LocalServer.Routing;

public sealed class LocalRouter
{
  private readonly IReadOnlyList<RouteDefinition> _routes;

  public LocalRouter(RouteRegistry registry)
  {
    _routes = registry.Routes;
  }

  public object Dispatch(HttpListenerContext context)
  {
    string method = context.Request.HttpMethod;
    string path = Normalize(context.Request.Url.AbsolutePath);

    RouteMatch match = Find(method, path);
    if (match == null)
    {
      return Http.ServerResponse.NotFound(new { error = "not_found" });
    }

    object[] args = BindArguments(match, context);
    try
    {
      return match.Route.Handler.Invoke(match.Route.Controller, args);
    }
    catch (TargetInvocationException e) when (e.InnerException != null)
    {
      ExceptionDispatchInfo.Capture(e.InnerException).Throw();
      throw;
    }
  }

  private RouteMatch Find(string method, string path)
  {
    string[] requestSegments = Split(path);

    foreach (RouteDefinition route in _routes)
    {
      if (!string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase))
        continue;

      if (route.Segments.Length != requestSegments.Length)
        continue;

      Dictionary<string, string> parameters = new Dictionary<string, string>();
      bool matched = true;

      for (int i = 0; i < route.Segments.Length; i++)
      {
        string routeSegment = route.Segments[i];
        string requestSegment = requestSegments[i];

        if (routeSegment.StartsWith(":", StringComparison.Ordinal))
        {
          string name = routeSegment.Substring(1);
          parameters[name] = WebUtility.UrlDecode(requestSegment);
          continue;
        }

        if (!string.Equals(routeSegment, requestSegment, StringComparison.Ordinal))
        {
          matched = false;
          break;
        }
      }

      if (matched) return new RouteMatch(route, parameters);
    }

    return null;
  }

  private static object[] BindArguments(RouteMatch match, HttpListenerContext context)
  {
    ParameterInfo[] parameters = match.Route.Handler.GetParameters();
    object[] args = new object[parameters.Length];

    for (int i = 0; i < parameters.Length; i++)
    {
      ParameterInfo parameter = parameters[i];

      ParamAttribute paramAttribute = parameter.GetCustomAttribute<ParamAttribute>();
      if (paramAttribute != null)
      {
        match.Params.TryGetValue(paramAttribute.Name, out string value);
        args[i] = ConvertValue(value, parameter.ParameterType);
        continue;
      }

      QueryAttribute queryAttribute = parameter.GetCustomAttribute<QueryAttribute>();
      if (queryAttribute != null)
      {
        string value = context.Request.QueryString[queryAttribute.Name];
        args[i] = ConvertValue(value, parameter.ParameterType);
        continue;
      }

      args[i] = GetDefault(parameter.ParameterType);
    }

    return args;
  }

  private static object ConvertValue(string value, Type targetType)
  {
    if (string.IsNullOrEmpty(value)) return GetDefault(targetType);

    Type nullableType = Nullable.GetUnderlyingType(targetType);
    if (nullableType != null) return ConvertValue(value, nullableType);

    if (targetType == typeof(string)) return value;
    if (targetType == typeof(int)) return int.Parse(value);
    if (targetType == typeof(long)) return long.Parse(value);
    if (targetType == typeof(bool)) return bool.Parse(value);

    return Convert.ChangeType(value, targetType);
  }

  private static object GetDefault(Type type)
  {
    return type.IsValueType ? Activator.CreateInstance(type) : null;
  }

  private static string Normalize(string path)
  {
    if (string.IsNullOrEmpty(path) || path == "/") return "/";
    return path.TrimEnd('/');
  }

  private static string[] Split(string path)
  {
    if (path == "/") return new string[0];
    return path.Trim('/').Split('/');
  }
}
