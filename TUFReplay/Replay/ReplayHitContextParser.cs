using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TUFReplay.Replay;

public static class ReplayHitContextParser
{
  public static List<ReplayHitContext> Parse(byte[] hitContextCsv)
  {
    List<ReplayHitContext> contexts = new List<ReplayHitContext>();
    if (hitContextCsv == null || hitContextCsv.Length == 0) return contexts;

    string text = Encoding.UTF8.GetString(hitContextCsv);
    string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

    foreach (string line in lines)
    {
      if (TryParseLine(line, out ReplayHitContext context))
      {
        contexts.Add(context);
      }
    }

    return contexts;
  }

  private static bool TryParseLine(string line, out ReplayHitContext context)
  {
    context = default;

    string[] parts = line.Split(',');
    if (parts.Length != 11) return false;

    if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentFloorID))
      return false;
    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double currAngle))
      return false;
    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float overloadCounter))
      return false;
    if (!TryParseBool(parts[3], out bool noFailHit))
      return false;
    if (!TryParseBool(parts[4], out bool isAuto))
      return false;
    if (!TryParseBool(parts[5], out bool nextFloorAuto))
      return false;
    if (!double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double cachedAngle))
      return false;
    if (!double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double targetExitAngle))
      return false;
    if (!TryParseBool(parts[8], out bool midspinInfiniteMargin))
      return false;
    if (!TryParseBool(parts[9], out bool rdcAuto))
      return false;
    if (!int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int curFreeRoamSection))
      return false;

    context = new ReplayHitContext(
      currentFloorID,
      currAngle,
      overloadCounter,
      noFailHit,
      isAuto,
      nextFloorAuto,
      cachedAngle,
      targetExitAngle,
      midspinInfiniteMargin,
      rdcAuto,
      curFreeRoamSection
    );
    return true;
  }

  private static bool TryParseBool(string value, out bool result)
  {
    if (value == "1")
    {
      result = true;
      return true;
    }

    if (value == "0")
    {
      result = false;
      return true;
    }

    return bool.TryParse(value, out result);
  }
}
