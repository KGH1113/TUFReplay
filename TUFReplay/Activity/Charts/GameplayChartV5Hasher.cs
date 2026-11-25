using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TUFReplay.Activity.Charts;

/// <summary>
/// Hashes what decides judgments and run outcomes, following ADOFAI's own level build
/// (scnGame.ApplyCoreEventsToFloors, scrLevelMaker.CalculateFloorEntryTimes, scrPlayer).
/// Effects, song volume and file name, the speed trial target, disabled events and the
/// order of events on different tiles do not change it, so a VFX and a plain version of
/// one chart match.
/// </summary>
internal static class GameplayChartV5Hasher
{
  public const int Version = 5;

  public const string SetSpeed = "SetSpeed";
  public const string Twirl = "Twirl";
  public const string Hold = "Hold";
  public const string MultiPlanet = "MultiPlanet";
  public const string Pause = "Pause";
  public const string AutoPlayTiles = "AutoPlayTiles";
  public const string ScaleMargin = "ScaleMargin";
  public const string Multitap = "Multitap";
  public const string KillPlayer = "KillPlayer";
  public const string FreeRoam = "FreeRoam";
  public const string FreeRoamTwirl = "FreeRoamTwirl";
  public const string FreeRoamRemove = "FreeRoamRemove";
  public const string SetInputEvent = "SetInputEvent";
  public const string MoveDecorations = "MoveDecorations";

  // Event kinds, in the order events on one tile are written.
  private static readonly string[] Kinds =
  {
    SetSpeed,
    Twirl,
    Hold,
    MultiPlanet,
    Pause,
    AutoPlayTiles,
    ScaleMargin,
    Multitap,
    KillPlayer,
    FreeRoam,
    FreeRoamTwirl,
    FreeRoamRemove,
    SetInputEvent,
  };

  private static readonly Dictionary<string, string[]> EventProperties = new Dictionary<string, string[]>(
    StringComparer.Ordinal
  )
  {
    // A speed change can start partway through a tile (angleOffset).
    [SetSpeed] = new[] { "speedType", "beatsPerMinute", "bpmMultiplier", "angleOffset" },
    [Twirl] = Array.Empty<string>(),
    [Hold] = new[] { "duration" },
    [MultiPlanet] = new[] { "planets" },
    [Pause] = new[] { "duration" },
    // Safety tiles keep a miss from killing the player.
    [AutoPlayTiles] = new[] { "enabled", "safetyTiles" },
    [ScaleMargin] = new[] { "scale" },
    [Multitap] = new[] { "taps" },
    // A tagged kill waits for its trigger instead of firing at its own time.
    [KillPlayer] = new[] { "angleOffset", "eventTag" },
    // Duration adds beats to the tile; size and offsets shape the free-roam area.
    [FreeRoam] = new[] { "duration", "size", "positionOffset", "outTime" },
    [FreeRoamTwirl] = new[] { "position" },
    [FreeRoamRemove] = new[] { "position", "size" },
    [SetInputEvent] = new[] { "angleOffset", "target", "state", "ignoreInput", "targetEventTag" },
    // Only moves of hitbox decorations are written; they move the hitbox.
    [MoveDecorations] = new[]
    {
      "tag",
      "eventTag",
      "angleOffset",
      "duration",
      "relativeTo",
      "visible",
      "decorationImage",
      "originalSize",
      "cropPivotOffset",
      "parallax",
      "parallaxOffset",
      "positionOffset",
      "pivotOffset",
      "rotationOffset",
      "scale",
      "ease",
    },
  };

  /// <summary>Placement and hitbox properties of a Kill or Event hitbox decoration.</summary>
  public static readonly string[] HitboxDecorationProperties =
  {
    "relativeTo",
    "position",
    "pivotOffset",
    "rotation",
    "scale",
    "parallax",
    "parallaxOffset",
    "lockRotation",
    "lockScale",
    "scaleMultiplier",
    "decorationImage",
    "tag",
    "hitbox",
    "hitboxEventTag",
    "failHitboxType",
    "failHitboxScale",
    "failHitboxOffset",
    "failHitboxRotation",
    "hitboxDetectTarget",
    "hitboxTargetPlanet",
    "hitboxTriggerType",
    "hitboxRepeatInterval",
    "hitboxDecoTag",
  };

  private static readonly HashSet<string> TagProperties = new HashSet<string>(StringComparer.Ordinal)
  {
    "tag",
    "eventTag",
    "targetEventTag",
    "hitboxEventTag",
    "hitboxDecoTag",
  };

  // MoveDecorations only changes what the file sets (LevelEvent.disabled marks the rest).
  private static readonly HashSet<string> AlwaysUsedMoveProperties = new HashSet<string>(StringComparer.Ordinal)
  {
    "tag",
    "eventTag",
    "angleOffset",
    "duration",
  };

