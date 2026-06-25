using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TUFReplay.Recording;

namespace TUFReplay.Replay;

public static class ReplayInputParser
{
  public static List<ReplayInputEvent> Parse(byte[] inputCsv)
  {
    List<ReplayInputEvent> events = new List<ReplayInputEvent>();
    if (inputCsv == null || inputCsv.Length == 0) return events;

    string text = Encoding.UTF8.GetString(inputCsv);
    string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    foreach (string line in lines)
    {
      if (!TryParseLine(line, out ReplayInputEvent input)) continue;
      events.Add(input);
    }

    events.Sort((a, b) => a.TimeUs.CompareTo(b.TimeUs));
    return events;
  }

  public static bool TryParseLine(string line, out ReplayInputEvent input)
  {
    input = default;

    string[] parts = line.Split(',');
    if (parts.Length != 3) return false;

    if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timeUs))
      return false;

    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key))
      return false;

    if (!ushort.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort rawFlags))
      return false;

    input = new ReplayInputEvent(timeUs, key, (RecordInputFlags)rawFlags);
    return true;
  }
}
