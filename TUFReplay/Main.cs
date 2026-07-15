using System;
using TUFReplay.Bootstrap;
using TUFReplay.Infrastructure.Unity;
using UnityModManagerNet;

namespace TUFReplay;

public sealed class Main
{
  public static Main Instance { get; private set; }
  public static TUFReplaySetting Settings { get; private set; }

  public UnityModManager.ModEntry ModEntry { get; }
  public string Path => ModEntry.Path;
  public string Version => ModEntry.Info.Version;

  private readonly string _settingsPath;
  private bool _enabled;

  private Main(UnityModManager.ModEntry modEntry)
  {
    ModEntry = modEntry;
    _settingsPath = System.IO.Path.Combine(modEntry.Path, "Settings.json");
  }

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    try
    {
      Instance = new Main(modEntry);
      Settings = TUFReplaySetting.Load(Instance._settingsPath);
      UnityMainThread.Initialize();

      modEntry.OnToggle = OnToggle;
      modEntry.OnUnload = OnUnload;

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
      Settings.Save(_settingsPath);
    }
  }
}
