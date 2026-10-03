using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Replay.Export;

public static class RenderBundleCsv
{
  public const string InputHeader = "timeUs,key,down,sequence";
  public const string HitHeader =
    "timeUs,floorId,angle,overloadCounter,noFailHit,isAuto,nextFloorAuto,cachedAngle,targetExitAngle,midspinInfiniteMargin,rdcAuto,freeRoamSection,margin";

  public static void WriteInputs(
    TextWriter writer,
    IEnumerable<RecordedInput> inputs,
    string platform,
    long terminalTimeUs,
    CancellationToken cancellation = default
  )
  {
    writer.WriteLine(InputHeader);
    long previous = -1,
      sequence = 0;
    foreach (RecordedInput input in inputs)
    {
      cancellation.ThrowIfCancellationRequested();
      ValidateTime(input.TimeUs, previous, terminalTimeUs);
      if (!NativeInputKeyCodeMapper.TryGetUnityKeyName(platform, input, out string name))
        throw new InvalidDataException("This recording contains an input key the renderer cannot identify.");
      writer.WriteLine(FormattableString.Invariant($"{input.TimeUs},{name},{(input.Down ? 1 : 0)},{sequence++}"));
      previous = input.TimeUs;
    }
  }

  public static void WriteHits(
    TextWriter writer,
    IEnumerable<ReplayHitContext> hits,
    long terminalTimeUs,
    Func<int, string> marginName,
    CancellationToken cancellation = default
  )
  {
    writer.WriteLine(HitHeader);
    long previous = -1;
    foreach (ReplayHitContext hit in hits)
    {
      cancellation.ThrowIfCancellationRequested();
      ValidateTime(hit.TimeUs, previous, terminalTimeUs);
      string margin = marginName(hit.ResolvedHitMargin);
      if (string.IsNullOrEmpty(margin) || !IsSymbol(margin))
        throw new InvalidDataException("This recording contains an unknown hit judgment.");
      writer.WriteLine(
        string.Join(
          ",",
          new[]
          {
            hit.TimeUs.ToString(CultureInfo.InvariantCulture),
            hit.CurrentFloorID.ToString(CultureInfo.InvariantCulture),
            Number(hit.CurrAngle),
            Number(hit.OverloadCounter),
            Bit(hit.NoFailHit),
            Bit(hit.IsAuto),
            Bit(hit.NextFloorAuto),
            Number(hit.CachedAngle),
            Number(hit.TargetExitAngle),
            Bit(hit.MidspinInfiniteMargin),
            Bit(hit.RDCAuto),
            hit.CurFreeRoamSection.ToString(CultureInfo.InvariantCulture),
            margin,
          }
        )
      );
      previous = hit.TimeUs;
    }
  }

  private static void ValidateTime(long time, long previous, long terminal)
  {
    if (time < 0 || time < previous || time > terminal)
      throw new InvalidDataException("The recorded timeline is incomplete or out of order.");
  }

  private static bool IsSymbol(string value)
  {
    foreach (char character in value)
      if (!char.IsLetter(character) && character != '_')
        return false;
    return true;
  }

  private static string Bit(bool value) => value ? "1" : "0";

  private static string Number(double value)
  {
    if (double.IsNaN(value) || double.IsInfinity(value))
      throw new InvalidDataException("The recording contains an invalid hit value.");
    return value.ToString("R", CultureInfo.InvariantCulture);
  }
}