  // RDUtils.noneTags: the game treats these as "no tag".
  private static readonly HashSet<string> NoneTags = new HashSet<string>(StringComparer.Ordinal)
  {
    "NONE",
    "无",
    "無",
    "AUCUN",
    "ŽÁDNÝ",
    "NICHTS",
  };

  private static readonly char[] TagSeparator = { ' ' };

  /// <summary>Properties to copy for an event type, or null when V5 ignores the type.</summary>
  public static string[] PropertiesOf(string type) =>
    type != null && EventProperties.TryGetValue(type, out string[] properties) ? properties : null;

  public static byte[] Compute(GameplayChartSnapshot chart)
  {
    if (chart == null)
      throw new ArgumentNullException(nameof(chart));

    using var writer = new GameplayChartHashCanonicalWriter();
    writer.WriteFormatVersion(Version);
    writer.WriteCanonicalFloat(chart.Bpm);
    writer.WriteInt(chart.Offset);
    writer.WriteBool(chart.SeparateCountdownTime);
    writer.WriteInt(chart.CountdownTicks);
    writer.WriteChartKind(chart.LegacyPath);
    if (chart.LegacyPath)
      writer.WriteString(chart.PathData);
    else
      WriteAngles(writer, chart.Angles);

    WriteGameplayEvents(writer, chart.Events);
    WriteHitboxDecorations(writer, chart);
    return writer.ComputeSha256Hash();
  }

  /// <summary>The level's angle for the same direction in [0, 360); 999 marks a midspin.</summary>
  public static float NormalizeAngle(float value)
  {
    if (value == 999f || float.IsNaN(value) || float.IsInfinity(value))
      return value;
    float normalized = value % 360f;
    if (normalized < 0f)
      normalized += 360f;
    if (normalized >= 360f)
      normalized -= 360f;
    return normalized == 0f ? 0f : normalized;
  }

