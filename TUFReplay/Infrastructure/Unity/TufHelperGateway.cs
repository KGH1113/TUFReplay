using System;
using System.Reflection;
using TUFReplay.Application.Activity;

namespace TUFReplay.Infrastructure.Unity;

public static class TufHelperGateway
{
  private const int NegativeCacheTtlMilliseconds = 5000;

  private static readonly string[] ResolverTypeNames =
  {
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite.Core",
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite",
  };
  private static readonly TufLevelIdResolverCache ResolverCache = new TufLevelIdResolverCache(
    CreateResolver,
    LogResolutionFailure,
    NegativeCacheTtlMilliseconds
  );

  public static int? ResolveTufLevelId(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      return null;

    return ResolverCache.Resolve(levelPath);
  }

  private static Func<string, int?> CreateResolver()
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
    if (method == null || method.ReturnType != typeof(int?))
      return null;

    return (Func<string, int?>)method.CreateDelegate(typeof(Func<string, int?>));
  }

  private static void LogResolutionFailure(Exception exception) =>
    Main.Instance?.Log("[Recording] TUFHelperLite resolution unavailable: " + exception.GetType().Name);

  public static int? GetLevelID()
  {
    return ResolveTufLevelId(ADOBase.levelPath);
  }

  public static bool IsFromTUFHelper() => GetLevelID().HasValue;
}
