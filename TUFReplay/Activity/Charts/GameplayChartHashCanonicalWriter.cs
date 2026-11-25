using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TUFReplay.Activity.Charts;

internal sealed class GameplayChartHashCanonicalWriter : IDisposable
{
  private readonly MemoryStream _payload = new MemoryStream();

  public void WriteFormatVersion(int version) => WriteInt(version);

  public void WriteChartKind(bool legacy) => _payload.WriteByte(legacy ? (byte)0 : (byte)1);

  public void WriteGameplaySettingsV4(
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

  public byte[] ComputeSha256Hash()
  {
    _payload.Position = 0;
    using SHA256 sha256 = SHA256.Create();
    return sha256.ComputeHash(_payload);
  }

  public void Dispose() => _payload.Dispose();

  public void WriteByte(byte value) => _payload.WriteByte(value);

  public void WriteBool(bool value) => _payload.WriteByte(value ? (byte)1 : (byte)0);

  public void WriteInt(int value)
  {
    _payload.WriteByte((byte)(value >> 24));
    _payload.WriteByte((byte)(value >> 16));
    _payload.WriteByte((byte)(value >> 8));
    _payload.WriteByte((byte)value);
  }

  public void WriteString(string value)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
    WriteInt(bytes.Length);
    _payload.Write(bytes, 0, bytes.Length);
  }

  /// <summary>Writes the float with -0 folded into 0 and every NaN into one value.</summary>
  public void WriteCanonicalFloat(float value)
  {
    if (float.IsNaN(value))
      value = float.NaN;
    else if (value == 0f)
      value = 0f;
    WriteFloat(value);
  }

  private void WriteEventHeader(int floor, byte kind)
  {
    WriteInt(floor);
    _payload.WriteByte(kind);
  }

  private void WriteFloat(float value)
  {
    WriteInt(BitConverter.SingleToInt32Bits(value));
  }
}
