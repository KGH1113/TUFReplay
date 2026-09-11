using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Catalog;

/// <summary>Reads TUFHelper's installation claims on a background thread.
/// These claims are independently checked against the official server catalog.</summary>
public static class InstalledLevelReader
{
  public static InstalledLevelContext Read(string levelPath, int expectedLevelId)
  {
    string path = Path.GetFullPath(levelPath);
    var directory = new DirectoryInfo(Path.GetDirectoryName(path));
    for (int depth = 0; directory != null && depth < 16; depth++, directory = directory.Parent)
    {
      string manifestPath = Path.Combine(directory.FullName, ".tufhelperlite-level.json");
      if (!File.Exists(manifestPath)) continue;
      if (new FileInfo(manifestPath).Length > 65536) throw new InvalidDataException("Invalid installation manifest.");
      var manifest = JObject.Parse(File.ReadAllText(manifestPath));
      if ((int?)manifest["Id"] != expectedLevelId) throw new InvalidDataException("Installation level differs.");
      string fileId = (string)manifest["DownloadedFileId"];
      string hash = (string)manifest["InstalledPayloadHash"];
      if (string.IsNullOrWhiteSpace(fileId) || fileId.Length > 256 || hash?.Length != 64)
        throw new InvalidDataException("Installation identity is missing.");
      foreach (char character in hash)
        if (!Uri.IsHexDigit(character)) throw new InvalidDataException("Invalid installation hash.");
      string relative = Path.GetRelativePath(directory.FullName, path).Replace('\\', '/');
      if (relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        throw new InvalidDataException("Invalid installation path.");
      return new InstalledLevelContext(expectedLevelId, fileId, hash, relative);
    }
    throw new InvalidDataException("TUFHelper installation manifest is unavailable.");
  }
}
