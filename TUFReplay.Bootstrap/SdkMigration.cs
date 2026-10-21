using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace TUFReplay.BundledIpc;

// Shared by the payload and versioned launcher; it deliberately has no SDK references.
internal static class SdkMigration
{
  internal const string Launcher = "TUFReplay.SdkLauncher.dll";
  private const string EntryMethod = "TUFReplay.Bootstrap.Bootstrap.Load";

  internal static void Prepare(string payload, string install)
  {
    Validate(payload);
    BundledIpcFiles.Copy(payload, install);
    string version = (string)JObject.Parse(File.ReadAllText(Path.Combine(payload, "Info.json")))["Version"];
    BundledIpcFiles.CopyFile(Path.Combine(payload, Launcher), Path.Combine(install, LauncherPath(version)));
    CleanupRetiredIpc(install);
  }

  private static void CleanupRetiredIpc(string install)
  {
    try
    {
      DirectoryInfo mods = Directory.GetParent(
        Path.GetFullPath(install.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
      );
      string retired = Path.Combine(mods.Parent.FullName, ".adofai-ipc-retired");
      if (!Directory.Exists(retired) || IsLink(retired))
        return;
      foreach (string directory in Directory.GetDirectories(retired, "AdofaiIpc-*"))
        if (SafeTree(directory))
          Directory.Delete(directory, true);
      if (Directory.GetFileSystemEntries(retired).Length == 0)
        Directory.Delete(retired);
    }
    catch
    {
      // Windows can still hold a retired DLL open. A later launch retries only this known directory.
    }
  }

  private static bool SafeTree(string directory)
  {
    if (IsLink(directory))
      return false;
    foreach (string entry in Directory.GetFileSystemEntries(directory))
    {
      if (IsLink(entry))
        return false;
      if (Directory.Exists(entry) && !SafeTree(entry))
        return false;
    }
    return true;
  }

  private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

  internal static void Validate(string payload)
  {
    BundledIpcFiles.Validate(payload);
    string launcher = Path.Combine(payload, Launcher);
    if (AssemblyName.GetAssemblyName(launcher).Name != "TUFReplay.SdkLauncher")
      throw new InvalidDataException("The SDK launcher identity is invalid.");
    JObject info = JObject.Parse(File.ReadAllText(Path.Combine(payload, "Info.json")));
    if (
      (string)info["Id"] != "TUFReplay"
      || (string)info["AssemblyName"] != Launcher
      || (string)info["EntryMethod"] != EntryMethod
    )
      throw new InvalidDataException("The SDK runtime entrypoint is invalid.");
    LauncherPath((string)info["Version"]);
  }

  private static string LauncherPath(string version)
  {
    if (string.IsNullOrEmpty(version))
      throw new InvalidDataException("The SDK launcher version is missing.");
    foreach (char character in version)
      if (!char.IsLetterOrDigit(character) && character != '.' && character != '-')
        throw new InvalidDataException("The SDK launcher version is invalid.");
    if (version == "." || version == "..")
      throw new InvalidDataException("The SDK launcher version is invalid.");
    return "Runtime/launchers/" + version + "/" + Launcher;
  }

  internal static void Commit(string payload, string install)
  {
    // Info is the commit point. A partial copy never removes the working dependency entrypoint.
    Prepare(payload, install);
    string source = Path.Combine(payload, "Info.json");
    string destination = Path.Combine(install, "Info.json");
    JObject info = JObject.Parse(File.ReadAllText(source));
    string statePath = Path.Combine(install, "Runtime", "state.json");
    JObject state = JObject.Parse(File.ReadAllText(statePath));
    state["Current"] = (string)info["Version"];
    state["Previous"] = null;
    state["Trial"] = null;
    WriteAtomic(statePath, state.ToString() + Environment.NewLine);
    info["AssemblyName"] = LauncherPath((string)info["Version"]);
    WriteAtomic(destination, info.ToString() + Environment.NewLine);
  }

  private static void WriteAtomic(string path, string text)
  {
    if (File.Exists(path) && File.ReadAllText(path) == text)
      return;
    string temporary = path + ".sdk-" + Guid.NewGuid().ToString("N");
    try
    {
      File.WriteAllText(temporary, text);
      if (File.Exists(path))
        File.Replace(temporary, path, null);
      else
        File.Move(temporary, path);
    }
    finally
    {
      if (File.Exists(temporary))
        File.Delete(temporary);
    }
  }
}
