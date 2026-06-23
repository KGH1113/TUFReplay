using JALib.Core;
using JALib.Core.Setting;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Replay;

public class ReplaySetting : JASetting
{
  public bool AutoRecord = true;
  public bool DebugLogging = true;
  public bool MirrorInputToOS = true;
  public double InputLeadSeconds = 0d;

  public ReplaySetting(JAMod mod, JObject jsonObject = null) : base(mod, jsonObject)
  {
    Replay.Settings = this;
  }
}
