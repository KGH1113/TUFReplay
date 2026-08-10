using System;
using TUFReplay.Bootstrap;
using TUFReplay.Infrastructure.NativeInput;
using TUFReplay.Infrastructure.Settings;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;
using UnityModManagerNet;

namespace TUFReplay;

public sealed class Main
{
  public static Main Instance { get; private set; }
  public static TUFReplaySetting Settings => TUFReplaySettingStore.Current;
  public static UpdateSettings UpdaterSettings { get; private set; }

  public UnityModManager.ModEntry ModEntry { get; }
  public string InstallPath => ModEntry.Path;
  public string PayloadPath => System.IO.Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? ModEntry.Path;
  public string Path => InstallPath;
  public string Version => ModEntry.Info.Version;

  private readonly string _updateSettingsPath;
  private bool _enabled;

  private Main(UnityModManager.ModEntry modEntry)
  {
    ModEntry = modEntry;
    _updateSettingsPath = System.IO.Path.Combine(InstallPath, "UpdateSettings.json");
  }

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    try
    {
      if (AdofaiIpcMigrationBridge.PrepareAndNotify(modEntry))
        return true;

      Instance = new Main(modEntry);
      TUFReplaySettingStore.Initialize(System.IO.Path.Combine(Instance.InstallPath, "Settings.json"));
      UpdaterSettings = UpdateSettings.Load(Instance._updateSettingsPath);
      UnityMainThread.Initialize();

      modEntry.OnToggle = OnToggle;
      modEntry.OnUnload = OnUnload;
      modEntry.OnUpdate = OnUpdate;
      modEntry.OnGUI = OnGUI;
      modEntry.OnSaveGUI = OnSaveGUI;

      Instance.Enable();
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
  }

  public void Log(string message)
  {
    ModEntry.Logger.Log(message);
  }

  public void LogException(string context, Exception exception)
  {
    ModEntry.Logger.Error("[" + context + "] " + exception);
  }

  private static void OnGUI(UnityModManager.ModEntry modEntry)
  {
    GUILayout.Label("Updates");
    bool receiveBetaUpdates = GUILayout.Toggle(UpdaterSettings.ReceiveBetaUpdates, "Receive beta updates");
    GUILayout.Label("Beta builds may be unstable. Changes apply on the next game launch.");

    if (receiveBetaUpdates != UpdaterSettings.ReceiveBetaUpdates)
    {
      UpdaterSettings.ReceiveBetaUpdates = receiveBetaUpdates;
      SaveUpdateSettings(modEntry);
    }
  }

  private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
  {
    ModBootstrap.UpdateRuntime();
    NativeInputUmmWindowInterlock.SynchronizeWithManagerWindow();
  }

  private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
  {
    SaveUpdateSettings(modEntry);
  }

  private static void SaveUpdateSettings(UnityModManager.ModEntry modEntry)
  {
    try
    {
      UpdaterSettings.Save(Instance._updateSettingsPath);
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error("[UpdateSettings] " + exception);
    }
  }

  private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
  {
    try
    {
      if (value)
        Instance.Enable();
      else
        Instance.Disable();
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
  }

  private static bool OnUnload(UnityModManager.ModEntry modEntry)
  {
    try
    {
      Instance?.Disable();
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
    finally
    {
      UnityMainThread.Shutdown();
    }
  }

  private void Enable()
  {
    if (_enabled)
      return;

    ModBootstrap.InitializeRuntime();
    try
    {
      FeatureRegistry.Initialize();
      _enabled = true;
      NativeInputUmmWindowInterlock.SynchronizeWithManagerWindow();
    }
    catch
    {
      FeatureRegistry.Shutdown();
      throw;
    }
  }

  private void Disable()
  {
    if (!_enabled)
      return;

    _enabled = false;
    try
    {
      ModBootstrap.Shutdown();
    }
    finally
    {
      NativeInputUmmWindowInterlock.Reset();
      TUFReplaySettingStore.Save();
    }
  }
}
