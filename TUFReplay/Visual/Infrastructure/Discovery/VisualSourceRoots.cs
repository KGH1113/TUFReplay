using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TUFReplay.Composition;

namespace TUFReplay.Visual.Infrastructure.Discovery;

/// <summary>Immutable paths captured while the mod is on Unity's main thread.</summary>
public sealed class VisualSourceRoots
{
  public IReadOnlyList<string> JipperResourcePack { get; }

  public IReadOnlyList<string> JipperKeyviewer { get; }
  public IReadOnlyList<string> ImplResourcePack { get; }

  public VisualSourceRoots(
    IEnumerable<string> jipperResourcePack,
    IEnumerable<string> jipperKeyviewer = null,
    IEnumerable<string> implResourcePack = null
  )
  {
    JipperResourcePack = Copy(jipperResourcePack);
    JipperKeyviewer = Copy(jipperKeyviewer);
    ImplResourcePack = Copy(implResourcePack);
  }

  public static VisualSourceRoots Capture()
  {
    return Discover(Main.Instance?.InstallPath);
  }

  /// <summary>Discover bounded installation roots; file contents are inspected by the locator.</summary>
  public static VisualSourceRoots Discover(string installPath)
  {
    if (string.IsNullOrWhiteSpace(installPath))
      return new VisualSourceRoots(new string[0]);

    // UMM paths can end in a slash. GetParent would otherwise return the mod
    // itself and shift every search root down by one directory.
    string modsPath = Directory
      .GetParent(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
      ?.FullName;
    string gamePath = Directory.GetParent(modsPath ?? string.Empty)?.FullName;
    var jipper = new List<string>();
    AddKnownRoots(jipper, modsPath, "JipperResourcePack", "JipperResourcePack Resource Pack");
    AddKnownRoots(jipper, gamePath, "JipperResourcePack", "JipperResourcePack Resource Pack");

    // Mod directories are often renamed or suffixed with a version. Their
    // Info.json ID, not the directory name, establishes their source identity.
    // Do not scan unrelated game subdirectories (including backups/profiles).
    AddModDirectories(jipper, modsPath);
    // The locator checks metadata IDs before accepting these bounded candidates.
    return new VisualSourceRoots(jipper, jipper, jipper);
  }

  private static void AddKnownRoots(List<string> roots, string parent, params string[] names)
  {
    if (string.IsNullOrWhiteSpace(parent))
      return;
    foreach (string name in names)
    {
      string path = Path.Combine(parent, name);
      if (Directory.Exists(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
        roots.Add(path);
    }
  }

  private static void AddModDirectories(List<string> roots, string parent)
  {
    if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
      return;
    try
    {
      foreach (string child in Directory.EnumerateDirectories(parent))
      {
        if (!File.Exists(Path.Combine(child, "Info.json")))
          continue;
        if (!roots.Contains(child, StringComparer.OrdinalIgnoreCase))
          roots.Add(child);
      }
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private static IReadOnlyList<string> Copy(IEnumerable<string> paths)
  {
    var result = new List<string>();
    if (paths == null)
      return result;
    foreach (string path in paths)
    {
      if (string.IsNullOrWhiteSpace(path))
        continue;
      string fullPath;
      try
      {
        fullPath = Path.GetFullPath(path);
      }
      catch (Exception)
      {
        continue;
      }
      if (!result.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        result.Add(fullPath);
    }
    return result;
  }
}
