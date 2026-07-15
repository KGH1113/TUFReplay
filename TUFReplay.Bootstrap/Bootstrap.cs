using System;
using System.IO;
using UnityModManagerNet;

namespace TUFReplay.Bootstrap;

public static class Bootstrap
{
  private const string PayloadAssemblyName = "TUFReplay.dll";
  private const string PayloadEntryMethod = "TUFReplay.Main.Load";

  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    string displayName = modEntry.Info.DisplayName;
    string installedVersion = modEntry.Info.Version;
    PayloadCandidate installed = new(
      installedVersion,
      Path.Combine(modEntry.Path, PayloadAssemblyName));

    try
    {
      modEntry.Info.DisplayName = Status(modEntry, "Checking for updates...");

      UpdateManager updateManager = new(modEntry.Path);
      PayloadCandidate fallback = updateManager.GetActiveCandidate(installed) ?? installed;
      PayloadCandidate candidate = fallback;

      try
      {
        candidate = updateManager.GetLatestCandidate(fallback) ?? fallback;
      }
      catch (TimeoutException exception)
      {
        Warn(modEntry, "Update check exceeded 20 seconds. Loading the existing mod.", exception);
      }
      catch (Exception exception)
      {
        Warn(modEntry, "Update check failed. Loading the existing mod.", exception);
      }

      modEntry.Info.DisplayName = displayName;

      if (TryLoad(modEntry, candidate, installedVersion, out Exception loadException))
      {
        if (!PathsEqual(candidate.AssemblyPath, installed.AssemblyPath))
        {
          try
          {
            updateManager.MarkActive(candidate);
          }
          catch (Exception exception)
          {
            Warn(modEntry, "The updated mod loaded, but its cache marker could not be saved.", exception);
          }
        }
        return true;
      }

      if (!PathsEqual(candidate.AssemblyPath, installed.AssemblyPath))
      {
        Warn(modEntry, "The updated mod failed to load. Loading the installed version.", loadException);
        return TryLoadInstalled(modEntry, installed, installedVersion);
      }

      modEntry.Logger.Error(loadException?.ToString() ?? "TUFReplay failed to load.");
      return false;
    }
    catch (Exception exception)
    {
      modEntry.Info.DisplayName = displayName;
      Warn(modEntry, "The updater failed unexpectedly. Loading the installed version.", exception);
      return TryLoadInstalled(modEntry, installed, installedVersion);
    }
  }

  private static bool TryLoadInstalled(
    UnityModManager.ModEntry modEntry,
    PayloadCandidate installed,
    string installedVersion)
  {
    if (TryLoad(modEntry, installed, installedVersion, out Exception exception))
      return true;

    modEntry.Logger.Error(exception?.ToString() ?? "The installed TUFReplay payload failed to load.");
    return false;
  }

  private static bool TryLoad(
    UnityModManager.ModEntry modEntry,
    PayloadCandidate candidate,
    string installedVersion,
    out Exception exception)
  {
    try
    {
      modEntry.Info.Version = candidate.Version;
      PayloadLoader.Load(candidate.AssemblyPath, PayloadEntryMethod, modEntry);
      exception = null;
      return true;
    }
    catch (Exception caught)
    {
      modEntry.Info.Version = installedVersion;
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

  private static bool PathsEqual(string left, string right)
  {
    return string.Equals(
      Path.GetFullPath(left),
      Path.GetFullPath(right),
      StringComparison.OrdinalIgnoreCase);
  }
}
