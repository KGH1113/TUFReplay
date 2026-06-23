using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DirectLevel;
using TUFHelper.ModScripts.Json;
using TUFHelper.Utils;
using TUFReplay.Shared;

namespace TUFReplay.Replay;

class TUFHelperReplayAPI
{
  private static readonly object OpeningLevelIdsLock = new object();
  private static readonly HashSet<int> OpeningLevelIds = new HashSet<int>();
  private static int _replayOpenDepth;

  public static bool IsOpeningReplayLevel => _replayOpenDepth > 0;

  public static OpenRecordResult OpenLevel(LevelListInfoElementJson levelInfo)
  {
    Setting setting = GetTUFHelperSetting();
    if (setting == null) return OpenRecordResult.Fail("tuf_helper_setting_not_ready");

    DownloadedLevel downloaded = setting.DownloadedLevels
      .FirstOrDefault(level => level.LevelInfo?.ID == levelInfo.ID);

    if (downloaded != null)
    {
      List<string> files = LevelDownloader.FindAdofaiFiles(downloaded.NameFolder);
      if (files.Count == 0) return OpenRecordResult.Fail("adofai_file_not_found");

      string adofaiPath = files[0];

      UnityMainThread.Post(() =>
      {
        TryToLoadReplayLevel(levelInfo, adofaiPath);
      });

      return OpenRecordResult.Opened(levelInfo.ID);
    }
    else
    {
      if (levelInfo.DlLink == null) return OpenRecordResult.Fail("download_link_missing");
      if (!TryBeginOpening(levelInfo.ID)) return OpenRecordResult.Fail("level_open_already_running");

      LevelDownloader downloader = new LevelDownloader(levelInfo.DlLink);

      downloader.DownloadComplete += (sender, args) =>
      {
        UnityMainThread.Post(() =>
        {
          try
          {
            if (args.Levels.Count == 0)
            {
              return;
            }

            string adofaiPath = args.Levels[0];
            string folder = Path.GetDirectoryName(adofaiPath);

            LevelPrefabScript.SaveLevelToSettings(levelInfo, folder, adofaiPath);
            TryToLoadReplayLevel(levelInfo, adofaiPath);
          }
          finally
          {
            EndOpening(levelInfo.ID);
          }
        });
      };

      downloader.ErrorHandler = ex =>
      {
        EndOpening(levelInfo.ID);
        Main.Instance.LogException(ex);
      };

      _ = downloader.DownloadWithTask(
        setting.LevelSaveFolder,
        true,
        CancellationToken.None
      );

      return OpenRecordResult.Downloading(levelInfo.ID);
    }
  }

  private static bool TryBeginOpening(int levelId)
  {
    lock (OpeningLevelIdsLock)
    {
      if (OpeningLevelIds.Contains(levelId)) return false;

      OpeningLevelIds.Add(levelId);
      return true;
    }
  }

  private static void EndOpening(int levelId)
  {
    lock (OpeningLevelIdsLock)
    {
      OpeningLevelIds.Remove(levelId);
    }
  }

  private static void TryToLoadReplayLevel(LevelListInfoElementJson levelInfo, string adofaiPath)
  {
    _replayOpenDepth++;

    try
    {
      LevelPrefabScript.TryToLoadLevel(levelInfo, adofaiPath);
    }
    finally
    {
      _replayOpenDepth--;
    }
  }

  private static Setting GetTUFHelperSetting()
  {
    FieldInfo field = typeof(global::TUFHelper.Main).GetField(
      "Setting",
      BindingFlags.Static | BindingFlags.NonPublic
    );
    return field.GetValue(null) as Setting;
  }
}
