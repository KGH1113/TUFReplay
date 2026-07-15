using ADOFAI;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Application.Replay;

public static class ReplayLevelHashValidator
{
  public static bool EnsureExpectedHash(StoredReplayRun run, out string errorCode, out string errorMessage)
  {
    errorCode = null;
    errorMessage = null;
    if (run == null)
      return Error("run_not_found", "The recorded run was not found.", out errorCode, out errorMessage);

    if (run.GameplayHash != null || run.GameplayHashVersion.HasValue)
    {
      return GameplayChartHash.IsSupported(run.GameplayHashVersion, run.GameplayHash)
        || Error(
          "level_hash_unsupported",
          "This run uses an unsupported gameplay hash.",
          out errorCode,
          out errorMessage
        );
    }

    string recordedPath = LevelPathIdentity.Canonicalize(run.LevelPath);
    if (recordedPath == null)
    {
      return Error(
        "level_hash_unavailable",
        "The original level file is required once to identify this older run.",
        out errorCode,
        out errorMessage
      );
    }

    if (!GameplayChartHash.TryLoad(recordedPath, out _, out byte[] hash, out string loadError))
      return Error("level_hash_failed", loadError, out errorCode, out errorMessage);

    run.GameplayHash = hash;
    run.GameplayHashVersion = GameplayChartHash.Version;
    RunRepository.UpdateGameplayHashIfMissing(run.Id, hash, GameplayChartHash.Version);
    return true;
  }

  public static bool ValidateTarget(
    StoredReplayRun run,
    string targetPath,
    out string canonicalPath,
    out string errorCode,
    out string errorMessage
  )
  {
    canonicalPath = LevelPathIdentity.Canonicalize(targetPath);
    errorCode = null;
    errorMessage = null;
    if (canonicalPath == null)
      return Error("level_unavailable", "The selected level file is unavailable.", out errorCode, out errorMessage);

    if (!EnsureExpectedHash(run, out errorCode, out errorMessage))
      return false;

    if (!GameplayChartHash.TryLoad(canonicalPath, out _, out byte[] actualHash, out string loadError))
      return Error("level_hash_failed", loadError, out errorCode, out errorMessage);

    return GameplayChartHash.Equals(run.GameplayHash, actualHash)
      || Error(
        "level_gameplay_mismatch",
        "The selected level does not have the same tiles and gameplay events.",
        out errorCode,
        out errorMessage
      );
  }

  public static bool ValidateLoaded(
    StoredReplayRun run,
    LevelData levelData,
    out string errorCode,
    out string errorMessage
  )
  {
    errorCode = null;
    errorMessage = null;
    if (!EnsureExpectedHash(run, out errorCode, out errorMessage))
      return false;
    if (!GameplayChartHash.TryCompute(levelData, out byte[] actualHash, out string hashError))
      return Error("level_hash_failed", hashError, out errorCode, out errorMessage);
    return GameplayChartHash.Equals(run.GameplayHash, actualHash)
      || Error(
        "level_gameplay_mismatch",
        "The loaded level no longer matches the recorded gameplay.",
        out errorCode,
        out errorMessage
      );
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
