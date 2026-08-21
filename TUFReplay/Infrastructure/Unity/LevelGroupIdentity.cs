using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TUFReplay.Infrastructure.Unity;

public static class LevelGroupIdentity
{
  public static string Create(int? tufLevelId, string levelPath)
  {
    string identity;
    if (tufLevelId.HasValue)
    {
      identity = "tuf\0" + tufLevelId.Value.ToString(CultureInfo.InvariantCulture);
    }
    else
    {
      string canonicalPath =
        LevelPathIdentity.Canonicalize(levelPath, requireExists: false) ?? levelPath ?? string.Empty;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        canonicalPath = canonicalPath.ToUpperInvariant();
      identity = "local\0" + canonicalPath;
    }

    using SHA256 sha256 = SHA256.Create();
    string encoded = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(identity)));
    return "level-group-v1-" + encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }
}
