using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TUFReplay.Replay.Rendering;

// These patches exist only while a hosted render replays a recording. TUFReplay's own camera
// first-run check reads RenderReplayHost.Active instead of being patched.
internal static class RenderReplayHooks
{
  private static readonly Harmony Harmony = new("TUFReplay.RenderReplay");
  internal static RenderReplayHost Current { get; private set; }

  internal static void Activate(RenderReplayHost driver)
  {
    if (Current != null)
      throw new InvalidOperationException("Another recorded render is already running.");
    Current = driver;
    try
    {
      foreach (var target in SuppressedMethods())
        Patch(target, nameof(Suppress));
      Patch(AccessTools.Method(typeof(scrPlayer), nameof(scrPlayer.Hit)), nameof(AllowRecordedHit));
      Harmony.Patch(
        AccessTools.Method(typeof(scrPlayer), nameof(scrPlayer.Die)),
        prefix: new HarmonyMethod(typeof(RenderReplayHooks), nameof(AllowRecordedFailure))
        {
          priority = Priority.First,
        },
        postfix: new HarmonyMethod(typeof(RenderReplayHooks), nameof(DeathStarted))
      );
      Patch(AccessTools.Method(typeof(scrPlanet), nameof(scrPlanet.MarkFail)), nameof(AllowRecordedFailure));
      Harmony.Patch(
        AccessTools.Method(typeof(scrController), nameof(scrController.Fail2Action)),
        postfix: new HarmonyMethod(typeof(RenderReplayHooks), nameof(DeathAnimationCompleted))
      );
      Patch(AccessTools.Method(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen)), nameof(RestoreCachedAngle));
      Patch(AccessTools.Method(typeof(scrMisc), nameof(scrMisc.GetHitMarginInDeg)), nameof(RecordedMargin));
      Patch(AccessTools.Method(typeof(scrMisc), nameof(scrMisc.GetHitMarginInSec)), nameof(RecordedMargin));
      Patch(AccessTools.PropertyGetter(typeof(scrPlayer), nameof(scrPlayer.holding)), nameof(Holding));
      Patch(AccessTools.Method(typeof(scrController), nameof(scrController.UpdateFreeroam)), nameof(AllowFreeroam));
      Patch(AccessTools.Method(typeof(scrConductor), nameof(scrConductor.StartMusic)), nameof(PrepareMusic));
      Patch(
        AccessTools.Method(typeof(scrLevelMaker), nameof(scrLevelMaker.CalculateFloorEntryTimes)),
        nameof(PrepareMusic)
      );
      Harmony.Patch(
        AccessTools.Method(typeof(scrConductor), nameof(scrConductor.SetupConductorWithLevelData)),
        postfix: new HarmonyMethod(typeof(RenderReplayHooks), nameof(PrepareMusic)) { priority = Priority.Last }
      );
      Patch(AccessTools.Method(typeof(scrMistakesManager), nameof(scrMistakesManager.SaveCustom)), nameof(SkipScores));
      Patch(AccessTools.Method(typeof(scrMistakesManager), nameof(scrMistakesManager.Save)), nameof(SkipScores));
      Patch(AccessTools.PropertyGetter(typeof(Persistence), "enableCompetitiveMode"), nameof(CompetitiveMode));
      Patch(AccessTools.PropertyGetter(typeof(scrConductor), "calibration_i"), nameof(Calibration));
      // Direct mutation, including deletion of checkpoint data, must be blocked as well as disk writes.
      foreach (MethodInfo method in typeof(Persistence).GetMethods(BindingFlags.Public | BindingFlags.Static))
        if (
          method.ReturnType == typeof(void)
          && (
            method.Name.StartsWith("Set", StringComparison.Ordinal)
            || method.Name.StartsWith("Delete", StringComparison.Ordinal)
            || method.Name == "Save"
          )
        )
          Patch(method, nameof(Suppress));
    }
    catch
    {
      Deactivate(driver);
      throw;
    }
  }

  private static IEnumerable<MethodBase> SuppressedMethods()
  {
    yield return AccessTools.Method(typeof(scrController), nameof(scrController.UpdateInput));
    yield return AccessTools.Method(typeof(scrController), nameof(scrController.PortalTravelAction));
    yield return AccessTools.Method(typeof(scrController), nameof(scrController.SaveProgress));
    yield return AccessTools.Method(typeof(scrController), "Fail2_Update");
    yield return AccessTools.Method(typeof(scrPlayer), nameof(scrPlayer.Simulated_PlayerControl_Update));
    yield return AccessTools.Method(typeof(scrPlanet), "AsyncRefreshAngles");
    yield return AccessTools.Method(typeof(scrMistakesManager), "SaveCheckpointProgress");
  }

  private static void Patch(MethodBase target, string prefix)
  {
    if (target == null)
      throw new MissingMethodException("This game version does not expose the recorded-render contract.");
    Harmony.Patch(target, prefix: new HarmonyMethod(typeof(RenderReplayHooks), prefix) { priority = Priority.First });
  }

  internal static void Deactivate(RenderReplayHost driver)
  {
    if (Current != driver)
      return;
    Current = null;
    Harmony.UnpatchAll(Harmony.Id);
  }

  private static bool Suppress() => Current == null;

  private static bool AllowRecordedHit() => Current == null || Current.ApplyingRecordedHit;

  private static bool AllowRecordedFailure() =>
    Current == null || Current.ApplyingRecordedHit || Current.ApplyingTerminalDeath;

  private static void DeathStarted() => Current?.DeathStarted();

  private static void DeathAnimationCompleted() => Current?.DeathAnimationCompleted();

  private static bool AllowFreeroam() => Current == null || Current.ApplyingFreeroam;

  private static void PrepareMusic() => Current?.ApplyPreparedSettings();

  private static void RestoreCachedAngle(scrPlanet __instance)
  {
    if (Current?.ActiveHit is RecordedHitEvent hit)
      __instance.cachedAngle = hit.CachedAngle;
  }

  private static bool RecordedMargin(ref HitMargin __result)
  {
    if (Current?.ApplyingRecordedHit != true)
      return true;
    __result = Current.ActiveMargin;
    return false;
  }

  private static bool Holding(ref bool __result)
  {
    if (Current == null)
      return true;
    __result = Current.HasHeldKeys;
    return false;
  }

  private static bool CompetitiveMode(ref bool __result)
  {
    if (Current == null)
      return true;
    __result = Current.CompetitiveMode;
    return false;
  }

  private static bool Calibration(ref float __result)
  {
    if (Current == null)
      return true;
    __result = Current.InputCalibrationSeconds;
    return false;
  }

  private static bool SkipScores(scrMistakesManager __instance, ref scrMistakesManager.EndLevelInfo __result)
  {
    if (Current == null)
      return true;
    __instance.CalculateTotalAccuracy();
    __result = new scrMistakesManager.EndLevelInfo { endLevelType = EndLevelType.None, newBestType = NewBestType.None };
    return false;
  }
}
