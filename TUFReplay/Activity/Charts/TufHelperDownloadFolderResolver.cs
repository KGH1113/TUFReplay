using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Activity.Charts;

// Finds the TUF level ID of a TUFHelper download from TUFHelper's own folder rule, for TUFHelper
// versions that do not publish LevelContextResolver (3.1.1 and older). TUFHelper saves each level to
// "{LevelSaveFolder}/{artist} - {song} {id}" and keeps LevelSaveFolder in its Settings.json.
internal sealed class TufHelperDownloadFolderResolver
{
  private const string SettingsFileName = "Settings.json";
  private const string SaveFolderSetting = "LevelSaveFolder";

  private static readonly char[] DirectorySeparators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

  private readonly object _gate = new object();
  private readonly string _settingsPath;
  private readonly string _defaultSaveFolder;
  private string _saveFolder;
  private DateTime? _settingsWriteTimeUtc;

  public TufHelperDownloadFolderResolver(string modPath)
    : this(modPath, DefaultSaveFolder(modPath)) { }

  internal TufHelperDownloadFolderResolver(string modPath, string defaultSaveFolder)
  {
    _settingsPath = Path.Combine(modPath, SettingsFileName);
    _defaultSaveFolder = defaultSaveFolder;
  }

  // The defaults of TUFHelper's Setting constructor.
  public static string DefaultSaveFolder(string modPath) =>
    RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
      ? Path.Combine(modPath, "SavedLevels")
      : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TUFHelper", "Levels");

  public int? ResolveTufLevelId(string levelPath)
  {
    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath);
    if (canonicalPath == null)
      return null;

    string saveFolder = GetSaveFolder();
    if (saveFolder == null)
      return null;

    string relativePath = Path.GetRelativePath(saveFolder, canonicalPath);
    if (Path.IsPathRooted(relativePath))
      return null;

    int separatorIndex = relativePath.IndexOfAny(DirectorySeparators);
    if (separatorIndex <= 0)
      return null;

    return TufLevelFolderName.TryParseTufHelper(relativePath.Substring(0, separatorIndex), out int levelId)
      ? levelId
      : null;
  }

  // TUFHelper writes Settings.json as soon as the save folder changes, so it is read again only when
  // the file changes.
  private string GetSaveFolder()
  {
    lock (_gate)
    {
      DateTime? writeTimeUtc = File.Exists(_settingsPath) ? File.GetLastWriteTimeUtc(_settingsPath) : null;
      if (_saveFolder != null && writeTimeUtc == _settingsWriteTimeUtc)
        return _saveFolder;

      // An unreadable file, for example one TUFHelper is writing, is read again on the next call.
      if (!TryReadSaveFolder(writeTimeUtc.HasValue, out string saveFolder))
        return null;

      _saveFolder = saveFolder;
      _settingsWriteTimeUtc = writeTimeUtc;
      return _saveFolder;
    }
  }

  private bool TryReadSaveFolder(bool settingsExist, out string saveFolder)
  {
    saveFolder = null;
    try
    {
      string configured = null;
      if (settingsExist)
      {
        JToken value = JObject.Parse(File.ReadAllText(_settingsPath))[SaveFolderSetting];
        configured = value?.Type == JTokenType.String ? (string)value : null;
      }

      saveFolder = Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? _defaultSaveFolder : configured);
      return true;
    }
    catch (Exception exception)
      when (exception is IOException
        || exception is UnauthorizedAccessException
        || exception is JsonException
        || exception is ArgumentException
        || exception is NotSupportedException
      )
    {
      return false;
    }
  }
}
