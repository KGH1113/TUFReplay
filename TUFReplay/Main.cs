using JALib.Core;
using TUFReplay.Bootstrap;

namespace TUFReplay;

public class Main() : JAMod(typeof(TUFReplaySetting))
{
  public static Main Instance;
  public static TUFReplaySetting Settings;
  public static LocalServer.LocalServer Server;

  protected override void OnSetup()
  {
    Instance = this;
    Settings = (TUFReplaySetting)Setting;

    ModBootstrap.InitializeRuntime();
    FeatureRegistry.Initialize();
  }

  protected override void OnEnable()
  {
    StartServer();
  }

  protected override void OnDisable()
  {
    ModBootstrap.Shutdown();
    SaveSetting();
  }

  private static void StartServer()
  {
    if (Server != null) return;

    Server = new LocalServer.LocalServer();
    Server.Start();
  }
}
