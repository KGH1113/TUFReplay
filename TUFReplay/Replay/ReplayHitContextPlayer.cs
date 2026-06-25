using System.Collections.Generic;
using UnityEngine;

namespace TUFReplay.Replay;

public sealed class ReplayHitContextPlayer
{
  private readonly List<ReplayHitContext> _contexts;
  private int _nextIndex;

  public ReplayHitContextPlayer(List<ReplayHitContext> contexts)
  {
    _contexts = contexts ?? new List<ReplayHitContext>();
  }

  public int Count => _contexts.Count;
  public int NextIndex => _nextIndex;
  public bool Finished => _nextIndex >= _contexts.Count;

  public void Reset()
  {
    _nextIndex = 0;
  }

  public int ResetTo(scrController controller, bool skipPassedAngles)
  {
    _nextIndex = 0;

    if (controller == null || controller.currFloor == null || _contexts.Count == 0)
    {
      return _nextIndex;
    }

    int floor = controller.currFloor.seqID;
    int freeRoamSection = controller.curFreeRoamSection;
    float angle = skipPassedAngles ? GetCurrentAngle(controller) : float.NegativeInfinity;

    while (_nextIndex < _contexts.Count &&
           IsBeforePlaybackPosition(_contexts[_nextIndex], floor, freeRoamSection, angle, skipPassedAngles))
    {
      _nextIndex++;
    }

    return _nextIndex;
  }

  public ReplayHitContext? PeekNext()
  {
    if (_nextIndex >= _contexts.Count) return null;
    return _contexts[_nextIndex];
  }

  public HitContextTickResult Tick(scrController controller)
  {
    if (controller == null || _contexts.Count == 0 || _nextIndex >= _contexts.Count)
    {
      return HitContextTickResult.None;
    }

    if (controller.currFloor == null || controller.playerOne == null)
    {
      return HitContextTickResult.None;
    }

    int played = 0;
    int ignoredFalseResults = 0;
    ReplayHitContext next = _contexts[_nextIndex];

    if (controller.currFloor.seqID != next.CurrentFloorID ||
        controller.curFreeRoamSection > next.CurFreeRoamSection)
    {
      return HitContextTickResult.Failed(
        "mismatch floor=" + controller.currFloor.seqID +
        ", expected=" + next.CurrentFloorID +
        ", freeRoam=" + controller.curFreeRoamSection +
        ", expectedFreeRoam=" + next.CurFreeRoamSection
      );
    }

    float angle = GetCurrentAngle(controller);
    while (controller.currFloor.seqID == next.CurrentFloorID &&
           controller.curFreeRoamSection == next.CurFreeRoamSection &&
           angle >= next.CurrAngle)
    {
      if (!PlayHit(controller.playerOne, next))
      {
        ignoredFalseResults++;
      }

      played++;
      _nextIndex++;

      if (_nextIndex >= _contexts.Count)
      {
        break;
      }

      next = _contexts[_nextIndex];

      if (controller.currFloor.seqID != next.CurrentFloorID ||
          controller.curFreeRoamSection > next.CurFreeRoamSection)
      {
        return HitContextTickResult.Failed(
          "mismatch after hit floor=" + controller.currFloor.seqID +
          ", expected=" + next.CurrentFloorID +
          ", freeRoam=" + controller.curFreeRoamSection +
          ", expectedFreeRoam=" + next.CurFreeRoamSection
        );
      }

      angle = GetCurrentAngle(controller);
    }

    return played > 0 ? HitContextTickResult.Played(played, ignoredFalseResults) : HitContextTickResult.None;
  }

  public bool ShouldBlockFreeroam(scrController controller)
  {
    if (controller == null || _nextIndex >= _contexts.Count) return false;

    ReplayHitContext next = _contexts[_nextIndex];
    return next.CurFreeRoamSection == controller.curFreeRoamSection;
  }

