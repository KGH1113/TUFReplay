using System.IO;
using AdofaiIpc.Contracts;
using AdofaiIpc.Loader;

namespace TUFReplay.Shared.Ipc;

// Load the bundled runtime; generated typed APIs remain owned by this mod.
public static class IpcRuntime
{
  public static IIpcRuntime Current { get; private set; }

  public static void Connect(string modRoot)
  {
    string mods = Directory
      .GetParent(Path.GetFullPath(modRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
      .FullName;
    Current = IpcLoader.GetRuntime(
      mods,
      new RuntimeRequirements(
        minimumRuntimeVersion: "2.1.0",
        requiredCapabilities: new[] { "commands", "game-thread", "snapshots", "downloads", "typed-files" }
      )
    );
  }
}
