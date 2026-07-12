using System.Collections.Generic;
using Newtonsoft.Json;
using TUFReplay.Domain.Activity;

namespace TUFReplay.Ipc.Dtos;

public sealed class ActivityDaySummaryDto
{
  public string Date;
  public int AppSessionCount;
  public int LevelSessionCount;
  public int RunCount;
  public int NoFailRunCount;
  public int UniqueLevelCount;
  public string StartedAtUtc;
  public string EndedAtUtc;

  public static ActivityDaySummaryDto From(ActivityDaySummary summary)
  {
    return new ActivityDaySummaryDto
    {
      Date = summary.Date,
      AppSessionCount = summary.AppSessionCount,
      LevelSessionCount = summary.LevelSessionCount,
      RunCount = summary.RunCount,
      NoFailRunCount = summary.NoFailRunCount,
      UniqueLevelCount = summary.UniqueLevelCount,
      StartedAtUtc = summary.StartedAtUtc,
      EndedAtUtc = summary.EndedAtUtc
    };
  }
}

public sealed class ActivityDayDto
{
  public string Date;
  public ActivityDaySummaryDto Summary;
  public List<ActivityAppSessionDto> AppSessions;
}

public sealed class ActivityAppSessionDto
{
  public string Id;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public List<ActivityLevelSessionOverviewDto> LevelSessions;

  public static ActivityAppSessionDto From(AppSession session, List<ActivityLevelSessionOverviewDto> levelSessions)
  {
    return new ActivityAppSessionDto
    {
      Id = session.Id,
      StartedAtUtc = session.StartedAtUtc,
      EndedAtUtc = session.EndedAtUtc,
      LevelSessions = levelSessions
    };
  }
}

public sealed class ActivityLevelSessionDto
{
  public ActivityLevelSessionOverviewDto Session;
  public List<ActivitySegmentGroupDto> SegmentGroups;
}

public sealed class ActivityLevelSessionOverviewDto
{
  public string Id;
  public string AppSessionId;
  public int TufLevelId;
  public string OpenedAtUtc;
  public string ClosedAtUtc;
  public int LevelTileCount;
  public int RunCount;
  public int NoFailRunCount;
  public int? FirstStartTile;
  public int? LastStartTile;

  public static ActivityLevelSessionOverviewDto From(LevelSessionOverview session)
  {
    return new ActivityLevelSessionOverviewDto
    {
      Id = session.Id,
      AppSessionId = session.AppSessionId,
      TufLevelId = session.TufLevelId,
      OpenedAtUtc = session.OpenedAtUtc,
      ClosedAtUtc = session.ClosedAtUtc,
      LevelTileCount = session.LevelTileCount,
      RunCount = session.RunCount,
      NoFailRunCount = session.NoFailRunCount,
      FirstStartTile = session.FirstStartTile,
      LastStartTile = session.LastStartTile
    };
  }
}

public sealed class ActivitySegmentGroupDto
{
  public int SegmentGroupIndex;
  public int StartTile;
  public int AttemptCount;
  public int BestLastTile;
  public string FirstStartedAtUtc;
  public string LastStartedAtUtc;

  public static ActivitySegmentGroupDto From(SegmentGroupSummary group)
  {
    return new ActivitySegmentGroupDto
    {
      SegmentGroupIndex = group.SegmentGroupIndex,
      StartTile = group.StartTile,
      AttemptCount = group.AttemptCount,
      BestLastTile = group.BestLastTile,
      FirstStartedAtUtc = group.FirstStartedAtUtc,
      LastStartedAtUtc = group.LastStartedAtUtc
    };
  }
}

public sealed class ActivityRunDto
{
  public string Id;
  public string AppSessionId;
  public string LevelSessionId;
  public int TufLevelId;
  public int RunIndex;
  public int SegmentGroupIndex;
  public string StartedAtUtc;
  public string EndedAtUtc;
  public int LevelTileCount;
  public int StartTile;
  public int? LastTile;
  public string Result;
  public bool NoFailMode;
  public double? GameplayStartSongPosition;
  public int? LevelPitchPercent;
  public float? EffectivePitch;
  public int InputCount;
  public int HitContextCount;
  public long InputCsvBytes;
  public long HitContextCsvBytes;
  public object Meta;

  public static ActivityRunDto From(RunRecord run)
  {
    return new ActivityRunDto
    {
      Id = run.Id,
      AppSessionId = run.AppSessionId,
      LevelSessionId = run.LevelSessionId,
      TufLevelId = run.TufLevelId,
      RunIndex = run.RunIndex,
      SegmentGroupIndex = run.SegmentGroupIndex,
      StartedAtUtc = run.StartedAtUtc,
      EndedAtUtc = run.EndedAtUtc,
      LevelTileCount = run.LevelTileCount,
      StartTile = run.StartTile,
      LastTile = run.LastTile,
      Result = run.Result,
      NoFailMode = run.NoFailMode,
      GameplayStartSongPosition = run.GameplayStartSongPosition,
      LevelPitchPercent = run.LevelPitchPercent,
      EffectivePitch = run.EffectivePitch,
      InputCount = run.InputCount,
      HitContextCount = run.HitContextCount,
      InputCsvBytes = run.InputCsv?.LongLength ?? 0,
      HitContextCsvBytes = run.HitContextCsv?.LongLength ?? 0,
      Meta = ParseMeta(run.MetaJson)
    };
  }

  private static object ParseMeta(string metaJson)
  {
    if (string.IsNullOrEmpty(metaJson)) return null;

    try
    {
      return JsonConvert.DeserializeObject(metaJson);
    }
    catch
    {
      return metaJson;
    }
  }
}
