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
    long previous = long.MinValue,
      sequence = 0;
    foreach (RecordedInput input in inputs)
    {
      cancellation.ThrowIfCancellationRequested();
      ValidateTime(input.TimeUs, previous, terminalTimeUs, checked((int)sequence + 2), "inputs.csv");
      if (!NativeInputKeyCodeMapper.TryGetUnityKeyName(platform, input, out string name))
        throw new RenderBundleValidationException(
          "render_input_key_unsupported",
          "A recorded key cannot be mapped for rendering. Check the recording's original keyboard platform.",
          "key",
          checked((int)sequence + 2),
          "inputs.csv"
        );
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
    long previous = long.MinValue;
    int row = 1;
    foreach (ReplayHitContext hit in hits)
    {
      cancellation.ThrowIfCancellationRequested();
      row++;
      ValidateTime(hit.TimeUs, previous, terminalTimeUs, row, "hits.csv");
      string margin = marginName(hit.ResolvedHitMargin);
      if (string.IsNullOrEmpty(margin) || !IsSymbol(margin))
        throw new RenderBundleValidationException(
          "render_hit_judgment_unsupported",
          "A recorded judgment is not supported by this game version. Use the original compatible game version or record a new run.",
          "margin",
          row,
          "hits.csv"
        );
      writer.WriteLine(
        string.Join(
          ",",
          new[]
          {
            hit.TimeUs.ToString(CultureInfo.InvariantCulture),
            hit.CurrentFloorID.ToString(CultureInfo.InvariantCulture),
            Number(hit.CurrAngle, "angle", row),
            Number(hit.OverloadCounter, "overloadCounter", row),
            Bit(hit.NoFailHit),
            Bit(hit.IsAuto),
            Bit(hit.NextFloorAuto),
            Number(hit.CachedAngle, "cachedAngle", row),
            Number(hit.TargetExitAngle, "targetExitAngle", row),
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

  private static void ValidateTime(long time, long previous, long terminal, int row, string file)
  {
    // The recording origin is gameplay start; countdown keys legitimately precede zero.
    if (time < previous)
      throw new RenderBundleValidationException(
        "render_timeline_out_of_order",
        "Recorded events are out of time order. Export the recording again or record a new run.",
        "timeUs",
        row,
        file
      );
    if (time > terminal)
      throw new RenderBundleValidationException(
        "render_event_after_terminal",
        "A recorded event occurs after the recording's end time. Export the recording again or record a new run.",
        "timeUs",
        row,
        file
      );
  }

  private static bool IsSymbol(string value)
  {
    foreach (char character in value)
      if (!char.IsLetter(character) && character != '_')
        return false;
    return true;
  }

  private static string Bit(bool value) => value ? "1" : "0";

  private static string Number(double value, string field, int row)
  {
    if (double.IsNaN(value) || double.IsInfinity(value))
      throw new RenderBundleValidationException(
        "render_hit_value_invalid",
        "A recorded hit contains an invalid value. Export the recording again or record a new run.",
        field,
        row,
        "hits.csv"
      );
    return value.ToString("R", CultureInfo.InvariantCulture);
  }
}
