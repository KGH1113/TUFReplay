using System.Collections.Generic;
using TUFReplay.Domain.Activity;

namespace TUFReplay.Ipc.Dtos;

public sealed class ActivityAppSessionDto
{
  public string Id;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public string RecorderTimeZoneId;
  public int RecorderUtcOffsetMinutes;
  public List<ActivityLevelSessionOverviewDto> LevelSessions;

  public static ActivityAppSessionDto From(AppSession s, List<ActivityLevelSessionOverviewDto> levels) =>
    new ActivityAppSessionDto
    {
      Id = s.Id,
      StartedAtUtc = s.StartedAtUtc,
      EndedAtUtc = s.EndedAtUtc,
      RecorderTimeZoneId = s.RecorderTimeZoneId,
      RecorderUtcOffsetMinutes = s.RecorderUtcOffsetMinutes,
      LevelSessions = levels,
    };
}

public sealed class ActivityLevelSessionOverviewDto
{
  public string Id;
  public string AppSessionId;
  public int? TufLevelId;
  public string Song;
  public string Author;
  public string Artist;
  public string OpenedAtUtc;
  public string ClosedAtUtc;
  public int FloorCount;
  public int RunCount;
  public int ClearRunCount;
  public int NoFailRunCount;
  public int? FirstStartTile;
  public int? LastStartTile;
  public bool ChartAvailable;

  public static ActivityLevelSessionOverviewDto From(LevelSessionOverview s) =>
    new ActivityLevelSessionOverviewDto
    {
      Id = s.Id,
      AppSessionId = s.AppSessionId,
      TufLevelId = s.TufLevelId,
      Song = s.Song,
      Author = s.Author,
      Artist = s.Artist,
      OpenedAtUtc = s.OpenedAtUtc,
      ClosedAtUtc = s.ClosedAtUtc,
      FloorCount = s.LevelTileCount,
      RunCount = s.RunCount,
      ClearRunCount = s.ClearRunCount,
      NoFailRunCount = s.NoFailRunCount,
      FirstStartTile = s.FirstStartTile,
      LastStartTile = s.LastStartTile,
      ChartAvailable = s.ChartAvailable,
    };
}

public sealed class ActivityRunDto
{
  public string Id;
  public string LevelSessionId;
  public int? TufLevelId;
  public int RunIndex;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public int FloorCount;
  public int StartTile;
  public int? LastTile;
  public string Result;
  public bool NoFailMode;
  public double? GameplayStartSongPosition;
  public int? LevelPitchPercent;
  public float? EffectivePitch;
  public float? XAccuracy;
  public string JudgmentDifficulty;
  public JudgmentCountsDto JudgmentCounts;
  public int InputCount;
  public int HitContextCount;
  public long InputBytes;
  public long HitContextBytes;

  public static ActivityRunDto From(RunRecord r) =>
    new ActivityRunDto
    {
      Id = r.Id,
      LevelSessionId = r.LevelSessionId,
      TufLevelId = r.TufLevelId,
      RunIndex = r.RunIndex,
      StartedAtUtc = r.StartedAtUtc,
      EndedAtUtc = r.EndedAtUtc,
      FloorCount = r.LevelTileCount,
      StartTile = r.StartTile,
      LastTile = r.LastTile,
      Result = r.Result,
      NoFailMode = r.NoFailMode,
      GameplayStartSongPosition = r.GameplayStartSongPosition,
      LevelPitchPercent = r.LevelPitchPercent,
      EffectivePitch = r.EffectivePitch,
      XAccuracy = r.XAccuracy,
      JudgmentDifficulty = r.JudgmentDifficulty?.ToString(),
      JudgmentCounts = JudgmentCountsDto.From(r.JudgmentCounts),
      InputCount = r.InputCount,
      HitContextCount = r.HitContextCount,
      InputBytes = r.InputCsvBytes,
      HitContextBytes = r.HitContextCsvBytes,
    };
}

public sealed class JudgmentCountsDto
{
  public int Overload;
  public int TooEarly;
  public int Early;
  public int EarlyPerfect;
  public int Perfect;
  public int LatePerfect;
  public int Late;
  public int TooLate;
  public int Miss;

  public static JudgmentCountsDto From(JudgmentCounts counts)
  {
    counts ??= new JudgmentCounts();
    return new JudgmentCountsDto
    {
      Overload = counts.Overload,
      TooEarly = counts.TooEarly,
      Early = counts.Early,
      EarlyPerfect = counts.EarlyPerfect,
      Perfect = counts.Perfect,
      LatePerfect = counts.LatePerfect,
      Late = counts.Late,
      TooLate = counts.TooLate,
      Miss = counts.Miss,
    };
  }
}

public sealed class ActivityChartDto
{
  public string LevelSessionId;
  public string LevelText;
  public int FloorCount;
}
