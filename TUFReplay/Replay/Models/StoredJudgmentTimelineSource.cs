using TUFReplay.Activity.Models;

namespace TUFReplay.Replay.Models;

/// <summary>The parts of a saved run needed to draw its judgment timeline.</summary>
public sealed class StoredJudgmentTimelineSource
{
  public string Id;
  public string Result;
  public RunJudgmentSystem JudgmentSystem;
  public string MetaJson;
  public byte[] HitContextCsv;
}
