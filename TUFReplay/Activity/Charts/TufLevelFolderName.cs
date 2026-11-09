using System;
using System.Globalization;

namespace TUFReplay.Activity.Charts;

// Names of the folders TUF download mods save levels into, which carry the TUF level ID.
internal static class TufLevelFolderName
{
  private const string TufHelperLitePrefix = "tuf-";
  private const string TufHelperArtistSeparator = " - ";

  public static bool TryParseLevelId(string folderName, out int levelId) =>
    TryParseTufHelperLite(folderName, out levelId) || TryParseTufHelper(folderName, out levelId);

  // TUFHelperLite: "tuf-{id}".
  public static bool TryParseTufHelperLite(string folderName, out int levelId)
  {
    levelId = 0;
    return folderName != null
      && folderName.StartsWith(TufHelperLitePrefix, StringComparison.OrdinalIgnoreCase)
      && TryParseId(folderName.Substring(TufHelperLitePrefix.Length), out levelId);
  }

  // TUFHelper: "{artist} - {song} {id}".
  public static bool TryParseTufHelper(string folderName, out int levelId)
  {
    levelId = 0;
    if (folderName == null)
      return false;

    int lastSpace = folderName.LastIndexOf(' ');
    int artistEnd = folderName.IndexOf(TufHelperArtistSeparator, StringComparison.Ordinal);
    return artistEnd > 0
      && artistEnd + TufHelperArtistSeparator.Length < lastSpace
      && TryParseId(folderName.Substring(lastSpace + 1), out levelId);
  }

  private static bool TryParseId(string text, out int levelId) =>
    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out levelId) && levelId > 0;
}
