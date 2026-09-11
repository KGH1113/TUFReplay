namespace TUFReplay.Replay.Playback;

public readonly struct ReplayHitContext
{
  public readonly int CurrentFloorID;
  public readonly double CurrAngle;
  public readonly float OverloadCounter;
  public readonly bool NoFailHit;
  public readonly bool IsAuto;
  public readonly bool NextFloorAuto;
  public readonly double CachedAngle;
  public readonly double TargetExitAngle;
  public readonly bool MidspinInfiniteMargin;
  public readonly bool RDCAuto;
  public readonly int CurFreeRoamSection;
  public readonly int ResolvedHitMargin;
  public readonly long TimeUs;

  public ReplayHitContext(
    int currentFloorID,
    double currAngle,
    float overloadCounter,
    bool noFailHit,
    bool isAuto,
    bool nextFloorAuto,
    double cachedAngle,
    double targetExitAngle,
    bool midspinInfiniteMargin,
    bool rdcAuto,
    int curFreeRoamSection,
    int resolvedHitMargin,
    long timeUs
  )
  {
    CurrentFloorID = currentFloorID;
    CurrAngle = currAngle;
    OverloadCounter = overloadCounter;
    NoFailHit = noFailHit;
    IsAuto = isAuto;
    NextFloorAuto = nextFloorAuto;
    CachedAngle = cachedAngle;
    TargetExitAngle = targetExitAngle;
    MidspinInfiniteMargin = midspinInfiniteMargin;
    RDCAuto = rdcAuto;
    CurFreeRoamSection = curFreeRoamSection;
    ResolvedHitMargin = resolvedHitMargin;
    TimeUs = timeUs;
  }
}
