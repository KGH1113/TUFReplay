using System.Collections.Generic;
using System.IO;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Adofai;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Activity;

public static class ActivityQueryService
{
  public static List<AppSession> ListAppSessions(int offset, int limit) => AppSessionRepository.List(offset, limit);

  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSession(string id)
  {
    List<LevelSessionOverview> levels = ActivityRepository.ListLevelSessionOverviewsByAppSession(id);
    foreach (LevelSessionOverview level in levels)
      EnsureLevelMetadata(level);
    return levels;
  }

  public static LevelSessionOverview GetLevelSessionOverview(string id)
  {
    LevelSessionOverview level = ActivityRepository.GetLevelSessionOverview(id);
    EnsureLevelMetadata(level);
    return level;
  }

  public static List<RunRecord> ListRunsByLevelSession(string id, int offset, int limit) =>
    RunRepository.ListByLevelSession(id, offset, limit);

  public static ChartData GetChart(string id)
  {
    LevelSession s = LevelSessionRepository.Get(id);
    if (s == null)
      return null;
    if (string.IsNullOrEmpty(s.LevelPath) || !File.Exists(s.LevelPath))
      return new ChartData { id = id, floorCount = s.LevelTileCount };
    return new ChartData
    {
      id = id,
      levelText = File.ReadAllText(s.LevelPath),
      floorCount = s.LevelTileCount,
    };
  }

  private static void EnsureLevelMetadata(LevelSessionOverview level)
  {
    if (level == null || level.MetadataState != LevelMetadataState.Pending)
      return;

    bool captured = AdofaiLevelMetadataReader.TryRead(level.LevelPath, out LevelMetadataSnapshot metadata);
    LevelMetadataState state = captured ? LevelMetadataState.Captured : LevelMetadataState.Unavailable;
    LevelSessionRepository.UpdateMetadata(level.Id, metadata, state);
    level.Song = metadata?.Song;
    level.Author = metadata?.Author;
    level.Artist = metadata?.Artist;
    level.MetadataState = state;
  }
}

public sealed class ChartData
{
  public string id;
  public string levelText;
  public int floorCount;
}
