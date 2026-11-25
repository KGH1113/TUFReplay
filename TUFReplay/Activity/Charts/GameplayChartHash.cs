using System;
using System.Collections.Generic;
using ADOFAI;
using GDMiniJSON;

namespace TUFReplay.Activity.Charts;

public static class GameplayChartHash
{
  /// <summary>Version of the hash in the gameplay_hash column (V4).</summary>
  public const int Version = 4;
  public const int HashSize = 32;
  public const int V5Version = GameplayChartV5Hasher.Version;

  public static bool TryComputeCurrent(out GameplayChartHashes hashes, out string error)
  {
    LevelData levelData = ADOBase.editor?.levelData ?? ADOBase.customLevel?.levelData;
    return TryCompute(levelData, out hashes, out error);
  }

  public static bool TryComputeCurrentV5(out byte[] hash, out string error)
  {
    LevelData levelData = ADOBase.editor?.levelData ?? ADOBase.customLevel?.levelData;
    return TryComputeV5(levelData, out hash, out error);
  }

  public static bool TryLoadCustomLevel(
    string levelPath,
    out LevelData levelData,
    out GameplayChartHashes hashes,
    out string error
  )
  {
    return TryLoadCustomLevel(levelPath, out _, out levelData, out hashes, out error);
  }

  public static bool TryLoadCustomLevel(
    string levelPath,
    out string source,
    out LevelData levelData,
    out GameplayChartHashes hashes,
    out string error
  )
  {
    source = null;
    levelData = null;
    hashes = null;
    error = null;

    string canonicalPath = LevelPathIdentity.Canonicalize(levelPath);
    if (canonicalPath == null)
    {
      error = "Level file is unavailable.";
      return false;
    }

    try
    {
      source = RDFile.ReadAllText(canonicalPath);
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
        string migrated = source
          .Replace("\"enabled\": true", "\"active\": true")
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

      if (!TryCompute(loaded, out hashes, out error))
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

  /// <summary>Computes the V4 and V5 hashes of one chart.</summary>
  public static bool TryCompute(LevelData levelData, out GameplayChartHashes hashes, out string error)
  {
    hashes = null;
    if (!TryComputeV4(levelData, out byte[] v4, out error) || !TryComputeV5(levelData, out byte[] v5, out error))
      return false;
    hashes = new GameplayChartHashes(v4, v5);
    return true;
  }

  public static bool TryComputeV5(LevelData levelData, out byte[] hash, out string error)
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
      hash = GameplayChartV5Hasher.Compute(GameplayChartSnapshotReader.Read(levelData));
      return true;
    }
    catch (Exception exception)
    {
      error = "Level hash failed: " + exception.GetType().Name;
      return false;
    }
  }

  // V4 stays byte-for-byte as shipped: runs recorded before V5 only carry this hash.
  private static bool TryComputeV4(LevelData levelData, out byte[] hash, out string error)
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
      writer.WriteFormatVersion(Version);
      writer.WriteGameplaySettingsV4(
        levelData.songFilename,
        levelData.bpm,
        levelData.volume,
        levelData.offset,
        levelData.separateCountdownTime,
        levelData.countdownTicks,
        Convert.ToSingle(levelData.levelSettings["speedTrialAim"]),
        levelData.levelSettings.GetBool("legacySpriteTiles")
      );
      writer.WriteChartKind(levelData.isOldLevel);

      if (levelData.isOldLevel)
        writer.WriteLegacyPath(levelData.pathData);
      else
        writer.WriteAngles(levelData.angleData);

      foreach (LevelEvent levelEvent in levelData.levelEvents)
        WriteGameplayEvent(writer, levelEvent);

      hash = writer.ComputeSha256Hash();
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
    return version == Version && hash?.Length == HashSize;
  }

  public static bool IsV5(byte[] hash) => hash?.Length == HashSize;

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
