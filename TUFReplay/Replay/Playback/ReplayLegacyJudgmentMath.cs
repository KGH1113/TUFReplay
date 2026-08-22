namespace TUFReplay.Replay.Playback;

internal static class ReplayLegacyJudgmentMath
{
  private const float OverloadDamagePerMiss = 0.5f;
  private const float DrumControllerDamageDivisor = 1.7f;

  public static bool BecomesFailOverload(float overloadCounter, bool drumController, bool purePerfectOnly, bool noFail)
  {
    float damage = drumController ? OverloadDamagePerMiss / DrumControllerDamageDivisor : OverloadDamagePerMiss;
    bool overloaded = overloadCounter + damage > 1f;
    return overloaded && (!purePerfectOnly || noFail);
  }
}
