using System;

namespace TUFReplay.Replay.Models;

/// <summary>
/// ADOFAI plays a level at its pitch times a speed multiplier: the editor's Ctrl+Space speed, a
/// speed trial or practice mode. The game shows the multiplier next to the level name, as in
/// "(0.75x)", so a replay plays at the recorded multiplier instead of the one set now.
/// </summary>
public static class RecordedSpeedMultiplier
{
  public static float From(ReplayMetadata meta) =>
    meta == null ? 1f : From(meta.levelPitchPercent, meta.effectivePitch, meta.pitchSpeedMultiplier);

  /// <summary>
  /// The played pitch over the level's pitch is what the player heard, so it comes first. Older
  /// recordings stored 1 as the multiplier of a practice-mode run that played slower. Without
  /// the level's pitch, the recorded multiplier is used, else 1.
  /// </summary>
  public static float From(int? levelPitchPercent, float? effectivePitch, float? recordedMultiplier)
  {
    if (levelPitchPercent > 0 && IsPositive(effectivePitch))
      return effectivePitch.Value / (levelPitchPercent.Value * 0.01f);
    return IsPositive(recordedMultiplier) ? recordedMultiplier.Value : 1f;
  }

  private static bool IsPositive(float? value) =>
    value.HasValue && value.Value > 0f && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
}
