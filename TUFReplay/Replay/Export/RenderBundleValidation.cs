using System;
using TUFReplay.Replay.Models;

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
      !metadata.wonTimeUs.HasValue
        || (metadata.wonTimeUs.Value >= 0 && metadata.wonTimeUs.Value <= metadata.terminalTimeUs.Value),
      "wonTimeUs"
    );
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
