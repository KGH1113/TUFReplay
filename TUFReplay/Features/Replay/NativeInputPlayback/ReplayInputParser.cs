using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Features.Replay;

public static class ReplayInputParser
{
  public static List<RecordedInput> Parse(byte[] inputCsv)
  {
    List<ParsedInput> parsed = new List<ParsedInput>();
    if (inputCsv == null || inputCsv.Length == 0)
      return new List<RecordedInput>();

    string text = Encoding.UTF8.GetString(inputCsv);
    string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    for (int sequence = 0; sequence < lines.Length; sequence++)
    {
      if (!TryParseLine(lines[sequence], out RecordedInput input))
        continue;
      parsed.Add(new ParsedInput(input, sequence));
    }

    parsed.Sort(
      (a, b) =>
      {
        int timeOrder = a.Input.TimeUs.CompareTo(b.Input.TimeUs);
        return timeOrder != 0 ? timeOrder : a.Sequence.CompareTo(b.Sequence);
      }
    );

    List<RecordedInput> events = new List<RecordedInput>(parsed.Count);
    for (int i = 0; i < parsed.Count; i++)
      events.Add(parsed[i].Input);
    return events;
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

    string[] parts = line.Split(',');
    if (parts.Length != 3)
      return false;

    if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeUs))
      return false;

    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key))
      return false;

    if (!ushort.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort rawFlags))
      return false;

    input = new RecordedInput(timeUs, key, (RecordInputFlags)rawFlags);
    return true;
  }
}
