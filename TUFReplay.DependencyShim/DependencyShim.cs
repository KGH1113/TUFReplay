using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityModManagerNet;

namespace TUFReplay.DependencyShim;

public static class DependencyShim
{
  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    string statePath = Path.Combine(modEntry.Path, "Launcher", "state.json");
    try
    {
      LauncherState state = LoadState(statePath);
      if (!string.IsNullOrWhiteSpace(state.Trial))
      {
        string trial = state.Trial;
        if (TryLoad(modEntry, trial, out Exception trialError))
        {
          if (!Equal(state.Current, trial)) state.Previous = state.Current;
          state.Current = trial;
          state.Trial = null;
          SaveState(statePath, state);
          return true;
        }
        modEntry.Logger.Warning("[DependencyShim] Bootstrap candidate " + trial + " failed: " + trialError);
      }

      if (TryLoad(modEntry, state.Current, out Exception currentError)) return true;
      if (!string.IsNullOrWhiteSpace(state.Previous) &&
          TryLoad(modEntry, state.Previous, out Exception previousError))
      {
        state.Current = state.Previous;
        state.Previous = null;
        SaveState(statePath, state);
        modEntry.Logger.Warning("[DependencyShim] Rolled back after: " + currentError);
        return true;
      }
      throw new InvalidOperationException("No usable AdofaiIpc dependency bootstrap is available.", currentError);
    }
    catch (Exception exception)
    {
      modEntry.Logger.Error("[DependencyShim] " + exception);
      return false;
    }
  }

  private static bool TryLoad(UnityModManager.ModEntry modEntry, string version, out Exception error)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(version) || version.Contains("/") || version.Contains("\\") ||
          version == "." || version == "..")
        throw new InvalidDataException("Dependency bootstrap state contains an invalid version.");
      string path = Path.Combine(modEntry.Path, "Launcher", "versions", version, "AdofaiIpc.Bootstrap.dll");
      if (!File.Exists(path)) throw new FileNotFoundException("Dependency bootstrap is missing.", path);
      ActivateManifest(modEntry.Path, Path.GetDirectoryName(path));
      Assembly assembly = Assembly.LoadFrom(path);
      Type type = assembly.GetType("AdofaiIpc.Bootstrap.Bootstrap", true);
      MethodInfo method = type.GetMethod(
        "LoadPrepared", BindingFlags.Public | BindingFlags.Static, null,
        new[] { typeof(UnityModManager.ModEntry) }, null)
        ?? type.GetMethod(
          "Load", BindingFlags.Public | BindingFlags.Static, null,
          new[] { typeof(UnityModManager.ModEntry) }, null)
        ?? throw new MissingMethodException(type.FullName, "LoadPrepared/Load");
      object result = method.Invoke(null, new object[] { modEntry });
      if (result is bool loaded && !loaded) throw new InvalidOperationException("Dependency bootstrap returned false.");
      error = null;
      return true;
    }
    catch (Exception exception)
    {
      error = exception is TargetInvocationException invocation && invocation.InnerException != null
        ? invocation.InnerException
        : exception;
      return false;
    }
  }

  private static void ActivateManifest(string installPath, string versionPath)
  {
    string source = Path.Combine(versionPath, "AdofaiIpcBootstrap.json");
    if (!File.Exists(source)) throw new FileNotFoundException("Versioned dependency manifest is missing.", source);
    string target = Path.Combine(installPath, "AdofaiIpcBootstrap.json");
    string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
    File.WriteAllText(temporary, File.ReadAllText(source), Encoding.UTF8);
    try
    {
      if (File.Exists(target)) File.Replace(temporary, target, null, true);
      else File.Move(temporary, target);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }

  private static LauncherState LoadState(string path)
  {
    string backup = path + ".bak";
    if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
    LauncherState state = JsonConvert.DeserializeObject<LauncherState>(File.ReadAllText(path));
    if (state == null || state.SchemaVersion != 1 || string.IsNullOrWhiteSpace(state.Current))
      throw new InvalidDataException("TUFReplay launcher state is invalid.");
    return state;
  }

  private static void SaveState(string path, LauncherState state)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
    string backup = path + ".bak";
    File.WriteAllText(temporary, JsonConvert.SerializeObject(state, Formatting.Indented) + Environment.NewLine, Encoding.UTF8);
    try
    {
      if (File.Exists(path))
      {
        if (File.Exists(backup)) File.Delete(backup);
        File.Replace(temporary, path, backup, true);
      }
      else File.Move(temporary, path);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }

  private static bool Equal(string left, string right) =>
    string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

  private sealed class LauncherState
  {
    public int SchemaVersion { get; set; } = 1;
    public string Current { get; set; }
    public string Previous { get; set; }
    public string Trial { get; set; }
  }
}
