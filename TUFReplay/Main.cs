using JALib.Core;
using TUFReplay.Bootstrap;

namespace TUFReplay;

public class Main() : JAMod(typeof(TUFReplaySetting))
{
  public static Main Instance;
  public static TUFReplaySetting Settings;

  protected override void OnSetup()
  {
    Instance = this;
    Settings = (TUFReplaySetting)Setting;

    ModBootstrap.InitializeRuntime();
    FeatureRegistry.Initialize();
  }

  protected override void OnDisable()
  {
    ModBootstrap.Shutdown();
    SaveSetting();
  }
}