  private static bool PlayHit(scrPlayer player, ReplayHitContext hit)
  {
    player.consecMultipressCounter = 0;
    scrController.instance.multipressPenalty = false;
    player.failBar.overloadCounter = 0f;

    scrController.instance.noFailInfiniteMargin = hit.NoFailHit;
    bool isAuto = hit.IsAuto;

    player.planetarySystem.chosenPlanet.SetTargetExitAngle(hit.TargetExitAngle);
    player.midspinInfiniteMargin = hit.MidspinInfiniteMargin;
    player.failBar.overloadCounter = hit.OverloadCounter;
    player.planetarySystem.chosenPlanet.angle = hit.CachedAngle;
    RDC.auto = hit.IsAuto;

    player.responsive = true;

    ReplayService.AllowReplayMarkFailOnce();
    if (hit.NoFailHit)
    {
      scrMissIndicator indicator = player.planetarySystem.chosenPlanet.MarkFail();
      if (indicator != null)
      {
        indicator.BlinkForSeconds(3f);
      }
    }
    ReplayService.SuppressReplayMarkFail();

    scrMisc.Vibrate(50L);
    if (!player.responsive)
    {
      return false;
    }

    if (ADOBase.isLevelEditor && ADOBase.controller.paused)
    {
      return false;
    }

    bool nextFloorAuto = player.planetarySystem.chosenPlanet.currfloor.nextfloor != null &&
                         player.planetarySystem.chosenPlanet.currfloor.nextfloor.auto;
    player.planetarySystem.chosenPlanet.cachedAngle = player.planetarySystem.chosenPlanet.angle;

    if (!player.HitInputEvent(isAuto, InputEventState.Down))
    {
      return false;
    }

    player.planetarySystem.chosenPlanet.next.planetRenderer.ChangeFace(true);
    scrPlanet hitPlanet = player.planetarySystem.chosenPlanet;
    player.planetarySystem.chosenPlanet = player.planetarySystem.chosenPlanet.SwitchChosen();
    bool result = hitPlanet != player.planetarySystem.chosenPlanet;

    if (ADOBase.controller.errorMeter &&
        ADOBase.controller.gameworld &&
        Persistence.hitErrorMeterSize != ErrorMeterSize.Off)
    {
      float angleDiff = (float)(hitPlanet.cachedAngle - hitPlanet.targetExitAngle);
      if (hitPlanet.currfloor.isCCW)
      {
        angleDiff *= -1f;
      }

      if (!player.midspinInfiniteMargin)
      {
        if ((player.auto || nextFloorAuto) && !RDC.useOldAuto)
        {
          ADOBase.controller.errorMeter.AddHit(0f, 1f, player.planetarySystem.chosenPlanet, hitPlanet.player.currFloor);
        }
        else
        {
          ADOBase.controller.errorMeter.AddHit(
            angleDiff,
            (float)player.currFloor.marginScale,
            player.planetarySystem.chosenPlanet,
            hitPlanet.player.currFloor
          );
        }
      }
    }

    if (ADOBase.playerIsOnIntroScene)
    {
      return result;
    }

    bool shouldPulseCamera = player.planetarySystem.chosenPlanet.currfloor.holdLength == -1 ||
                             (player.planetarySystem.chosenPlanet.currfloor.holdLength > -1 &&
                              ADOBase.controller.lastCamPulseFloor < player.planetarySystem.chosenPlanet.currfloor.seqID);
    ADOBase.controller.lastCamPulseFloor = player.planetarySystem.chosenPlanet.currfloor.seqID;

    scrCamera camera = ADOBase.controller.camy;
    if (shouldPulseCamera)
    {
      camera.UpdateFollowCam();
    }

    if (camera.isPulsingOnHit && shouldPulseCamera)
    {
      camera.Pulse();
    }

    bool shouldHitAvatar = true;
    if (ADOBase.lm != null && ADOBase.controller.gameworld)
    {
      if (player.currFloor.midSpin ||
          (player.currFloor.seqID > 0 && ADOBase.lm.listFloors[player.currFloor.seqID - 1].holdLength > -1))
      {
        shouldHitAvatar = false;
      }

      if (player.currFloor.seqID > 1 &&
          ADOBase.lm.listFloors[player.currFloor.seqID - 1].midSpin &&
          ADOBase.lm.listFloors[player.currFloor.seqID - 2].holdLength > -1)
      {
        shouldHitAvatar = false;
      }
    }

    if (shouldHitAvatar)
    {
      if (scnEditor.instance != null)
      {
        scnEditor.instance.OttoBlink();
      }

      if (VirtualAvatarCanvas.instance != null)
      {
        VirtualAvatarCanvas.instance.Hit(player.playerID);
      }
    }

    if (player.currFloor.midSpin)
    {
      player.midspinInfiniteMargin = true;
      player.keyTimes.Add(Time.unscaledTimeAsDouble);
    }
    else
    {
      player.midspinInfiniteMargin = false;
    }

    player.planetarySystem.chosenPlanet.Update_RefreshAngles();
    ReplayService.SuppressReplayMarkFail();
    return result;
  }

  private static float GetCurrentAngle(scrController controller)
  {
    float angle = (float)(controller.chosenPlanet.angle - controller.chosenPlanet.targetExitAngle);
    if (!controller.playerOne.planetarySystem.isCW)
    {
      angle *= -1f;
    }

    return angle;
  }

  private static bool IsBeforePlaybackPosition(
    ReplayHitContext context,
    int floor,
    int freeRoamSection,
    float angle,
    bool compareAngle
  )
  {
    if (context.CurrentFloorID < floor) return true;
    if (context.CurrentFloorID > floor) return false;
    if (context.CurFreeRoamSection < freeRoamSection) return true;
    if (context.CurFreeRoamSection > freeRoamSection) return false;

    return compareAngle && context.CurrAngle <= angle;
  }
}

public readonly struct HitContextTickResult
{
  public readonly int PlayedCount;
  public readonly int IgnoredFalseResultCount;
  public readonly string ErrorMessage;

  public bool PlayedAny => PlayedCount > 0;
  public bool HasError => ErrorMessage != null;

  private HitContextTickResult(int playedCount, int ignoredFalseResultCount, string errorMessage)
  {
    PlayedCount = playedCount;
    IgnoredFalseResultCount = ignoredFalseResultCount;
    ErrorMessage = errorMessage;
  }

  public static HitContextTickResult None => new HitContextTickResult(0, 0, null);
  public static HitContextTickResult Played(int playedCount, int ignoredFalseResultCount) =>
    new HitContextTickResult(playedCount, ignoredFalseResultCount, null);
  public static HitContextTickResult Failed(string errorMessage) => new HitContextTickResult(0, 0, errorMessage);
}
