using System;
using System.Collections.Generic;

namespace TUFReplay.Features.Replay;

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
      if (TryParseLine(line, out ReplayHitContext context))
      {
        contexts.Add(context);
      }
    }

    return contexts;
  }

  private static bool TryParseLine(ReadOnlySpan<byte> line, out ReplayHitContext context)
  {
    context = default;

    Span<Range> timedParts = stackalloc Range[13];
    Span<Range> resolvedParts = stackalloc Range[12];
    Span<Range> legacyParts = stackalloc Range[11];
    bool hasTimeUs = Utf8Csv.TrySplit(line, timedParts);
    bool hasResolvedHitMargin = hasTimeUs || Utf8Csv.TrySplit(line, resolvedParts);
    Span<Range> parts = hasTimeUs ? timedParts : hasResolvedHitMargin ? resolvedParts : legacyParts;
    if (!hasResolvedHitMargin && !Utf8Csv.TrySplit(line, legacyParts))
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

    int? resolvedHitMargin = null;
    if (hasResolvedHitMargin)
    {
      if (!Utf8Csv.TryParseInt32(line[parts[11]], out int hitMarginValue))
        return false;
      resolvedHitMargin = hitMarginValue;
    }

    long? timeUs = null;
    if (hasTimeUs)
    {
      if (!Utf8Csv.TryParseInt64(line[parts[12]], out long parsedTimeUs))
        return false;
      timeUs = parsedTimeUs;
    }

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
