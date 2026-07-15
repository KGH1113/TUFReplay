using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ADOFAI;

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

  public static bool TryLoad(string levelPath, out LevelData levelData, out byte[] hash, out string error)
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
      var loaded = new LevelData();
      if (!loaded.LoadLevel(canonicalPath, out LoadResult status))
      {
        error = "ADOFAI could not parse the level file. status=" + status;
        return false;
      }

      if (!TryCompute(loaded, out hash, out error))
        return false;

      levelData = loaded;
      return true;
    }
    catch (Exception exception)
    {
      error = "Level hash failed: " + exception.GetType().Name;
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
      using var payload = new MemoryStream();
      if (levelData.isOldLevel)
      {
        WriteUtf(payload, levelData.pathData ?? string.Empty);
      }
      else
      {
        int count = levelData.angleData?.Count ?? 0;
        WriteInt(payload, count);
        for (int index = 0; index < count; index++)
          WriteFloat(payload, levelData.angleData[index]);
      }

      foreach (LevelEvent levelEvent in levelData.levelEvents)
        WriteGameplayEvent(payload, levelEvent);

      payload.Position = 0;
      using MD5 md5 = MD5.Create();
      hash = md5.ComputeHash(payload);
      return true;
    }
    catch (Exception exception)
    {
      error = "Level hash failed: " + exception.GetType().Name;
      return false;
    }
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

  private static void WriteGameplayEvent(Stream stream, LevelEvent levelEvent)
  {
    switch (levelEvent.eventType)
    {
      case LevelEventType.SetSpeed:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(0);
        var speedType = (SpeedType)levelEvent["speedType"];
        stream.WriteByte((byte)speedType);
        WriteFloat(stream, (float)levelEvent[speedType == SpeedType.Bpm ? "beatsPerMinute" : "bpmMultiplier"]);
        break;

      case LevelEventType.Twirl:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(1);
        break;

      case LevelEventType.Hold:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(2);
        WriteInt(stream, (int)levelEvent["duration"]);
        break;

      case LevelEventType.MultiPlanet:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(3);
        stream.WriteByte((byte)(PlanetCount)levelEvent["planets"]);
        break;

      case LevelEventType.Pause:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(4);
        WriteFloat(stream, (float)levelEvent["duration"]);
        break;

      case LevelEventType.AutoPlayTiles:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(5);
        stream.WriteByte((bool)levelEvent["enabled"] ? (byte)1 : (byte)0);
        break;

      case LevelEventType.ScaleMargin:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(6);
        WriteFloat(stream, (float)levelEvent["scale"]);
        break;

      case LevelEventType.Multitap:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(7);
        WriteFloat(stream, (float)levelEvent["taps"]);
        break;

      case LevelEventType.KillPlayer:
        WriteInt(stream, levelEvent.floor);
        stream.WriteByte(8);
        break;
    }
  }

  private static void WriteUtf(Stream stream, string value)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(value);
    WriteInt(stream, bytes.Length);
    stream.Write(bytes, 0, bytes.Length);
  }

  private static void WriteInt(Stream stream, int value)
  {
    stream.WriteByte((byte)(value >> 24));
    stream.WriteByte((byte)(value >> 16));
    stream.WriteByte((byte)(value >> 8));
    stream.WriteByte((byte)value);
  }

  private static void WriteFloat(Stream stream, float value)
  {
    byte[] bytes = BitConverter.GetBytes(value);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    stream.Write(bytes, 0, bytes.Length);
  }
}
