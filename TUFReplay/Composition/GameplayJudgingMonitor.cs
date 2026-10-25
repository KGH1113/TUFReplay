using System;
using TUFReplay.Shared.Threading;

namespace TUFReplay.Composition;

// Publishes whether ADOFAI is judging input so optional background work can wait.
// Polled once per frame instead of hooked on state changes: scene loads can replace
// the controller without a ChangeState call, and a stale "judging" would hold work.
internal static class GameplayJudgingMonitor
{
  public static void Tick() => GameplayIdleGate.SetJudging(IsJudging());

  private static bool IsJudging()
  {
    try
    {
      scrController controller = ADOBase.controller;
      // The level-select worlds also use PlayerControl to move the planet.
      if (controller == null || ADOBase.isLevelSelect)
        return false;
      return controller.stateMachine?.GetState() is States state
        && (state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl);
    }
    catch (Exception)
    {
      return false;
    }
  }
}
