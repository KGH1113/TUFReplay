using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TUFReplay.Activity.Charts;

public static class LevelGroupIdentity
{
  public static string Create(int? tufLevelId, string levelPath)
  {
    string identity;
    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath, requireExists: false) ?? levelPath ?? string.Empty;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      canonicalPath = canonicalPath.ToUpperInvariant();
    identity = tufLevelId.HasValue
      ? "tuf\0" + tufLevelId.Value.ToString(CultureInfo.InvariantCulture) + "\0" + canonicalPath
      : "local\0" + canonicalPath;

    using SHA256 sha256 = SHA256.Create();
    string encoded = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(identity)));
    return "level-group-v2-" + encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }
}
