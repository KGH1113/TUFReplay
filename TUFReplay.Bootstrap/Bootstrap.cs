using System;
using System.IO;
using UnityModManagerNet;

namespace TUFReplay.Bootstrap;

public static class Bootstrap
{
  private const string PayloadEntryMethod = "TUFReplay.Main.Load";

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    string displayName = modEntry.Info.DisplayName;
    RuntimeStore store = new(modEntry.Path);
    try
    {
      RuntimeState state = store.LoadAndRepair();
      RuntimeCandidate current = store.GetCandidate(state.Current);
      modEntry.Info.Version = current.Version;
      modEntry.Info.DisplayName = Status(modEntry, "Checking for updates...");

      UpdateResolution resolution;
      try
      {
        resolution = UpdateEngineLoader.Resolve(modEntry, current);
      }
      catch (Exception exception)
      {
        Warn(modEntry, "Update check failed. Loading the current runtime.", exception);
        resolution = UpdateResolution.None();
      }

      modEntry.Info.DisplayName = displayName;
      if (!resolution.HasCandidate)
        return TryLoadCurrent(modEntry, current);

      RuntimeCandidate trial = store.ValidateCandidate(resolution.Version, resolution.RuntimePath);
      state.Trial = trial.Version;
      store.Save(state);
      modEntry.Info.Version = trial.Version;
      if (TryLoad(modEntry, trial, out Exception loadException))
      {
        try
        {
          store.Promote(state, trial.Version);
        }
        catch (Exception exception)
        {
          Warn(modEntry, "The updated runtime loaded, but its active marker could not be saved.", exception);
        }
        return true;
      }

      state.Trial = null;
      store.Save(state);
      store.DeleteUnreferencedRuntime(trial.Version, state);
      modEntry.Info.Version = current.Version;
      modEntry.Info.DisplayName = displayName + " <color=red>[Failed to update!]</color>";
      Warn(modEntry, "The updated runtime failed to initialize. It will be retried next launch.", loadException);
      return false;
    }
    catch (Exception exception)
    {
      modEntry.Info.DisplayName = displayName + " <color=red>[Failed to update!]</color>";
      Warn(modEntry, "The runtime launcher failed.", exception);
      return false;
    }
  }

  private static bool TryLoadCurrent(UnityModManager.ModEntry modEntry, RuntimeCandidate current)
  {
    modEntry.Info.Version = current.Version;
    if (TryLoad(modEntry, current, out Exception exception))
      return true;
    modEntry.Logger.Error(exception?.ToString() ?? "The current TUFReplay runtime failed to load.");
    return false;
  }

  private static bool TryLoad(
    UnityModManager.ModEntry modEntry,
    RuntimeCandidate candidate,
    out Exception exception)
  {
    try
    {
      PayloadLoader.Load(candidate.AssemblyPath, PayloadEntryMethod, modEntry);
      exception = null;
      return true;
    }
    catch (Exception caught)
    {
      exception = caught;
      return false;
    }
  }

  private static void Warn(UnityModManager.ModEntry modEntry, string message, Exception exception)
  {
    modEntry.Logger.Warning("[AutoUpdate] " + message);
    if (exception != null)
      modEntry.Logger.Warning("[AutoUpdate] " + exception);
  }

  private static string Status(UnityModManager.ModEntry modEntry, string status)
  {
    return modEntry.Info.Id + " <color=grey>[" + status + "]</color>";
  }
}
