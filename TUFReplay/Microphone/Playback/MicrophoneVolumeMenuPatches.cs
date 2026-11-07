using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TUFReplay.Replay.Playback;
using UnityEngine;

namespace TUFReplay.Microphone.Playback;

// Adds the microphone volume to ADOFAI's audio settings. The menu is
// built from a JSON asset, and each row's value is read and written by name, so
// the row needs the JSON entry, its own value handling and its label text. If
// ADOFAI changes any of the three methods, none of them is patched and the
// menu stays as it is.
internal static class MicrophoneVolumeMenuTargets
{
  internal static readonly MethodBase GenerateSettings = AccessTools.Method(typeof(SettingsMenu), "GenerateSettings");

  internal static readonly MethodBase UpdateSetting = AccessTools.Method(
    typeof(SettingsMenu),
    nameof(SettingsMenu.UpdateSetting),
    new[] { typeof(PauseSettingButton), typeof(SettingsMenu.Interaction) }
  );

  internal static readonly MethodBase GetWithCheck = AccessTools.Method(
    typeof(RDString),
    nameof(RDString.GetWithCheck),
    new[] { typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>) }
  );

  internal static readonly MethodInfo TextAssetText = AccessTools.PropertyGetter(
    typeof(TextAsset),
    nameof(TextAsset.text)
  );

  private static bool? _available;

  internal static bool Available
  {
    get
    {
      if (_available.HasValue)
        return _available.Value;
      _available = GenerateSettings != null && UpdateSetting != null && GetWithCheck != null && TextAssetText != null;
      if (!_available.Value)
        Main.Instance?.Log("[Settings] ADOFAI's settings menu changed; the microphone volume row is not added.");
      return _available.Value;
    }
  }

  internal static bool IsKorean => RDString.language == SystemLanguage.Korean;
}

[HarmonyPatch]
internal static class MicrophoneVolumeMenuRowPatch
{
  [HarmonyPrepare]
  private static bool Prepare() => MicrophoneVolumeMenuTargets.Available;

  private static MethodBase TargetMethod() => MicrophoneVolumeMenuTargets.GenerateSettings;

  // Passes the menu JSON through MicrophoneVolumeMenu.AddRow right
  // after GenerateSettings reads it from the asset.
  [HarmonyTranspiler]
  private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    MethodInfo addRow = AccessTools.Method(typeof(MicrophoneVolumeMenu), nameof(MicrophoneVolumeMenu.AddRow));
    bool added = false;
    foreach (CodeInstruction instruction in instructions)
    {
      yield return instruction;
      if (!added && instruction.Calls(MicrophoneVolumeMenuTargets.TextAssetText))
      {
        yield return new CodeInstruction(OpCodes.Call, addRow);
        added = true;
      }
    }
    if (!added)
      Main.Instance?.Log("[Settings] ADOFAI's settings menu JSON was not found; the microphone row is not added.");
  }
}

[HarmonyPatch]
internal static class MicrophoneVolumeMenuUpdatePatch
{
  [HarmonyPrepare]
  private static bool Prepare() => MicrophoneVolumeMenuTargets.Available;

  private static MethodBase TargetMethod() => MicrophoneVolumeMenuTargets.UpdateSetting;

  // ADOFAI has no getter or setter for this row, so it is handled here and the
  // original method is skipped for it.
  [HarmonyPrefix]
  private static bool Prefix(PauseSettingButton setting, SettingsMenu.Interaction action)
  {
    if (setting == null || !string.Equals(setting.name, MicrophoneVolumeMenu.RowName, StringComparison.Ordinal))
      return true;
    try
    {
      Update(setting, action);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("MicrophoneVolumeMenu", exception);
    }
    return false;
  }

  private static void Update(PauseSettingButton setting, SettingsMenu.Interaction action)
  {
    bool increment = action == SettingsMenu.Interaction.Increment;
    bool decrement = action == SettingsMenu.Interaction.Decrement;
    int step = MicrophoneVolumeMenu.ToStep(MicrophoneVolumeService.Current);
    int next = step;
    if (increment || decrement)
    {
      int amount =
        setting.changeBy <= 0 ? 1
        : RDInput.holdingShift ? setting.changeBySmall
        : setting.changeBy;
      next = MicrophoneVolumeMenu.Move(step, increment ? amount : -amount);
    }

    setting.leftArrow.transform.localScale = next <= MicrophoneVolumeMenu.OffStep ? Vector3.zero : Vector3.one;
    setting.rightArrow.transform.localScale = next >= MicrophoneVolumeMenu.MaxStep ? Vector3.zero : Vector3.one;
    setting.valueLabel.text = MicrophoneVolumeMenu.FormatValue(next, MicrophoneVolumeMenuTargets.IsKorean);
    if (action == SettingsMenu.Interaction.Refresh)
      setting.initialValue = next;
    if (next == step)
      return;

    MicrophoneVolumeService.Set(MicrophoneVolumeMenu.FromStep(next));
    setting.PlayArrowAnimation(isRight: increment);
    scrController controller = scrController.instance;
    PauseMenu pauseMenu = controller != null ? controller.pauseMenu : null;
    if (pauseMenu != null)
      pauseMenu.PlayMenuSfx(increment ? SfxSound.MenuIncrement : SfxSound.MenuDecrement);
  }
}

[HarmonyPatch]
internal static class MicrophoneVolumeMenuTextPatch
{
  [HarmonyPrepare]
  private static bool Prepare() => MicrophoneVolumeMenuTargets.Available;

  private static MethodBase TargetMethod() => MicrophoneVolumeMenuTargets.GetWithCheck;

  [HarmonyPrefix]
  private static bool Prefix(string key, ref bool exists, ref string __result)
  {
    string text = MicrophoneVolumeMenu.Text(key, MicrophoneVolumeMenuTargets.IsKorean);
    if (text == null)
      return true;
    exists = true;
    __result = text;
    return false;
  }
}
