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

    bool hasV4 = run.GameplayHash != null || run.GameplayHashVersion.HasValue;
    if (!hasV4 && run.GameplayHashV5 == null)
      return true;

    return (!hasV4 || GameplayChartHash.IsSupported(run.GameplayHashVersion, run.GameplayHash))
        && (run.GameplayHashV5 == null || GameplayChartHash.IsV5(run.GameplayHashV5))
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
  ) => ValidateLoaded(run, levelData, loadedPath, out errorCode, out errorMessage, out _);

  // loadedHashes are the loaded level's own hashes, so callers need not hash it again.
  public static bool ValidateLoaded(
    StoredReplayRun run,
    LevelData levelData,
    string loadedPath,
    out string errorCode,
    out string errorMessage,
    out GameplayChartHashes loadedHashes
  )
  {
    errorCode = null;
    errorMessage = null;
    loadedHashes = null;
    if (!ValidateStoredHash(run, out errorCode, out errorMessage))
      return false;
    if (!GameplayChartHash.TryCompute(levelData, out GameplayChartHashes actual, out string hashError))
      return Error("level_file_invalid", hashError, out errorCode, out errorMessage);
    loadedHashes = actual;

    if (run.GameplayHash == null && run.GameplayHashV5 == null)
    {
      if (!IsRecordedFile(run, loadedPath))
      {
        return Error(
          "level_hash_unavailable",
          "The original level file is required once to identify this older run.",
          out errorCode,
          out errorMessage
        );
      }

      run.GameplayHash = actual.V4;
      run.GameplayHashVersion = GameplayChartHash.Version;
      run.GameplayHashV5 = actual.V5;
      RunRepository.UpdateGameplayHashesIfMissing(run.Id, actual);
      return true;
    }

    return MatchesRun(run, actual, loadedPath)
      || Error("level_gameplay_modified", LevelFileAccessValidator.ModifiedMessage, out errorCode, out errorMessage);
  }

  /// <summary>
  /// Whether a chart plays the same as the recorded one. A run recorded before V5 has
  /// only V4, which rejects charts that differ in effects-only settings such as song
  /// volume; the recorded file, when it still matches, supplies V5 once for good.
  /// </summary>
  public static bool MatchesRun(StoredReplayRun run, GameplayChartHashes candidate, string candidatePath)
  {
    if (run == null || candidate == null)
      return false;
    if (GameplayChartHashMatch.Matches(run.GameplayHash, run.GameplayHashVersion, run.GameplayHashV5, candidate))
    {
      if (IsRecordedFile(run, candidatePath))
        TakeV5(run, candidate);
      return true;
    }
    if (run.GameplayHashV5 != null)
      return false;
    return TryTakeV5FromRecordedFile(run) && GameplayChartHash.Equals(run.GameplayHashV5, candidate.V5);
  }

  private static bool TryTakeV5FromRecordedFile(StoredReplayRun run)
  {
    string recordedPath = LevelPathIdentity.Canonicalize(run.LevelPath);
    if (
      recordedPath == null
      || !GameplayChartHash.TryLoadCustomLevel(recordedPath, out _, out GameplayChartHashes recorded, out _)
    )
      return false;
    TakeV5(run, recorded);
    return run.GameplayHashV5 != null;
  }

  private static void TakeV5(StoredReplayRun run, GameplayChartHashes recorded)
  {
    if (!GameplayChartHashMatch.CanTakeV5(run.GameplayHash, run.GameplayHashVersion, run.GameplayHashV5, recorded))
      return;
    run.GameplayHashV5 = recorded.V5;
    RunRepository.UpdateGameplayHashesIfMissing(run.Id, recorded);
  }

  private static bool IsRecordedFile(StoredReplayRun run, string path)
  {
    string recordedPath = LevelPathIdentity.Canonicalize(run.LevelPath);
    return recordedPath != null && LevelPathIdentity.Equals(LevelPathIdentity.Canonicalize(path), recordedPath);
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
