using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.Unity;

public static class LevelPathIdentity
{
  private static StringComparison Comparison =>
    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

  public static string Canonicalize(string levelPath, bool requireExists = true)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(levelPath))
        return null;
      if (!levelPath.EndsWith(".adofai", StringComparison.OrdinalIgnoreCase))
        return null;

      string canonical = Path.GetFullPath(levelPath);
      return !requireExists || File.Exists(canonical) ? canonical : null;
    }
    catch
    {
      return null;
    }
  }

  public static string Current()
  {
    return Canonicalize(ADOBase.levelPath);
  }

  public static bool Equals(string left, string right)
  {
    string canonicalLeft = Canonicalize(left);
    string canonicalRight = Canonicalize(right);
    return canonicalLeft != null && canonicalRight != null && string.Equals(canonicalLeft, canonicalRight, Comparison);
  }
}
