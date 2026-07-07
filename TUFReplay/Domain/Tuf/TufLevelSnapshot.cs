using TUFHelper.ModScripts.Json;

namespace TUFReplay.Domain.Tuf;

public sealed class TufLevelSnapshot
{
  public TufLevelIdentity Identity;
  public LevelListInfoElementJson LevelInfo;
  public int LevelTileCount;
}
