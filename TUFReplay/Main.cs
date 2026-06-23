using JALib.Core;
using JALib.Tools;
using TUFReplay.Shared;
using UnityEngine;

namespace TUFReplay;

public class Main() : JAMod(typeof(TUFReplaySetting))
{
  public static Main Instance;
  public static SettingGUI SettingGUI;
  public static TUFReplaySetting Settings;
  public static LocalServer.LocalServer Server;

  protected override void OnSetup()
  {
    Instance = this;
    Settings = (TUFReplaySetting)Setting;
    SettingGUI = new SettingGUI(this);

    UnityMainThread.Initialize();
    Database.Initialize();
    AddFeature(new Recording.Recording());
    AddFeature(new Replay.Replay());
  }

  protected override void OnEnable()
  {
    StartServer();
  }

  protected override void OnDisable()
  {
    Server?.Stop();
    Server = null;

    Recording.Recording.Instance?.Session.Stop();
    SaveSetting();
  }

  protected override void OnGUI()
  {
    GUILayout.Label("TUFReplay");
  }

  private static void StartServer()
  {
    if (Server != null) return;

    Server = new LocalServer.LocalServer();
    Server.Start();
  }
}
