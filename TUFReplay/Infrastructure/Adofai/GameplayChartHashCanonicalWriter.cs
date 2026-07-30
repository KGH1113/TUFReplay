using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TUFReplay.Infrastructure.Adofai;

internal sealed class GameplayChartHashCanonicalWriter : IDisposable
{
  private readonly MemoryStream _payload = new MemoryStream();

  public void WriteFormatVersion(int version) => WriteInt(version);

  public void WriteChartKind(bool legacy) => _payload.WriteByte(legacy ? (byte)0 : (byte)1);

  public void WriteGameplaySettingsV2(
    int levelVersion,
    string songFilename,
    float bpm,
    int volume,
    int offset,
    int pitch,
    byte hitsound,
    int hitsoundVolume,
    bool separateCountdownTime,
    int countdownTicks,
    float speedTrialAim,
    bool legacySpriteTiles
  )
  {
    WriteInt(levelVersion);
    WriteString(songFilename);
    WriteFloat(bpm);
    WriteInt(volume);
    WriteInt(offset);
    WriteInt(pitch);
    _payload.WriteByte(hitsound);
    WriteInt(hitsoundVolume);
    _payload.WriteByte(separateCountdownTime ? (byte)1 : (byte)0);
    WriteInt(countdownTicks);
    WriteFloat(speedTrialAim);
    _payload.WriteByte(legacySpriteTiles ? (byte)1 : (byte)0);
  }

  public void WriteGameplaySettingsV3(
    int levelVersion,
    string songFilename,
    float bpm,
    int volume,
    int offset,
    bool separateCountdownTime,
    int countdownTicks,
    float speedTrialAim,
    bool legacySpriteTiles
  )
  {
    WriteInt(levelVersion);
    WriteString(songFilename);
    WriteFloat(bpm);
    WriteInt(volume);
    WriteInt(offset);
    _payload.WriteByte(separateCountdownTime ? (byte)1 : (byte)0);
    WriteInt(countdownTicks);
    WriteFloat(speedTrialAim);
    _payload.WriteByte(legacySpriteTiles ? (byte)1 : (byte)0);
  }

  public void WriteLegacyPath(string pathData)
  {
    WriteString(pathData);
  }

  public void WriteAngles(IReadOnlyList<float> angleData)
  {
    int count = angleData?.Count ?? 0;
    WriteInt(count);
    for (int index = 0; index < count; index++)
      WriteFloat(angleData[index]);
  }

  public void WriteNormalizedAngles(IReadOnlyList<float> angleData)
  {
    int count = angleData?.Count ?? 0;
    WriteInt(count);
    for (int index = 0; index < count; index++)
      WriteFloat(NormalizeAngle(angleData[index]));
  }

  public void WriteSetSpeed(int floor, byte speedType, float value)
  {
    WriteEventHeader(floor, 0);
    _payload.WriteByte(speedType);
    WriteFloat(value);
  }

  public void WriteTwirl(int floor) => WriteEventHeader(floor, 1);

  public void WriteHold(int floor, int duration)
  {
    WriteEventHeader(floor, 2);
    WriteInt(duration);
  }

  public void WriteMultiPlanet(int floor, byte planets)
  {
    WriteEventHeader(floor, 3);
    _payload.WriteByte(planets);
  }

  public void WritePause(int floor, float duration)
  {
    WriteEventHeader(floor, 4);
    WriteFloat(duration);
  }

  public void WriteAutoPlayTiles(int floor, bool enabled)
  {
    WriteEventHeader(floor, 5);
    _payload.WriteByte(enabled ? (byte)1 : (byte)0);
  }

  public void WriteScaleMargin(int floor, float scale)
  {
    WriteEventHeader(floor, 6);
    WriteFloat(scale);
  }

  public void WriteMultitap(int floor, float taps)
  {
    WriteEventHeader(floor, 7);
    WriteFloat(taps);
  }

  public void WriteKillPlayer(int floor) => WriteEventHeader(floor, 8);

  public byte[] ComputeMd5Hash()
  {
    _payload.Position = 0;
    using MD5 md5 = MD5.Create();
    return md5.ComputeHash(_payload);
  }

  public byte[] ComputeSha256Hash()
  {
    _payload.Position = 0;
    using SHA256 sha256 = SHA256.Create();
    return sha256.ComputeHash(_payload);
  }

  public void Dispose() => _payload.Dispose();

  private void WriteEventHeader(int floor, byte kind)
  {
    WriteInt(floor);
    _payload.WriteByte(kind);
  }

  private void WriteInt(int value)
  {
    _payload.WriteByte((byte)(value >> 24));
    _payload.WriteByte((byte)(value >> 16));
    _payload.WriteByte((byte)(value >> 8));
    _payload.WriteByte((byte)value);
  }

  private void WriteString(string value)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
    WriteInt(bytes.Length);
    _payload.Write(bytes, 0, bytes.Length);
  }

  private void WriteFloat(float value)
  {
    byte[] bytes = BitConverter.GetBytes(value);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    _payload.Write(bytes, 0, bytes.Length);
  }

  private static float NormalizeAngle(float value)
  {
    if (value == 999f)
      return value;
    float normalized = value % 360f;
    if (normalized < 0f)
      normalized += 360f;
    return normalized == 0f ? 0f : normalized;
  }
}
