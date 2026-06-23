using JALib.Core;
using JALib.Core.Setting;
using Newtonsoft.Json.Linq;

namespace TUFReplay;

public class TUFReplaySetting(JAMod mod, JObject jsonObject = null) : JASetting(mod, jsonObject)
{
  public float Size = 1;
}
