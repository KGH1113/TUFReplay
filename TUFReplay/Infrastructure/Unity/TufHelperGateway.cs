using System;
using System.Reflection;

namespace TUFReplay.Infrastructure.Unity;

public static class TufHelperGateway
{
  private static readonly string[] ResolverTypeNames =
  {
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite.Core",
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite",
  };

  public static int? ResolveTufLevelId(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      return null;

    try
    {
      Type resolver = null;
      foreach (string resolverTypeName in ResolverTypeNames)
      {
        resolver = Type.GetType(resolverTypeName, false);
        if (resolver != null)
          break;
      }
      MethodInfo method = resolver?.GetMethod(
        "ResolveTufLevelId",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(string) },
        null
      );
      if (method != null && method.ReturnType == typeof(int?))
      {
        int? resolvedLevelId = (int?)method.Invoke(null, new object[] { levelPath });
        if (resolvedLevelId.HasValue)
          return resolvedLevelId;
      }
    }
    catch (Exception ex)
    {
      Main.Instance?.Log("[Recording] TUFHelperLite resolution unavailable: " + ex.GetType().Name);
    }

    return null;
  }

  public static int? GetLevelID()
  {
    return ResolveTufLevelId(ADOBase.levelPath);
  }

  public static bool IsFromTUFHelper() => GetLevelID().HasValue;
}
