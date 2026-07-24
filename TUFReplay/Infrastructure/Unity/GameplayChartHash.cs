using System;
using System.Collections.Generic;
using ADOFAI;
using GDMiniJSON;
using TUFReplay.Infrastructure.Adofai;

namespace TUFReplay.Infrastructure.Unity;

public static class GameplayChartHash
{
  public const int Version = 1;
  public const int Size = 16;

  public static bool TryComputeCurrent(out byte[] hash, out string error)
  {
    LevelData levelData = ADOBase.editor?.levelData ?? ADOBase.customLevel?.levelData;
    return TryCompute(levelData, out hash, out error);
  }

  public static bool TryLoadCustomLevel(string levelPath, out LevelData levelData, out byte[] hash, out string error)
  {
    levelData = null;
    hash = null;
    error = null;

    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath);
    if (canonicalPath == null)
    {
      error = "Level file is unavailable.";
      return false;
    }

    try
    {
      string source = RDFile.ReadAllText(canonicalPath);
      if (!(Json.Deserialize(source) is Dictionary<string, object> decoded))
      {
        error = "ADOFAI could not parse the level JSON.";
        return false;
      }

      var loaded = new LevelData();
      PrepareCustomAngleData(loaded, decoded);
      loaded.Decode(decoded, out LoadResult status);
      if (status != LoadResult.Successful)
      {
        error = "ADOFAI could not parse the level file. status=" + status;
        return false;
      }

      // LevelData.LoadLevel performs this compatibility pass for v7 files after
      // the initial decode. Keep the custom-file path independent from the
      // current scene while preserving the game's own migration behavior.
      if (loaded.version == 7)
      {
        string migrated = source.Replace("\"enabled\": true", "\"active\": true")
          .Replace("\"enabled\": false", "\"active\": false");
        if (!(Json.Deserialize(migrated) is Dictionary<string, object> migratedDecoded))
        {
          error = "ADOFAI could not migrate the version 7 level JSON.";
          return false;
        }

        PrepareCustomAngleData(loaded, migratedDecoded);
        loaded.Decode(migratedDecoded, out status);
        if (status != LoadResult.Successful)
        {
          error = "ADOFAI could not migrate the level file. status=" + status;
          return false;
        }
      }

      if (!TryCompute(loaded, out hash, out error))
        return false;

      levelData = loaded;
      return true;
    }
    catch (Exception exception)
    {
      error = "ADOFAI level verification failed: " + exception.GetType().Name + ": " + exception.Message;
      return false;
    }
  }

  public static bool TryCompute(LevelData levelData, out byte[] hash, out string error)
  {
    hash = null;
    error = null;
    if (levelData == null)
    {
      error = "Level data is unavailable.";
      return false;
    }

    try
    {
      using var writer = new GameplayChartHashCanonicalWriter();
      if (levelData.isOldLevel)
        writer.WriteLegacyPath(levelData.pathData);
      else
        writer.WriteAngles(levelData.angleData);

      foreach (LevelEvent levelEvent in levelData.levelEvents)
        WriteGameplayEvent(writer, levelEvent);

      hash = writer.ComputeHash();
      return true;
    }
    catch (Exception exception)
    {
      error = "Level hash failed: " + exception.GetType().Name;
      return false;
    }
  }

  private static void PrepareCustomAngleData(LevelData levelData, Dictionary<string, object> decoded)
  {
    if (decoded.ContainsKey("pathData"))
      return;

    // LevelData.Decode normally reads angleData only while a non-legacy scnGame
    // instance exists. Replay verification can begin from menus where there is no
    // scnGame yet, so seed the same game-decoded values before calling Decode.
    levelData.angleData = new List<float>(RDEditorUtils.DecodeFloatArray(decoded["angleData"]));
    levelData.pathData = "";
    levelData.isOldLevel = false;
  }

  public static bool Equals(byte[] left, byte[] right)
  {
    if (ReferenceEquals(left, right))
      return true;
    if (left == null || right == null || left.Length != right.Length)
      return false;

    int difference = 0;
    for (int index = 0; index < left.Length; index++)
      difference |= left[index] ^ right[index];
    return difference == 0;
  }

  public static bool IsSupported(int? version, byte[] hash)
  {
    return version == Version && hash?.Length == Size;
  }

  private static void WriteGameplayEvent(GameplayChartHashCanonicalWriter writer, LevelEvent levelEvent)
  {
    switch (levelEvent.eventType)
    {
      case LevelEventType.SetSpeed:
        var speedType = (SpeedType)levelEvent["speedType"];
        writer.WriteSetSpeed(
          levelEvent.floor,
          (byte)speedType,
          (float)levelEvent[speedType == SpeedType.Bpm ? "beatsPerMinute" : "bpmMultiplier"]
        );
        break;

      case LevelEventType.Twirl:
        writer.WriteTwirl(levelEvent.floor);
        break;

      case LevelEventType.Hold:
        writer.WriteHold(levelEvent.floor, (int)levelEvent["duration"]);
        break;

      case LevelEventType.MultiPlanet:
        writer.WriteMultiPlanet(levelEvent.floor, (byte)(PlanetCount)levelEvent["planets"]);
        break;

      case LevelEventType.Pause:
        writer.WritePause(levelEvent.floor, (float)levelEvent["duration"]);
        break;

      case LevelEventType.AutoPlayTiles:
        writer.WriteAutoPlayTiles(levelEvent.floor, (bool)levelEvent["enabled"]);
        break;

      case LevelEventType.ScaleMargin:
        writer.WriteScaleMargin(levelEvent.floor, (float)levelEvent["scale"]);
        break;

      case LevelEventType.Multitap:
        writer.WriteMultitap(levelEvent.floor, Convert.ToSingle(levelEvent["taps"]));
        break;

      case LevelEventType.KillPlayer:
        writer.WriteKillPlayer(levelEvent.floor);
        break;
    }
  }
}
