using System;
using TUFReplay.Application.Activity;
using TUFReplay.Features.Recording;
using TUFReplay.Infrastructure.Unity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Bootstrap;

public static class ModBootstrap
{
  private const int MigrationRetryFrames = 300;
  private static int _migrationDelayFrames;
  private static bool _migrationFinished;

  public static void InitializeRuntime()
  {
    DatabaseStore.Initialize();
    _migrationDelayFrames = 1;
    _migrationFinished = false;
  }

  public static void UpdateRuntime()
  {
    if (_migrationFinished || _migrationDelayFrames-- > 0)
      return;

    try
    {
      GameplayHashV3MigrationResult migration = GameplayHashV3Migration.Run();
      if (migration.Scanned > 0)
        Main.Instance.Log(
          "[Database] Gameplay hash migration scanned="
            + migration.Scanned
            + ", migrated="
            + migration.Migrated
            + ", skipped="
            + migration.Skipped
            + ", deferred="
            + migration.Deferred
            + ", retryable="
            + migration.Retryable
        );
      if (migration.Retryable > 0)
      {
        _migrationDelayFrames = MigrationRetryFrames;
        return;
      }
      _migrationFinished = true;
    }
    catch (Exception exception)
    {
      Main.Instance.LogException("Database gameplay hash v3 migration", exception);
      _migrationDelayFrames = MigrationRetryFrames;
    }
  }

  public static void Shutdown()
  {
    FeatureRegistry.Shutdown();
  }
}
