namespace TUFReplay.Submission.Catalog;

public sealed class InstalledLevelContext
{
  public int LevelId { get; }
  public string FileId { get; }
  public string PayloadHash { get; }
  public string RelativePath { get; }

  public InstalledLevelContext(int levelId, string fileId, string payloadHash, string relativePath)
  { LevelId = levelId; FileId = fileId; PayloadHash = payloadHash; RelativePath = relativePath; }
}
