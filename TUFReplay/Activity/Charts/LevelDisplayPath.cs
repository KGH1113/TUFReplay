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

    for (
      DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(canonicalPath));
      directory != null;
      directory = directory.Parent
    )
    {
      if (
        !TufLevelFolderName.TryParseLevelId(directory.Name, out int folderLevelId)
        || folderLevelId != tufLevelId.Value
      )
        continue;

      string relativePath = Path.GetRelativePath(directory.FullName, canonicalPath);
      return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    return fileName;
  }
}
