using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TUFReplay.Composition;
using TUFReplay.Microphone.Permissions;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Build;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Playback;
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
  private Task _pendingActivityWrites = Task.CompletedTask;

  private Main(UnityModManager.ModEntry modEntry)
  {
    ModEntry = modEntry;
    _updateSettingsPath = System.IO.Path.Combine(InstallPath, "UpdateSettings.json");
  }

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    try
    {
      string payload = System.IO.Path.GetDirectoryName(typeof(Main).Assembly.Location);
      TUFReplay.BundledIpc.SdkMigration.Prepare(payload, modEntry.Path);
      if (!LoadCore(modEntry))
        return false;
      try
      {
        TUFReplay.BundledIpc.SdkMigration.Commit(payload, modEntry.Path);
        AppDomain.CurrentDomain.SetData("AdofaiIpc.SdkMigration.TUFReplay", true);
        modEntry.Logger.Log("[SdkMigration] SDK active; standalone IPC installation is no longer required.");
      }
      catch (Exception exception)
      {
        // The payload is running. Retain the old entrypoint and IPC until a later launch retries the commit.
        modEntry.Logger.Warning("[SdkMigration] SDK active, but entrypoint migration will retry: " + exception);
      }
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error("[SdkMigration] " + exception);
      return false;
    }
  }

  // The deployed v1 launcher enters Load directly. Keep SDK type resolution behind file preparation.
  [MethodImpl(MethodImplOptions.NoInlining)]
  private static bool LoadCore(UnityModManager.ModEntry modEntry)
  {
    try
    {
      IpcRuntime.Connect(modEntry.Path);

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
      Application.quitting += OnApplicationQuitting;
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
    if (TUFReplayBuildFlavor.Name == "auto-submission")
      return;
    bool receiveBetaUpdates = GUILayout.Toggle(UpdaterSettings.ReceiveBetaUpdates, "Receive beta updates");
    if (receiveBetaUpdates != UpdaterSettings.ReceiveBetaUpdates)
    {
      UpdaterSettings.ReceiveBetaUpdates = receiveBetaUpdates;
      SaveUpdateSettings(modEntry);
    }
  }

  private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
  {
    GameplayJudgingMonitor.Tick();
    ModBootstrap.UpdateRuntime();
    UnityMainThread.DrainPending();
    MicrophonePermissionWarningCoordinator.Tick();
    ReplayLevelOpenService.Tick();
    ReplayPlaybackCoordinator.Tick();
    ReplaySessionService.TickStartup();
    ReplaySessionService.TickWebcamPlayback();
    CameraFirstRunCoordinator.Tick();
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
      Instance?.FlushPendingActivityWrites();
      return true;
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error(exception.ToString());
      return false;
    }
    finally
    {
      Application.quitting -= OnApplicationQuitting;
      UnityMainThread.Shutdown();
    }
  }

  private static void OnApplicationQuitting()
  {
    try
    {
      Instance?.Disable();
      Instance?.FlushPendingActivityWrites();
    }
    catch (Exception exception)
    {
      Instance?.LogException("Recording/Shutdown", exception);
    }
  }

  private void FlushPendingActivityWrites()
  {
    if (!_pendingActivityWrites.Wait(TimeSpan.FromSeconds(5)))
      Log("[Recording] Activity persistence did not finish within the shutdown timeout.");
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
    var recording = FeatureRegistry.Recording;
    try
    {
      ModBootstrap.Shutdown();
    }
    finally
    {
      if (recording != null)
        _pendingActivityWrites = Task.WhenAll(_pendingActivityWrites, recording.PendingActivityWrites);
      NativeInputUmmWindowInterlock.Reset();
      TUFReplaySettingStore.Save();
    }
  }
}
