using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Composition;

public static class ModBootstrap
{
  public static void InitializeRuntime()
  {
    DatabaseStore.Initialize();
  }

  public static void UpdateRuntime() { }

  public static void Shutdown()
  {
    FeatureRegistry.Shutdown();
  }
}
