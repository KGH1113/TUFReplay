using JALib.Core;
using JALib.Core.Setting;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Features.Recording;

public class RecordingSetting : JASetting
{
  public bool AutoRecord = true;

  public RecordingSetting(JAMod mod, JObject jsonObject = null) : base(mod, jsonObject)
  {
    RecordingFeature.Settings = this;
  }
}
