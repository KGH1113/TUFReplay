namespace TUFReplay.Domain.ReplayData;

public struct RecordedHitContext
{
  public int CurrentFloorID;
  public double CurrAngle;
  public float OverloadCounter;
  public bool NoFailHit;
  public bool IsAuto;
  public bool NextFloorAuto;
  public double CachedAngle;
  public double TargetExitAngle;
  public bool MidspinInfiniteMargin;
  public bool RDCAuto;
  public int CurFreeRoamSection;
}
