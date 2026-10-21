using System;
using System.Collections.Generic;
using System.Globalization;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Export;

public class RenderBundleValidationException : Exception
{
  public string Code { get; }
  public string Field { get; }
  public int? Line { get; }
  public string File { get; }

  public RenderBundleValidationException(
    string code,
    string message,
    string field = null,
    int? line = null,
    string file = null
  )
    : base(message)
  {
    Code = code;
    Field = field;
    Line = line;
    File = file;
  }
}

public static class RenderBundleValidation
{
  public const string RecoveredTerminalWarning =
    "This recording saved its end time before gameplay began. The video uses the end of its recorded events. Pre-start key timestamps saved as zero cannot be restored, so their key rain may differ from the original play. Record a new run with the updated TUFReplay to preserve their exact timing.";

  public static void ValidateMetadata(ReplayMetadata metadata)
  {
    if (metadata == null)
      throw new RenderBundleValidationException(
        "render_metadata_missing",
        "The recording metadata is missing. Record a new run with the current TUFReplay version."
      );
    Required(metadata.gameplayStartSongPosition.HasValue, "gameplayStartSongPosition");
    Required(metadata.effectivePitch.HasValue, "effectivePitch");
    Required(metadata.gameInputOffsetMs.HasValue, "gameInputOffsetMs");
    Required(metadata.terminalTimeUs.HasValue, "terminalTimeUs");
    Required(!string.IsNullOrWhiteSpace(metadata.judgmentSystem), "judgmentSystem");
    Valid(Finite(metadata.gameplayStartSongPosition.Value), "gameplayStartSongPosition");
    Valid(Finite(metadata.effectivePitch.Value) && metadata.effectivePitch.Value > 0, "effectivePitch");
    Valid(metadata.terminalTimeUs.Value >= 0, "terminalTimeUs");
    Valid(
      metadata.hitMarginLimit == null
        || metadata.hitMarginLimit == "None"
        || metadata.hitMarginLimit == "PerfectsOnly"
        || metadata.hitMarginLimit == "PurePerfectOnly",
      "hitMarginLimit"
    );
    Valid(
      !metadata.wonTimeUs.HasValue
        || (metadata.wonTimeUs.Value >= 0 && metadata.wonTimeUs.Value <= metadata.terminalTimeUs.Value),
      "wonTimeUs"
    );
  }

  public static long ResolveTerminalTimeUs(
    ReplayMetadata metadata,
    string result,
    IReadOnlyList<RecordedInput> inputs,
    IReadOnlyList<ReplayHitContext> hits,
    out bool recovered
  )
  {
    recovered = false;
    long terminal = metadata.terminalTimeUs.Value;
    // Older recorder sessions could be rearmed after a pre-start terminal
    // without clearing that terminal. Recover only that observed signature;
    // ordinary events after a nonzero terminal remain invalid.
    if (
      terminal != 0L
      || metadata.wonTimeUs.HasValue
      || !string.Equals(result, "aborted", StringComparison.OrdinalIgnoreCase)
      || metadata.inputFormat != RecordedRunPayload.NativeInputFormatV2
      || metadata.inputTimeBase != ReplayInputTimeBases.Hybrid
      || metadata.inputInvalidAnchors <= 0L
      || metadata.inputDiscontinuities < 2L
      || metadata.inputLastDiscontinuity != "fail"
      || inputs == null
      || inputs.Count == 0
      || hits == null
      || hits.Count < 2
      || !DateTimeOffset.TryParse(
        metadata.startedAtUtc,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal,
        out var started
      )
      || !DateTimeOffset.TryParse(
        metadata.endedAtUtc,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal,
        out var ended
      )
      || ended < started
      || (ended - started).TotalMilliseconds > 50d
    )
      return terminal;

    long lastInput = inputs[inputs.Count - 1].TimeUs;
    long lastHit = hits[hits.Count - 1].TimeUs;
    // The observed final input is a stop key shortly after the final hit.
    // Do not extend a video over arbitrary later input or an unproven hit tail.
    if (lastHit <= 0L || lastInput < lastHit || lastInput - lastHit > 1_000_000L)
      return terminal;
    recovered = true;
    return lastInput;
  }

  private static void Required(bool present, string field)
  {
    if (!present)
      throw new RenderBundleValidationException(
        "render_metadata_field_missing",
        "A required recording setting is missing. Record a new run with the current TUFReplay version.",
        field
      );
  }

  private static void Valid(bool valid, string field)
  {
    if (!valid)
      throw new RenderBundleValidationException(
        "render_metadata_field_invalid",
        "A recording setting has an invalid value. Export the recording again; if it still fails, record a new run.",
        field
      );
  }

  private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
