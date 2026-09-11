namespace TUFReplay.Replay.Models;

public sealed class ReplayArtifact
{
  public string RunId;
  public string EngineId = ReplayFormat.EngineId;
  public int FormatVersion = ReplayFormat.FormatVersion;
  public int InputCount;
  public int HitContextCount;
  public byte[] InputCsv;
  public byte[] HitContextCsv;
  public string MetadataJson;
}
