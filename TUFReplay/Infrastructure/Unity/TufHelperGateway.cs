using System;
using System.Reflection;

namespace TUFReplay.Infrastructure.Unity;

public static class TufHelperGateway
{
  private const string ResolverTypeName = "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite";

  public static int? ResolveTufLevelId(string levelPath)
  {
    if (string.IsNullOrEmpty(levelPath)) return null;

    try
    {
      Type resolver = Type.GetType(ResolverTypeName, false);
      MethodInfo method = resolver?.GetMethod(
        "ResolveTufLevelId",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(string) },
        null
      );
      if (method == null || method.ReturnType != typeof(int?)) return null;
      return (int?)method.Invoke(null, new object[] { levelPath });
    }
    catch (Exception ex)
    {
      Main.Instance?.Log("[Recording] TUFHelperLite resolution unavailable: " + ex.GetType().Name);
      return null;
    }
  }

  public static int? GetLevelID()
  {
    return ResolveTufLevelId(ADOBase.levelPath);
  }

  public static bool IsFromTUFHelper() => GetLevelID().HasValue;
}
