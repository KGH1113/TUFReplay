using TUFHelper.ModScripts.Json;
using TUFHelper.Utils;

namespace TUFReplay.Recording;

public static class TUFHelperAPI
{
  public static bool IsFromTUFHelper()
  {
    return ADOFAIGameplayHandler.IsFromTUFHelper;
  }

  public static int? GetLevelID()
  {
    return ADOFAIGameplayHandler.EditorPlayPatch.CurrentLevelInfo?.ID;
  }

  public static int? GetLevelID(PlayButtonEventArgs args)
  {
    return args?.CurrentLevelInfo?.ID;
  }

  public static LevelListInfoElementJson GetLevelInfo(PlayButtonEventArgs args)
  {
    return args?.CurrentLevelInfo;
  }
}
