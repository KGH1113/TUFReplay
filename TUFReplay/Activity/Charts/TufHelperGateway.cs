using System;
using System.Reflection;
using TUFReplay.Activity.Tracking;
using UnityModManagerNet;

namespace TUFReplay.Activity.Charts;

// Asks the installed TUF download mods, TUFHelperLite and TUFHelper, which TUF level a chart file belongs to.
public static class TufHelperGateway
{
  private const int NegativeCacheTtlMilliseconds = 5000;
  private const string TufHelperModId = "TUFHelper";

  // Both mods publish the same method: public static int? ResolveTufLevelId(string levelPath).
  private static readonly string[] TufHelperLiteResolverTypeNames =
  {
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite.Core",
    "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite",
  };
  private static readonly string[] TufHelperResolverTypeNames =
  {
    "TUFHelper.Integration.LevelContextResolver, TUFHelper",
  };

  // Each mod has its own cache, so one that loads later is still found.
  private static readonly TufLevelIdResolverCache TufHelperLiteResolver = new TufLevelIdResolverCache(
    () => FindPublishedResolver(TufHelperLiteResolverTypeNames),
    exception => LogResolutionFailure("TUFHelperLite", exception),
    NegativeCacheTtlMilliseconds
  );
  private static readonly TufLevelIdResolverCache TufHelperResolver = new TufLevelIdResolverCache(
    CreateTufHelperResolver,
    exception => LogResolutionFailure("TUFHelper", exception),
    NegativeCacheTtlMilliseconds
  );

  public static int? ResolveTufLevelId(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      return null;

    return TufHelperLiteResolver.Resolve(levelPath) ?? TufHelperResolver.Resolve(levelPath);
  }

  // TUFHelper 3.1.1 and older do not publish the method, so their download folder rule is read instead.
  private static Func<string, int?> CreateTufHelperResolver()
  {
    Func<string, int?> published = FindPublishedResolver(TufHelperResolverTypeNames);
    if (published != null)
      return published;

    string modPath = UnityModManager.FindMod(TufHelperModId)?.Path;
    return string.IsNullOrEmpty(modPath) ? null : new TufHelperDownloadFolderResolver(modPath).ResolveTufLevelId;
  }

  private static Func<string, int?> FindPublishedResolver(string[] resolverTypeNames)
  {
    Type resolver = null;
    foreach (string resolverTypeName in resolverTypeNames)
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

  private static void LogResolutionFailure(string modName, Exception exception) =>
    Main.Instance?.Log("[Recording] " + modName + " resolution unavailable: " + exception.GetType().Name);

  public static int? GetLevelID()
  {
    return ResolveTufLevelId(ADOBase.levelPath);
  }

  public static bool IsFromTUFHelper() => GetLevelID().HasValue;
}
