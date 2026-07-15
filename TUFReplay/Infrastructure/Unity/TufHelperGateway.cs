using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace TUFReplay.Infrastructure.Unity;

public static class TufHelperGateway
{
  private const string ResolverTypeName = "TUFHelperLite.Integration.LevelContextResolver, TUFHelperLite";
  private const string TufHelperAssemblyName = "TUFHelperLite";
  private const string TufCachePrefix = "tuf-";

  public static int? ResolveTufLevelId(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      return null;

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

    return ResolveFromDownloadCachePath(levelPath);
  }

  private static int? ResolveFromDownloadCachePath(string levelPath)
  {
    if (!string.Equals(Path.GetExtension(levelPath), ".adofai", StringComparison.OrdinalIgnoreCase))
    {
      return null;
    }

    try
    {
      Assembly tufHelperAssembly = FindLoadedTufHelperAssembly();
      if (tufHelperAssembly == null)
        return null;

      string modDirectory = ResolveTufHelperModDirectory(tufHelperAssembly);
      if (string.IsNullOrWhiteSpace(modDirectory))
        return null;

      string canonicalLevelPath = Path.GetFullPath(levelPath);
      if (!File.Exists(canonicalLevelPath))
        return null;

      string canonicalRoot = Path.GetFullPath(Path.Combine(modDirectory, "Downloads"));
      string relativePath = Path.GetRelativePath(canonicalRoot, canonicalLevelPath);
      if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
        return null;

      string parentPrefix = ".." + Path.DirectorySeparatorChar;
      string alternateParentPrefix = ".." + Path.AltDirectorySeparatorChar;
      if (
        relativePath == ".."
        || relativePath.StartsWith(parentPrefix, StringComparison.Ordinal)
        || relativePath.StartsWith(alternateParentPrefix, StringComparison.Ordinal)
      )
      {
        return null;
      }

      int separatorIndex = relativePath.IndexOfAny(
        new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }
      );
      if (separatorIndex <= 0)
        return null;

      string cacheKey = relativePath.Substring(0, separatorIndex);
      if (!cacheKey.StartsWith(TufCachePrefix, StringComparison.OrdinalIgnoreCase))
        return null;

      string id = cacheKey.Substring(TufCachePrefix.Length);
      return int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int levelId) && levelId > 0
        ? levelId
        : null;
    }
    catch (Exception ex)
      when (ex is ArgumentException
        || ex is IOException
        || ex is NotSupportedException
        || ex is UnauthorizedAccessException
      )
    {
      return null;
    }
  }

  private static Assembly FindLoadedTufHelperAssembly()
  {
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (string.Equals(assembly.GetName().Name, TufHelperAssemblyName, StringComparison.OrdinalIgnoreCase))
      {
        return assembly;
      }
    }

    return null;
  }

  private static string ResolveTufHelperModDirectory(Assembly assembly)
  {
    try
    {
      Type mainType = assembly.GetType("TUFHelperLite.Main", false);
      object instance = mainType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
      object modEntry = mainType
        ?.GetProperty("ModEntry", BindingFlags.Public | BindingFlags.Instance)
        ?.GetValue(instance);
      string modPath =
        modEntry?.GetType().GetProperty("Path", BindingFlags.Public | BindingFlags.Instance)?.GetValue(modEntry)
        as string;
      if (!string.IsNullOrWhiteSpace(modPath))
        return modPath;
    }
    catch (Exception)
    {
      // Older TUFHelperLite builds may expose a different integration surface.
    }

    return string.IsNullOrWhiteSpace(assembly.Location) ? null : Path.GetDirectoryName(assembly.Location);
  }

  public static int? GetLevelID()
  {
    return ResolveTufLevelId(ADOBase.levelPath);
  }

  public static bool IsFromTUFHelper() => GetLevelID().HasValue;
}
