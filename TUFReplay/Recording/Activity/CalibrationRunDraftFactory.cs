using System;
using TUFReplay.Activity.Models;
using TUFReplay.Replay.Models;

namespace TUFReplay.Recording.Activity;

internal static class CalibrationRunDraftFactory
{
  public static RunRecord Create(RecordedRunPayload data, int startTile, int levelTileCount)
  {
    return new RunRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      RunIndex = 0,
      SegmentGroupIndex = 0,
      StartedAtUtc = data.StartedAtUtc,
      LevelTileCount = levelTileCount,
      StartTile = startTile,
      NoFailMode = data.NoFailMode,
      GameplayStartSongPosition = data.GameplayStartSongPosition,
      LevelPitchPercent = data.LevelPitchPercent,
      EffectivePitch = data.EffectivePitch,
      GameplayHash = data.GameplayHash == null ? null : (byte[])data.GameplayHash.Clone(),
      GameplayHashVersion = data.GameplayHashVersion,
      MetaJson = data.ToActivityMetaJson(),
    };
  }
}
