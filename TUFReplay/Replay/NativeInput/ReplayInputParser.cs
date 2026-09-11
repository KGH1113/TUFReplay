using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.NativeInput;

public static class ReplayInputParser
{
  public static List<RecordedInput> Parse(byte[] inputCsv)
  {
    return Parse(inputCsv, out _);
  }

  public static List<RecordedInput> Parse(byte[] inputCsv, out long maxTimeUs)
  {
    maxTimeUs = 0;
    if (inputCsv == null || inputCsv.Length == 0)
      return new List<RecordedInput>();

    ReadOnlySpan<byte> payload = inputCsv;
    List<RecordedInput> events = new List<RecordedInput>(Utf8Csv.CountNonEmptyLines(payload));
    long previousTimeUs = 0;
    int offset = 0;

    while (Utf8Csv.TryReadNonEmptyLine(payload, ref offset, out ReadOnlySpan<byte> line))
    {
      if (!TryParseLine(line, out RecordedInput input))
        throw new InvalidDataException("Replay input payload contains a malformed row.");

      if (events.Count > 0 && input.TimeUs < previousTimeUs)
        throw new InvalidDataException("Replay input timestamps must be monotonic.");
      previousTimeUs = input.TimeUs;
      if (input.TimeUs > maxTimeUs)
        maxTimeUs = input.TimeUs;
      events.Add(input);
    }

    return events;
  }

  public static bool TryParseLine(string line, out RecordedInput input)
  {
    input = default;

    if (line == null)
      return false;
    ReadOnlySpan<char> value = line.AsSpan();
    int fieldCount = CountFields(value);
    if (fieldCount != 5)
      return false;
    Span<Range> parts = stackalloc Range[fieldCount];
    if (!Utf8Csv.TrySplit(value, parts))
      return false;

    if (!long.TryParse(value[parts[0]], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeUs))
      return false;

    if (!int.TryParse(value[parts[1]], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key))
      return false;

    if (!ushort.TryParse(value[parts[2]], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort rawFlags))
      return false;

    if (
      !int.TryParse(value[parts[3]], NumberStyles.Integer, CultureInfo.InvariantCulture, out int nativeCode)
      || !ulong.TryParse(value[parts[4]], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong nativeFlags)
    )
      return false;

    input = new RecordedInput(timeUs, key, (RecordInputFlags)rawFlags, nativeCode, nativeFlags);
    return true;
  }

  private static bool TryParseLine(ReadOnlySpan<byte> line, out RecordedInput input)
  {
    input = default;
    int fieldCount = CountFields(line);
    if (fieldCount != 5)
      return false;
    Span<Range> parts = stackalloc Range[fieldCount];
    if (!Utf8Csv.TrySplit(line, parts))
      return false;
    if (!Utf8Csv.TryParseInt64(line[parts[0]], out long timeUs))
      return false;
    if (!Utf8Csv.TryParseInt32(line[parts[1]], out int key))
      return false;
    if (!Utf8Csv.TryParseUInt16(line[parts[2]], out ushort rawFlags))
      return false;

    if (
      !Utf8Csv.TryParseInt32(line[parts[3]], out int nativeCode)
      || !TryParseUInt64(line[parts[4]], out ulong nativeFlags)
    )
      return false;

    input = new RecordedInput(timeUs, key, (RecordInputFlags)rawFlags, nativeCode, nativeFlags);
    return true;
  }

  private static int CountFields(ReadOnlySpan<char> value)
  {
    int count = 1;
    for (int i = 0; i < value.Length; i++)
    {
      if (value[i] == ',')
        count++;
    }
    return count;
  }

  private static int CountFields(ReadOnlySpan<byte> value)
  {
    int count = 1;
    for (int i = 0; i < value.Length; i++)
    {
      if (value[i] == (byte)',')
        count++;
    }
    return count;
  }

  private static bool TryParseUInt64(ReadOnlySpan<byte> value, out ulong result)
  {
    result = 0;
    if (value.Length == 0)
      return false;
    for (int i = 0; i < value.Length; i++)
    {
      byte digit = value[i];
      if (digit < (byte)'0' || digit > (byte)'9')
        return false;
      ulong previous = result;
      result = result * 10UL + (ulong)(digit - (byte)'0');
      if (result < previous)
        return false;
    }
    return true;
  }
}
