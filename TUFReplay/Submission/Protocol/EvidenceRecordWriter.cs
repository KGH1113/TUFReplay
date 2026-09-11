using System.Globalization;
using System.Text;
using TUFReplay.Submission.Capture;

namespace TUFReplay.Submission.Protocol;

/// <summary>Wire formatting runs only on the upload consumer.</summary>
public static class EvidenceRecordWriter
{
  private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

  public static void Append(StringBuilder text, CaptureRecord record)
  {
    if (record.Kind >= 3)
    {
      var state = record.State;
      text.Append(Newtonsoft.Json.JsonConvert.SerializeObject(new {
        version = 1, state = state.State.ToString(), time_us = state.TimeUs, rate = state.Rate,
        no_fail = state.NoFail, difficulty = state.Difficulty, dropped = state.Dropped, unmapped = state.Unmapped,
        hold_behavior = state.HoldBehavior,
      })).Append('\n');
      return;
    }
    if (record.Kind == 0)
    {
      var input = record.Input;
      text.Append(input.TimeUs.ToString(Invariant)).Append(',')
        .Append(input.Key.ToString(Invariant)).Append(',')
        .Append(((ushort)input.Flags).ToString(Invariant)).Append(',')
        .Append(input.NativeCode.ToString(Invariant)).Append(',')
        .Append(input.NativeFlags.ToString(Invariant)).Append('\n');
      return;
    }
    var hit = record.Hit;
    text.Append(hit.CurrentFloorID.ToString(Invariant)).Append(',')
      .Append(hit.CurrAngle.ToString("R", Invariant)).Append(',')
      .Append(hit.OverloadCounter.ToString("R", Invariant)).Append(',')
      .Append(hit.NoFailHit ? '1' : '0').Append(',')
      .Append(hit.IsAuto ? '1' : '0').Append(',')
      .Append(hit.NextFloorAuto ? '1' : '0').Append(',')
      .Append(hit.CachedAngle.ToString("R", Invariant)).Append(',')
      .Append(hit.TargetExitAngle.ToString("R", Invariant)).Append(',')
      .Append(hit.MidspinInfiniteMargin ? '1' : '0').Append(',')
      .Append(hit.RDCAuto ? '1' : '0').Append(',')
      .Append(hit.CurFreeRoamSection.ToString(Invariant)).Append(',')
      .Append(hit.ResolvedHitMargin?.ToString(Invariant) ?? "").Append(',')
      .Append(hit.TimeUs?.ToString(Invariant) ?? "").Append('\n');
  }
}
