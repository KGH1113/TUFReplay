using System;
using System.Globalization;
using System.IO;

namespace TUFReplay.Activity.Charts;

public static class LevelDisplayPath
{
  public static string RelativeToLevelFolder(int? tufLevelId, string levelPath)
  {
    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath, requireExists: false);
    if (canonicalPath == null)
      return null;

    string fileName = Path.GetFileName(canonicalPath);
    if (!tufLevelId.HasValue)
      return fileName;

    for (DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(canonicalPath));
         directory != null;
         directory = directory.Parent)
    {
      if (!directory.Name.StartsWith("tuf-", StringComparison.OrdinalIgnoreCase) ||
          !int.TryParse(directory.Name.Substring(4), NumberStyles.None,
            CultureInfo.InvariantCulture, out int folderLevelId) ||
          folderLevelId != tufLevelId.Value)
        continue;

      string relativePath = Path.GetRelativePath(directory.FullName, canonicalPath);
      return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    return fileName;
  }
}
