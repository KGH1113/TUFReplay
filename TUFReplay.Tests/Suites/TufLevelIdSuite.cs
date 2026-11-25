using TUFReplay.Activity.Charts;
using static TestFixture;

internal static class TufLevelIdSuite
{
  internal static void RunAll(string root)
  {
    ParsesDownloadFolderNames();
    ResolvesTufHelperDownloads(Path.Combine(root, "tufhelper"));
    UsesTheDefaultSaveFolderWithoutSettings(Path.Combine(root, "tufhelper-default"));
    FollowsSaveFolderChanges(Path.Combine(root, "tufhelper-change"));
    RetriesUnreadableSettings(Path.Combine(root, "tufhelper-unreadable"));
    ShowsPathsRelativeToTufHelperLevelFolders(Path.Combine(root, "tufhelper-display"));
  }

  private static void ParsesDownloadFolderNames()
  {
    AssertLevelId("tuf-8068", 8068);
    AssertLevelId("TUF-8068", 8068);
    AssertLevelId("Camellia - Ghost 8068", 8068);
    AssertLevelId("Artist - Song - Remix 12", 12);
    AssertLevelId("Unknown - Unknown 7", 7);
    AssertLevelId("tuf-0", null);
    AssertLevelId("tuf-12a", null);
    AssertLevelId("tuf--12", null);
    AssertLevelId("Ghost 8068", null);
    AssertLevelId("Camellia - Ghost", null);
    AssertLevelId("Camellia - 8068", null);
    AssertLevelId("Camellia - Ghost 0", null);
    AssertLevelId("Camellia - Ghost -5", null);
    AssertLevelId("Camellia - Ghost 8068 ", null);
    AssertLevelId("Camellia - Ghost 99999999999", null);
    AssertLevelId("", null);
    AssertLevelId(null, null);
    Assert(
      !TufLevelFolderName.TryParseTufHelper("tuf-8068", out _),
      "A TUFHelperLite folder was read as a TUFHelper folder."
    );
    Assert(
      !TufLevelFolderName.TryParseTufHelperLite("Camellia - Ghost 8068", out _),
      "A TUFHelper folder was read as a TUFHelperLite folder."
    );
  }

  private static void ResolvesTufHelperDownloads(string root)
  {
    string modPath = Path.Combine(root, "Mods", "TUFHelper");
    string saveFolder = Path.Combine(root, "Levels");
    WriteSettings(modPath, "{\"MinDiff\":1,\"LevelSaveFolder\":" + JsonString(saveFolder) + ",\"Language\":\"Auto\"}");
    var resolver = new TufHelperDownloadFolderResolver(modPath, Path.Combine(root, "unused-default"));

    AssertResolved(resolver, Touch(saveFolder, "Camellia - Ghost 8068", "Ghost.adofai"), 8068, "a level in its folder");
    AssertResolved(
      resolver,
      Touch(saveFolder, "Camellia - Ghost 8068", "EX", "Ghost EX.adofai"),
      8068,
      "a chart in a subfolder"
    );
    AssertResolved(resolver, Touch(saveFolder, "loose.adofai"), null, "a file directly in the save folder");
    AssertResolved(resolver, Touch(saveFolder, "My Levels", "a.adofai"), null, "a folder TUFHelper did not name");
    AssertResolved(resolver, Touch(saveFolder, "A - B 5", "a.txt"), null, "a file that is not .adofai");
    AssertResolved(resolver, Touch(root, "Elsewhere", "A - B 5", "a.adofai"), null, "a level outside the save folder");
    AssertResolved(resolver, Path.Combine(saveFolder, "A - B 6", "missing.adofai"), null, "a missing file");
    AssertResolved(resolver, null, null, "no path");
  }

  private static void UsesTheDefaultSaveFolderWithoutSettings(string root)
  {
    string modPath = Path.Combine(root, "Mods", "TUFHelper");
    string defaultFolder = Path.Combine(root, "Default");
    Directory.CreateDirectory(modPath);
    var resolver = new TufHelperDownloadFolderResolver(modPath, defaultFolder);
    string level = Touch(defaultFolder, "A - B 31", "a.adofai");
    AssertResolved(resolver, level, 31, "a level in the default folder without Settings.json");

    WriteSettings(modPath, "{\"LevelSaveFolder\":null}");
    AssertResolved(resolver, level, 31, "a level in the default folder with an empty setting");
  }

