using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Application.Activity;

public static class LevelFileAccessValidator
{
  public const string MissingMessage = "The level file was moved or deleted.";
  public const string InvalidMessage = "The level file could not be read.";
  public const string ModifiedMessage = "The level file's gameplay no longer matches this replay.";

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

    int hashVersion = session.GameplayHashVersion ?? GameplayChartHash.Version;
    if (
      !GameplayChartHash.TryLoadCustomLevel(
        canonicalPath,
        hashVersion,
        out levelText,
        out _,
        out byte[] actualHash,
        out _
      )
    )
    {
      return Error("level_file_invalid", InvalidMessage, out errorCode, out errorMessage);
    }

    if (session.GameplayHash == null)
    {
      session.GameplayHash = actualHash;
      session.GameplayHashVersion = hashVersion;
      session.LevelId = LevelRepository.UpdateGameplayHashIfMissing(session.LevelId, actualHash, hashVersion);
      return true;
    }

    return GameplayChartHash.Equals(session.GameplayHash, actualHash)
      || Error("level_gameplay_modified", ModifiedMessage, out errorCode, out errorMessage);
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