  /// <summary>The tags in a space-separated value, without "no tag" values, sorted.</summary>
  public static string NormalizeTags(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
      return string.Empty;
    string[] parts = value.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 1)
      return NoneTags.Contains(parts[0]) ? string.Empty : parts[0];
    var tags = new SortedSet<string>(StringComparer.Ordinal);
    foreach (string part in parts)
      if (!NoneTags.Contains(part))
        tags.Add(part);
    return string.Join(" ", tags);
  }

  public static void AddTags(string value, HashSet<string> tags)
  {
    if (string.IsNullOrWhiteSpace(value))
      return;
    foreach (string part in value.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries))
      if (!NoneTags.Contains(part))
        tags.Add(part);
  }

  public static bool HasAnyTag(string value, HashSet<string> tags)
  {
    if (tags == null || tags.Count == 0 || string.IsNullOrWhiteSpace(value))
      return false;
    if (value.IndexOf(' ') < 0)
      return tags.Contains(value);
    foreach (string part in value.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries))
      if (tags.Contains(part))
        return true;
    return false;
  }

  private static void WriteAngles(GameplayChartHashCanonicalWriter writer, IReadOnlyList<float> angles)
  {
    int count = angles?.Count ?? 0;
    writer.WriteInt(count);
    for (int index = 0; index < count; index++)
      writer.WriteCanonicalFloat(NormalizeAngle(angles[index]));
  }

  private static void WriteGameplayEvents(GameplayChartHashCanonicalWriter writer, List<GameplayChartItem> events)
  {
    // Kill events a SetInputEvent can trigger: the input event collects tagged
    // effects on its own tile (ffxSetInputEventPlus.PrepVfx).
    Dictionary<int, HashSet<string>> killTags = null;
    foreach (GameplayChartItem item in events)
    {
      if (!item.Active || item.Type != KillPlayer)
        continue;
      killTags ??= new Dictionary<int, HashSet<string>>();
      if (!killTags.TryGetValue(item.Floor, out HashSet<string> tags))
        killTags.Add(item.Floor, tags = new HashSet<string>(StringComparer.Ordinal));
      AddTags(item.Get("eventTag") as string, tags);
    }

    var selected = new List<(GameplayChartItem Item, int Kind, int Index)>();
    for (int index = 0; index < events.Count; index++)
    {
      GameplayChartItem item = events[index];
      if (!item.Active)
        continue;
      int kind = Array.IndexOf(Kinds, item.Type);
      if (kind < 0 || (item.Type == SetInputEvent && !AffectsGameplay(item, killTags)))
        continue;
      selected.Add((item, kind, index));
    }

    // Events on one tile apply together; only same-kind order matters (speed changes).
    List<(GameplayChartItem Item, int Kind, int Index)> ordered = selected
      .OrderBy(entry => entry.Item.Floor)
      .ThenBy(entry => entry.Kind)
      .ThenBy(entry => entry.Index)
      .ToList();
    writer.WriteInt(ordered.Count);
    foreach ((GameplayChartItem item, int kind, _) in ordered)
    {
      writer.WriteInt(item.Floor);
      writer.WriteByte((byte)kind);
      WriteEventValues(writer, item, killTags);
    }
  }

  // An input event matters when it swallows the key press or can trigger a kill.
  private static bool AffectsGameplay(GameplayChartItem item, Dictionary<int, HashSet<string>> killTags) =>
    item.Get("ignoreInput") is bool ignoreInput && ignoreInput || LinkedKillTags(item, killTags).Length > 0;

  private static string LinkedKillTags(GameplayChartItem item, Dictionary<int, HashSet<string>> killTags)
  {
    if (killTags == null || !killTags.TryGetValue(item.Floor, out HashSet<string> tags))
      return string.Empty;
    var linked = new HashSet<string>(StringComparer.Ordinal);
    AddTags(item.Get("targetEventTag") as string, linked);
    linked.IntersectWith(tags);
    return linked.Count == 0 ? string.Empty : string.Join(" ", linked.OrderBy(tag => tag, StringComparer.Ordinal));
  }

  private static void WriteEventValues(
    GameplayChartHashCanonicalWriter writer,
    GameplayChartItem item,
    Dictionary<int, HashSet<string>> killTags
  )
  {
    switch (item.Type)
    {
      case SetSpeed:
        object speedType = item.Get("speedType");
        WriteValue(writer, speedType);
        WriteValue(
          writer,
          item.Get(
            string.Equals(speedType as string, "Bpm", StringComparison.Ordinal) ? "beatsPerMinute" : "bpmMultiplier"
          )
        );
        WriteValue(writer, item.Get("angleOffset"));
        return;
      case SetInputEvent:
        WriteValue(writer, item.Get("angleOffset"));
        WriteValue(writer, item.Get("target"));
        WriteValue(writer, item.Get("state"));
        WriteValue(writer, item.Get("ignoreInput"));
        writer.WriteString(LinkedKillTags(item, killTags));
        return;
      default:
        WriteProperties(writer, item, EventProperties[item.Type], absentMatters: false);
        return;
    }
  }

  private static void WriteHitboxDecorations(GameplayChartHashCanonicalWriter writer, GameplayChartSnapshot chart)
  {
    var tags = new HashSet<string>(StringComparer.Ordinal);
    var decorations = new List<GameplayChartItem>();
    foreach (GameplayChartItem decoration in chart.HitboxDecorations)
    {
      if (!decoration.Active)
        continue;
      decorations.Add(decoration);
      AddTags(decoration.Get("tag") as string, tags);
    }

    writer.WriteInt(decorations.Count);
    foreach (GameplayChartItem decoration in decorations)
    {
      writer.WriteInt(decoration.Floor);
      WriteProperties(writer, decoration, HitboxDecorationProperties, absentMatters: false);
    }

    var moves = new List<(GameplayChartItem Item, int Index)>();
    for (int index = 0; index < chart.Events.Count; index++)
    {
      GameplayChartItem item = chart.Events[index];
      if (item.Active && item.Type == MoveDecorations && HasAnyTag(item.Get("tag") as string, tags))
        moves.Add((item, index));
    }
    writer.WriteInt(moves.Count);
    foreach ((GameplayChartItem item, _) in moves.OrderBy(entry => entry.Item.Floor).ThenBy(entry => entry.Index))
    {
      writer.WriteInt(item.Floor);
      WriteProperties(writer, item, EventProperties[MoveDecorations], absentMatters: true);
    }
  }

  private static void WriteProperties(
    GameplayChartHashCanonicalWriter writer,
    GameplayChartItem item,
    string[] properties,
    bool absentMatters
  )
  {
    foreach (string property in properties)
    {
      if (absentMatters && !AlwaysUsedMoveProperties.Contains(property) && item.IsAbsent(property))
      {
        writer.WriteByte(0);
        continue;
      }
      if (absentMatters)
        writer.WriteByte(1);
      object value = item.Get(property);
      if (TagProperties.Contains(property))
        writer.WriteString(NormalizeTags(value as string));
      else
        WriteValue(writer, value);
    }
  }

  private static void WriteValue(GameplayChartHashCanonicalWriter writer, object value)
  {
    switch (value)
    {
      case null:
        writer.WriteByte(0);
        return;
      case bool flag:
        writer.WriteByte(1);
        writer.WriteBool(flag);
        return;
      case int number:
        writer.WriteByte(2);
        writer.WriteInt(number);
        return;
      case float number:
        writer.WriteByte(3);
        writer.WriteCanonicalFloat(number);
        return;
      case double number:
        writer.WriteByte(3);
        writer.WriteCanonicalFloat((float)number);
        return;
      case string text:
        writer.WriteByte(4);
        writer.WriteString(text);
        return;
      case float[] vector:
        writer.WriteByte(5);
        writer.WriteInt(vector.Length);
        foreach (float component in vector)
          writer.WriteCanonicalFloat(component);
        return;
      default:
        writer.WriteByte(4);
        writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture));
        return;
    }
  }
}
