using System;
using TUFReplay.Composition;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Unity;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.NativeInput;
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
  private static bool _showReplayInputDiagnostics;

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

    _showReplayInputDiagnostics = GUILayout.Toggle(
      _showReplayInputDiagnostics,
      "Replay input diagnostics"
    );
    if (_showReplayInputDiagnostics)
    {
      if (ReplaySessionService.TryGetNativeInputStats(out ReplayNativeInputStats stats))
      {
        GUILayout.Label("Waiter: " + stats.Waiter);
        if (!string.IsNullOrWhiteSpace(stats.WaiterFallbackReason))
          GUILayout.Label("Waiter fallback: " + stats.WaiterFallbackReason);
        GUILayout.Label(
          "Lateness p50/p95/p99/max: "
            + stats.P50LatenessUs
            + "/"
            + stats.P95LatenessUs
            + "/"
            + stats.P99LatenessUs
            + "/"
            + stats.MaxLatenessUs
            + " us"
        );
        GUILayout.Label(
          "Scheduled/emitted/failed: " + stats.Scheduled + "/" + stats.Emitted + "/" + stats.FailedEvents
        );
        GUILayout.Label(
          "Catch-up groups/events: " + stats.CatchUpGroups + "/" + stats.CatchUpEvents
        );
        GUILayout.Label(
          "Partial retries / unsupported / waiter fallback: "
            + stats.PartialRetries
            + " / "
            + stats.UnsupportedEvents
            + " / "
            + stats.WaiterFallbacks
        );
      }
      else
      {
        GUILayout.Label("No active replay.");
      }
    }
  }

  private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
  {
    ModBootstrap.UpdateRuntime();
    UnityMainThread.DrainPending();
    ReplayLevelOpenService.Tick();
    ReplayPlaybackCoordinator.Tick();
    ReplaySessionService.TickStartup();
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
      MacOsInputMonitoringAccess.InitializeAtGameStart();
      Application.focusChanged += ReplaySessionService.OnApplicationFocusChanged;
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
    Application.focusChanged -= ReplaySessionService.OnApplicationFocusChanged;
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
