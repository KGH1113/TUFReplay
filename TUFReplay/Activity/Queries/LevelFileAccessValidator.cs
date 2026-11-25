using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;

namespace TUFReplay.Activity.Queries;

public static class LevelFileAccessValidator
{
  public const string MissingMessage = "The level file was moved or deleted.";
  public const string InvalidMessage = "The level file could not be read.";
  public const string ModifiedMessage = "The level file's gameplay no longer matches this replay.";

  private static readonly LevelFileVerificationCache VerifiedFiles = new LevelFileVerificationCache(
    (string path, out string source, out GameplayChartHashes hashes) =>
      GameplayChartHash.TryLoadCustomLevel(path, out source, out _, out hashes, out _),
    path => RDFile.ReadAllText(path),
    capacity: 64
  );

  public static bool TryValidate(
    LevelSession session,
    out string canonicalPath,
    out string levelText,
    out string errorCode,
    out string errorMessage
  )
  {
    canonicalPath = LevelPathIdentity.Canonicalize(session?.LevelPath);
    levelText = null;
    errorCode = null;
    errorMessage = null;
    if (session == null)
      return Error("level_not_found", "The recorded level was not found.", out errorCode, out errorMessage);
    if (canonicalPath == null)
    {
      return Error("level_file_missing", MissingMessage, out errorCode, out errorMessage);
    }

    if (!VerifiedFiles.TryRead(canonicalPath, out levelText, out GameplayChartHashes actual))
    {
      return Error("level_file_invalid", InvalidMessage, out errorCode, out errorMessage);
    }

    // This is the session's own file, so it may supply hashes the record lacks.
    if (
      session.GameplayHash == null
      || GameplayChartHashMatch.CanTakeV5(
        session.GameplayHash,
        session.GameplayHashVersion,
        session.GameplayHashV5,
        actual
      )
    )
    {
      session.GameplayHash ??= actual.V4;
      session.GameplayHashVersion ??= GameplayChartHash.Version;
      session.GameplayHashV5 = actual.V5;
      session.LevelId = LevelRepository.UpdateGameplayHashesIfMissing(session.LevelId, actual);
      return true;
    }

    return GameplayChartHashMatch.Matches(
        session.GameplayHash,
        session.GameplayHashVersion,
        session.GameplayHashV5,
        actual
      ) || Error("level_gameplay_modified", ModifiedMessage, out errorCode, out errorMessage);
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
