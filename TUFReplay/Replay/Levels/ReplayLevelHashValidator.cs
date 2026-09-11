using ADOFAI;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Queries;
using TUFReplay.Activity.Repositories;
using TUFReplay.Replay.Models;

namespace TUFReplay.Replay.Levels;

public static class ReplayLevelHashValidator
{
  private static bool ValidateStoredHash(StoredReplayRun run, out string errorCode, out string errorMessage)
  {
    errorCode = null;
    errorMessage = null;
    if (run == null)
      return Error("run_not_found", "The recorded run was not found.", out errorCode, out errorMessage);

    if (run.GameplayHash == null && !run.GameplayHashVersion.HasValue)
      return true;

    return GameplayChartHash.IsSupported(run.GameplayHashVersion, run.GameplayHash)
      || Error(
        "level_hash_unsupported",
        "This run uses an unsupported gameplay hash.",
        out errorCode,
        out errorMessage
      );
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
      return Error("level_file_missing", LevelFileAccessValidator.MissingMessage, out errorCode, out errorMessage);

    return ValidateStoredHash(run, out errorCode, out errorMessage);
  }

  public static bool ValidateLoaded(
    StoredReplayRun run,
    LevelData levelData,
    string loadedPath,
    out string errorCode,
    out string errorMessage
  )
  {
    errorCode = null;
    errorMessage = null;
    if (!ValidateStoredHash(run, out errorCode, out errorMessage))
      return false;
    if (!GameplayChartHash.TryCompute(levelData, out byte[] actualHash, out string hashError))
      return Error("level_file_invalid", hashError, out errorCode, out errorMessage);

    if (run.GameplayHash == null)
    {
      string canonicalLoadedPath = LevelPathIdentity.Canonicalize(loadedPath);
      string canonicalRecordedPath = LevelPathIdentity.Canonicalize(run.LevelPath);
      if (canonicalRecordedPath == null || !LevelPathIdentity.Equals(canonicalLoadedPath, canonicalRecordedPath))
      {
        return Error(
          "level_hash_unavailable",
          "The original level file is required once to identify this older run.",
          out errorCode,
          out errorMessage
        );
      }

      run.GameplayHash = actualHash;
      run.GameplayHashVersion = GameplayChartHash.Version;
      RunRepository.UpdateGameplayHashIfMissing(run.Id, actualHash, GameplayChartHash.Version);
      return true;
    }

    return GameplayChartHash.Equals(run.GameplayHash, actualHash)
      || MatchesVerifiedOriginalSemantics(run, run.GameplayHash, levelData)
      || Error("level_gameplay_modified", LevelFileAccessValidator.ModifiedMessage, out errorCode, out errorMessage);
  }

  public static bool MatchesVerifiedOriginalSemantics(
    StoredReplayRun run,
    byte[] referenceHash,
    LevelData candidateLevelData
  )
  {
    string originalPath = LevelPathIdentity.Canonicalize(run?.LevelPath);
    if (originalPath == null || referenceHash == null || candidateLevelData == null)
      return false;

    if (
      !GameplayChartHash.TryLoadCustomLevel(
        originalPath,
        out LevelData originalLevelData,
        out byte[] originalHash,
        out _
      )
      || !GameplayChartHash.Equals(referenceHash, originalHash)
      || !GameplayChartHash.TryComputeSemantic(originalLevelData, out byte[] originalSemanticHash, out _)
      || !GameplayChartHash.TryComputeSemantic(candidateLevelData, out byte[] candidateSemanticHash, out _)
    )
      return false;

    return GameplayChartHash.Equals(originalSemanticHash, candidateSemanticHash);
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