  private static void FollowsSaveFolderChanges(string root)
  {
    string modPath = Path.Combine(root, "Mods", "TUFHelper");
    string firstFolder = Path.Combine(root, "First");
    string secondFolder = Path.Combine(root, "Second");
    string firstLevel = Touch(firstFolder, "A - B 41", "a.adofai");
    string secondLevel = Touch(secondFolder, "A - B 42", "a.adofai");
    var resolver = new TufHelperDownloadFolderResolver(modPath, Path.Combine(root, "unused-default"));

    WriteSettings(modPath, "{\"LevelSaveFolder\":" + JsonString(firstFolder) + "}", new DateTime(2026, 1, 1));
    AssertResolved(resolver, firstLevel, 41, "a level in the first save folder");
    AssertResolved(resolver, secondLevel, null, "a level in a folder not yet chosen");

    WriteSettings(modPath, "{\"LevelSaveFolder\":" + JsonString(secondFolder) + "}", new DateTime(2026, 1, 2));
    AssertResolved(resolver, secondLevel, 42, "a level in the new save folder");
    AssertResolved(resolver, firstLevel, null, "a level in the old save folder");
  }

  private static void RetriesUnreadableSettings(string root)
  {
    string modPath = Path.Combine(root, "Mods", "TUFHelper");
    string saveFolder = Path.Combine(root, "Levels");
    string level = Touch(saveFolder, "A - B 51", "a.adofai");
    var resolver = new TufHelperDownloadFolderResolver(modPath, Path.Combine(root, "unused-default"));

    WriteSettings(modPath, "{\"LevelSaveFolder\":", new DateTime(2026, 1, 1));
    AssertResolved(resolver, level, null, "a level while Settings.json is half written");

    WriteSettings(modPath, "{\"LevelSaveFolder\":" + JsonString(saveFolder) + "}", new DateTime(2026, 1, 1));
    AssertResolved(resolver, level, 51, "a level after Settings.json is complete, with the same write time");
  }

  private static void ShowsPathsRelativeToTufHelperLevelFolders(string root)
  {
    string level = Path.Combine(root, "Levels", "Camellia - Ghost 8068", "EX", "Ghost EX.adofai");
    Assert(
      LevelDisplayPath.RelativeToLevelFolder(8068, level) == "EX/Ghost EX.adofai",
      "The displayed TUFHelper chart path is not relative to its downloaded level folder."
    );
    Assert(
      LevelDisplayPath.RelativeToLevelFolder(8069, level) == "Ghost EX.adofai",
      "A TUFHelper folder with a different ID was used as the level folder."
    );
  }

  private static void AssertLevelId(string folderName, int? expected)
  {
    int? actual = TufLevelFolderName.TryParseLevelId(folderName, out int levelId) ? levelId : null;
    Assert(
      actual == expected,
      "Folder name '" + folderName + "' gave TUF level " + (actual?.ToString() ?? "none") + "."
    );
  }

  private static void AssertResolved(
    TufHelperDownloadFolderResolver resolver,
    string levelPath,
    int? expected,
    string label
  )
  {
    int? actual = resolver.ResolveTufLevelId(levelPath);
    Assert(actual == expected, "TUFHelper resolution of " + label + " gave " + (actual?.ToString() ?? "none") + ".");
  }

  private static string Touch(string folder, params string[] parts)
  {
    string path = Path.Combine(new[] { folder }.Concat(parts).ToArray());
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, "{}");
    return path;
  }

  private static void WriteSettings(string modPath, string json, DateTime? writeTimeUtc = null)
  {
    Directory.CreateDirectory(modPath);
    string path = Path.Combine(modPath, "Settings.json");
    File.WriteAllText(path, json);
    if (writeTimeUtc.HasValue)
      File.SetLastWriteTimeUtc(path, writeTimeUtc.Value);
  }

  private static string JsonString(string value) => Newtonsoft.Json.JsonConvert.ToString(value);
}
