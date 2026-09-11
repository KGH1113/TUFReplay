namespace TUFReplay.Activity.Models;

public enum RunJudgmentDifficulty
{
  Lenient = 0,
  Normal = 1,
  Strict = 2,
}

public sealed class JudgmentCounts
{
  public int Overload;
  public int TooEarly;
  public int Early;
  public int EarlyPerfect;
  public int Perfect;
  public int PerfectMinus;
  public int XPerfect;
  public int PerfectPlus;
  public int LatePerfect;
  public int Late;
  public int TooLate;
  public int Miss;
}
