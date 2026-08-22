using System;
using System.Collections.Generic;
using System.Globalization;
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
    bool requiresSort = false;
    long previousTimeUs = 0;
    int offset = 0;

    while (Utf8Csv.TryReadNonEmptyLine(payload, ref offset, out ReadOnlySpan<byte> line))
    {
      if (!TryParseLine(line, out RecordedInput input))
        continue;

      if (events.Count > 0 && input.TimeUs < previousTimeUs)
        requiresSort = true;
      previousTimeUs = input.TimeUs;
      if (input.TimeUs > maxTimeUs)
        maxTimeUs = input.TimeUs;
      events.Add(input);
    }

    if (requiresSort)
      SortLegacyPayload(events);

    return events;
  }

  private static void SortLegacyPayload(List<RecordedInput> events)
  {
    List<ParsedInput> parsed = new List<ParsedInput>(events.Count);
    for (int i = 0; i < events.Count; i++)
      parsed.Add(new ParsedInput(events[i], i));

    parsed.Sort(
      (a, b) =>
      {
        int timeOrder = a.Input.TimeUs.CompareTo(b.Input.TimeUs);
        return timeOrder != 0 ? timeOrder : a.Sequence.CompareTo(b.Sequence);
      }
    );

    for (int i = 0; i < parsed.Count; i++)
      events[i] = parsed[i].Input;
  }

  private readonly struct ParsedInput
  {
    public readonly RecordedInput Input;
    public readonly int Sequence;

    public ParsedInput(RecordedInput input, int sequence)
    {
      Input = input;
      Sequence = sequence;
    }
  }

  public static bool TryParseLine(string line, out RecordedInput input)
  {
    input = default;

    if (line == null)
      return false;
    ReadOnlySpan<char> value = line.AsSpan();
    Span<Range> parts = stackalloc Range[3];
    if (!Utf8Csv.TrySplit(value, parts))
      return false;

    if (!long.TryParse(value[parts[0]], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeUs))
      return false;

    if (!int.TryParse(value[parts[1]], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key))
      return false;

    if (!ushort.TryParse(value[parts[2]], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort rawFlags))
      return false;

    input = new RecordedInput(timeUs, key, (RecordInputFlags)rawFlags);
    return true;
  }

  private static bool TryParseLine(ReadOnlySpan<byte> line, out RecordedInput input)
  {
    input = default;
    Span<Range> parts = stackalloc Range[3];
    if (!Utf8Csv.TrySplit(line, parts))
      return false;
    if (!Utf8Csv.TryParseInt64(line[parts[0]], out long timeUs))
      return false;
    if (!Utf8Csv.TryParseInt32(line[parts[1]], out int key))
      return false;
    if (!Utf8Csv.TryParseUInt16(line[parts[2]], out ushort rawFlags))
      return false;

    input = new RecordedInput(timeUs, key, (RecordInputFlags)rawFlags);
    return true;
  }
}
