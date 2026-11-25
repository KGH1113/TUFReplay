using System;
using System.Collections.Generic;
using System.Globalization;
using ADOFAI;
using UnityEngine;

namespace TUFReplay.Activity.Charts;

/// <summary>Copies only what the V5 gameplay hash reads out of ADOFAI's level data.</summary>
internal static class GameplayChartSnapshotReader
{
  public static GameplayChartSnapshot Read(LevelData levelData)
  {
    var chart = new GameplayChartSnapshot
    {
      Bpm = levelData.bpm,
      Offset = levelData.offset,
      SeparateCountdownTime = levelData.separateCountdownTime,
      CountdownTicks = levelData.countdownTicks,
      LegacyPath = levelData.isOldLevel,
      PathData = levelData.pathData,
      Angles = levelData.angleData,
    };

    HashSet<string> hitboxTags = null;
    if (levelData.decorations != null)
    {
      foreach (LevelEvent decoration in levelData.decorations)
      {
        if (decoration == null || !HasHitbox(decoration))
          continue;
        chart.HitboxDecorations.Add(
          Copy(decoration, decoration.eventType.ToString(), GameplayChartV5Hasher.HitboxDecorationProperties, false)
        );
        GameplayChartV5Hasher.AddTags(
          decoration.GetData().TryGetValue("tag", out object tag) ? tag as string : null,
          hitboxTags ??= new HashSet<string>(StringComparer.Ordinal)
        );
      }
    }

    foreach (LevelEvent levelEvent in levelData.levelEvents)
    {
      string type = TypeName(levelEvent.eventType);
      if (type == null)
        continue;
      bool move = type == GameplayChartV5Hasher.MoveDecorations;
      // VFX charts move thousands of decorations; copy only moves of hitbox decorations.
      if (
        move
        && !GameplayChartV5Hasher.HasAnyTag(
          levelEvent.GetData().TryGetValue("tag", out object tag) ? tag as string : null,
          hitboxTags
        )
      )
        continue;
      chart.Events.Add(Copy(levelEvent, type, GameplayChartV5Hasher.PropertiesOf(type), move));
    }
    return chart;
  }

  // Kill and Event hitboxes take part in play (scrDecoration); HitboxType.None is 0.
  private static bool HasHitbox(LevelEvent decoration)
  {
    if (!decoration.GetData().TryGetValue("hitbox", out object value) || value == null)
      return false;
    if (value is Enum)
      return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
    return value is string text && text.Length > 0 && !string.Equals(text, "None", StringComparison.Ordinal);
  }

  private static string TypeName(LevelEventType type)
  {
    switch (type)
    {
      case LevelEventType.SetSpeed:
        return GameplayChartV5Hasher.SetSpeed;
      case LevelEventType.Twirl:
        return GameplayChartV5Hasher.Twirl;
      case LevelEventType.Hold:
        return GameplayChartV5Hasher.Hold;
      case LevelEventType.MultiPlanet:
        return GameplayChartV5Hasher.MultiPlanet;
      case LevelEventType.Pause:
        return GameplayChartV5Hasher.Pause;
      case LevelEventType.AutoPlayTiles:
        return GameplayChartV5Hasher.AutoPlayTiles;
      case LevelEventType.ScaleMargin:
        return GameplayChartV5Hasher.ScaleMargin;
      case LevelEventType.Multitap:
        return GameplayChartV5Hasher.Multitap;
      case LevelEventType.KillPlayer:
        return GameplayChartV5Hasher.KillPlayer;
      case LevelEventType.FreeRoam:
        return GameplayChartV5Hasher.FreeRoam;
      case LevelEventType.FreeRoamTwirl:
        return GameplayChartV5Hasher.FreeRoamTwirl;
      case LevelEventType.FreeRoamRemove:
        return GameplayChartV5Hasher.FreeRoamRemove;
      case LevelEventType.SetInputEvent:
        return GameplayChartV5Hasher.SetInputEvent;
      case LevelEventType.MoveDecorations:
        return GameplayChartV5Hasher.MoveDecorations;
      default:
        return null;
    }
  }

  private static GameplayChartItem Copy(LevelEvent source, string type, string[] properties, bool recordAbsent)
  {
    var item = new GameplayChartItem(type, source.floor, source.active);
    Dictionary<string, object> data = source.GetData();
    foreach (string property in properties)
    {
      if (data != null && data.TryGetValue(property, out object value))
        item.Set(property, ToPlainValue(value));
      if (recordAbsent && source.disabled != null && source.disabled.TryGetValue(property, out bool absent) && absent)
        item.MarkAbsent(property);
    }
    return item;
  }

  private static object ToPlainValue(object value)
  {
    switch (value)
    {
      case null:
      case bool _:
      case int _:
      case float _:
      case string _:
        return value;
      case double number:
        return (float)number;
      case long number:
        return (int)number;
      case Enum _:
        return value.ToString();
      case Vector2 vector:
        return new[] { vector.x, vector.y };
      case Vector3 vector:
        return new[] { vector.x, vector.y, vector.z };
      default:
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
  }
}
