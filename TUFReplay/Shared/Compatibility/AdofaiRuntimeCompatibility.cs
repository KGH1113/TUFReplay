using System;
using System.Reflection;
using HarmonyLib;
using TUFReplay.Activity.Models;

namespace TUFReplay.Shared.Compatibility;

internal static class AdofaiRuntimeCompatibility
{
  private delegate scrPlanet SwitchChosenWithTick(scrPlanet planet, long? hitTick);
  private delegate scrPlanet SwitchChosenLegacy(scrPlanet planet);
  private delegate void UpdateHitErrorMeter(
    scrController controller,
    scrFloor floor,
    scrPlayer player,
    scrPlanet planet
  );
  private delegate void AddLegacyHit(
    scrHitErrorMeter meter,
    float angleDiff,
    float marginScale,
    scrPlanet planet,
    scrFloor floor
  );

  private static readonly MethodInfo ModernHitMethod = AccessTools.Method(
    typeof(scrPlayer),
    "Hit",
    new[] { typeof(long?), typeof(bool) }
  );
  private static readonly MethodInfo LegacyHitMethod = AccessTools.Method(
    typeof(scrPlayer),
    "Hit",
    new[] { typeof(bool) }
  );
  private static readonly SwitchChosenWithTick ModernSwitchChosen = CreateDelegate<SwitchChosenWithTick>(
    AccessTools.Method(typeof(scrPlanet), "SwitchChosen", new[] { typeof(long?) })
  );
  private static readonly SwitchChosenLegacy LegacySwitchChosen = CreateDelegate<SwitchChosenLegacy>(
    AccessTools.Method(typeof(scrPlanet), "SwitchChosen", Type.EmptyTypes)
  );
  private static readonly UpdateHitErrorMeter ModernUpdateHitErrorMeter = CreateDelegate<UpdateHitErrorMeter>(
    AccessTools.Method(
      typeof(scrController),
      "UpdateHitErrorMeter",
      new[] { typeof(scrFloor), typeof(scrPlayer), typeof(scrPlanet) }
    )
  );
  private static readonly AddLegacyHit LegacyAddHit = CreateDelegate<AddLegacyHit>(
    AccessTools.Method(
      typeof(scrHitErrorMeter),
      "AddHit",
      new[] { typeof(float), typeof(float), typeof(scrPlanet), typeof(scrFloor) }
    )
  );
  private static readonly PropertyInfo CompetitiveModeProperty = AccessTools.Property(
    typeof(Persistence),
    "enableCompetitiveMode"
  );

  internal static bool HasModernHitMargins => Enum.IsDefined(typeof(HitMargin), "XPerfect");

  internal static MethodBase ResolvePlayerHitMethod()
  {
    return ModernHitMethod ?? LegacyHitMethod ?? throw new MissingMethodException(typeof(scrPlayer).FullName, "Hit");
  }

  internal static RunJudgmentSystem CaptureJudgmentSystem()
  {
    if (!HasModernHitMargins)
      return RunJudgmentSystem.Legacy;

    try
    {
      return CompetitiveModeProperty != null && (bool)CompetitiveModeProperty.GetValue(null)
        ? RunJudgmentSystem.ModernCompetitive
        : RunJudgmentSystem.ModernClassic;
    }
    catch
    {
      return RunJudgmentSystem.ModernClassic;
    }
  }

  internal static RunJudgmentSystem ParseJudgmentSystem(string value)
  {
    return Enum.TryParse(value, out RunJudgmentSystem system) && Enum.IsDefined(typeof(RunJudgmentSystem), system)
      ? system
      : CaptureJudgmentSystem();
  }

  internal static int ReadHitCount(int[] hits, string marginName)
  {
    if (hits == null || !Enum.TryParse(marginName, out HitMargin margin))
      return 0;

    int index = (int)margin;
    return index >= 0 && index < hits.Length ? Math.Max(0, hits[index]) : 0;
  }

  internal static string GetHitMarginName(int value)
  {
    return Enum.GetName(typeof(HitMargin), value);
  }

  internal static scrPlanet SwitchChosen(scrPlanet planet)
  {
    if (ModernSwitchChosen != null)
      return ModernSwitchChosen(planet, null);
    if (LegacySwitchChosen != null)
      return LegacySwitchChosen(planet);
    throw new MissingMethodException(typeof(scrPlanet).FullName, "SwitchChosen");
  }

  internal static void UpdateErrorMeter(
    scrController controller,
    scrFloor hitFloor,
    scrPlayer player,
    scrPlanet priorChosenPlanet,
    bool nextFloorAuto
  )
  {
    if (ModernUpdateHitErrorMeter != null)
    {
      ModernUpdateHitErrorMeter(controller, hitFloor, player, priorChosenPlanet);
      return;
    }

    if (
      LegacyAddHit == null
      || !controller.errorMeter
      || !controller.gameworld
      || Persistence.hitErrorMeterSize == ErrorMeterSize.Off
      || player.midspinInfiniteMargin
    )
      return;

    float angleDiff = (float)(priorChosenPlanet.cachedAngle - priorChosenPlanet.targetExitAngle);
    if (priorChosenPlanet.currfloor.isCCW)
      angleDiff *= -1f;

    if ((player.auto || nextFloorAuto) && !RDC.useOldAuto)
      LegacyAddHit(controller.errorMeter, 0f, 1f, player.planetarySystem.chosenPlanet, hitFloor);
    else
      LegacyAddHit(
        controller.errorMeter,
        angleDiff,
        (float)player.currFloor.marginScale,
        player.planetarySystem.chosenPlanet,
        hitFloor
      );
  }

  private static T CreateDelegate<T>(MethodInfo method)
    where T : class
  {
    if (method == null)
      return null;
    return Delegate.CreateDelegate(typeof(T), method) as T;
  }
}
