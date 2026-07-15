using TUFReplay.Features.Recording;
using TUFReplay.Infrastructure.Unity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Bootstrap;

public static class ModBootstrap
{
  public static void InitializeRuntime()
  {
    DatabaseStore.Initialize();
  }

  public static void Shutdown()
  {
    FeatureRegistry.Shutdown();
  }
}
