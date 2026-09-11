using System;
using System.Collections.Generic;
using System.IO;

namespace TUFReplay.Replay.Playback;

public static class ReplayHitContextParser
{
  public static List<ReplayHitContext> Parse(byte[] hitContextCsv)
  {
    if (hitContextCsv == null || hitContextCsv.Length == 0)
      return new List<ReplayHitContext>();

    ReadOnlySpan<byte> payload = hitContextCsv;
    List<ReplayHitContext> contexts = new List<ReplayHitContext>(Utf8Csv.CountNonEmptyLines(payload));
    int offset = 0;

    while (Utf8Csv.TryReadNonEmptyLine(payload, ref offset, out ReadOnlySpan<byte> line))
    {
      if (!TryParseLine(line, out ReplayHitContext context))
        throw new InvalidDataException("Replay hit payload contains a malformed row.");
      contexts.Add(context);
    }

    return contexts;
  }

  private static bool TryParseLine(ReadOnlySpan<byte> line, out ReplayHitContext context)
  {
    context = default;

    Span<Range> parts = stackalloc Range[13];
    if (!Utf8Csv.TrySplit(line, parts))
      return false;

    if (!Utf8Csv.TryParseInt32(line[parts[0]], out int currentFloorID))
      return false;
    if (!Utf8Csv.TryParseDouble(line[parts[1]], out double currAngle))
      return false;
    if (!Utf8Csv.TryParseSingle(line[parts[2]], out float overloadCounter))
      return false;
    if (!Utf8Csv.TryParseBoolean(line[parts[3]], out bool noFailHit))
      return false;
    if (!Utf8Csv.TryParseBoolean(line[parts[4]], out bool isAuto))
      return false;
    if (!Utf8Csv.TryParseBoolean(line[parts[5]], out bool nextFloorAuto))
      return false;
    if (!Utf8Csv.TryParseDouble(line[parts[6]], out double cachedAngle))
      return false;
    if (!Utf8Csv.TryParseDouble(line[parts[7]], out double targetExitAngle))
      return false;
    if (!Utf8Csv.TryParseBoolean(line[parts[8]], out bool midspinInfiniteMargin))
      return false;
    if (!Utf8Csv.TryParseBoolean(line[parts[9]], out bool rdcAuto))
      return false;
    if (!Utf8Csv.TryParseInt32(line[parts[10]], out int curFreeRoamSection))
      return false;

    if (!Utf8Csv.TryParseInt32(line[parts[11]], out int resolvedHitMargin))
      return false;
    if (!Utf8Csv.TryParseInt64(line[parts[12]], out long timeUs) || timeUs < 0L)
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
      curFreeRoamSection,
      resolvedHitMargin,
      timeUs
    );
    return true;
  }
}
