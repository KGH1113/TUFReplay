using JALib.Core;
using HarmonyLib;
using MonsterLove.StateMachine;
using System;
using System.Collections.Generic;
using TUFHelper.Utils;
using TUFReplay.Recording;
using TUFReplay.Shared;

namespace TUFReplay.Replay;

class Replay : Feature
{
  public static Replay Instance;
  public static ReplaySetting Settings;

  public ReplaySession Session { get; private set; }
  private Harmony _harmony;

  public Replay() : base(Main.Instance, nameof(Replay), true, null, typeof(ReplaySetting))
  {
    Instance = this;
    Settings = (ReplaySetting)Setting;
    Session = new ReplaySession();
  }

  protected override void OnEnable()
  {
    PatchReplayInput();
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
  }

  protected override void OnDisable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    UnpatchReplayInput();
    Session.Stop("feature disabled");
  }

  protected override void OnGUI()
  {
    if (Settings == null)
    {
      UnityEngine.GUILayout.Label("Replay setting is not ready.");
      return;
    }

    Main.SettingGUI.AddSettingToggle(ref Settings.MirrorInputToOS, "Mirror replay input to OS");
    Main.SettingGUI.AddSettingToggle(ref Settings.DebugLogging, "Replay debug logging");
  }

  private void PatchReplayInput()
  {
    if (_harmony != null) return;

    _harmony = new Harmony("TUFReplay.Replay");
    _harmony.Patch(
      AccessTools.Method(typeof(RDInput), nameof(RDInput.GetMain)),
      prefix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.GetMainPrefix)),
      postfix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.GetMainPostfix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(RDInput), nameof(RDInput.GetStateKeys)),
      prefix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.GetStateKeysPrefix)),
      postfix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.GetStateKeysPostfix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) }),
      postfix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.OnChangeStatePostfix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(LevelPrefabScript), nameof(LevelPrefabScript.TryToLoadLevel)),
      prefix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.LevelPrefabTryToLoadLevelPrefix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(scrController), nameof(scrController.UpdateInput)),
      prefix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.ControllerUpdateInputPrefix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(scrPlayer), nameof(scrPlayer.ValidInputWasTriggered)),
      postfix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.ValidInputWasTriggeredPostfix))
    );
    _harmony.Patch(
      AccessTools.Method(typeof(scrPlayer), nameof(scrPlayer.CountValidKeysPressed)),
      prefix: new HarmonyMethod(typeof(ReplayPatches), nameof(ReplayPatches.CountValidKeysPressedPrefix))
    );

    LogDebug("Manual Harmony replay input patches installed.");
  }

  private void UnpatchReplayInput()
  {
    if (_harmony == null) return;

    _harmony.UnpatchAll("TUFReplay.Replay");
    _harmony = null;
    LogDebug("Manual Harmony replay input patches removed.");
  }

  private static void OnPlayButtonPressed(object sender, PlayButtonEventArgs e)
  {
    PlayRecord record = ReplayService.GetActiveRecord();
    int? currentLevelId = TUFHelperAPI.GetLevelID(e);

    LogDebug(
      "Play button pressed. currentTufLevelId=" + FormatNullable(currentLevelId) +
      ", activeRecordId=" + (record?.Id ?? "none") +
      ", activeRecordTufLevelId=" + (record == null ? "none" : record.TufLevelId.ToString()) +
      ", sessionPlaying=" + (Instance?.Session.IsPlaying == true)
    );

    if (record == null)
    {
      Instance?.Session.Stop("play button pressed without active replay record");
      return;
    }

    if (!currentLevelId.HasValue)
    {
      LogDebug("Replay ignored: play button level is not from TUFHelper.");
      ReplayService.StopActiveReplay("play button without TUFHelper level");
      return;
    }

    if (currentLevelId.Value != record.TufLevelId)
    {
      LogDebug(
        "Replay ignored: current level id mismatch. current=" +
        currentLevelId.Value +
        ", record=" +
        record.TufLevelId
      );
      ReplayService.StopActiveReplay("current level does not match active replay record");
      return;
    }

    Instance?.Session.Start(record);
  }

  public static void LogDebug(string message)
  {
    if (Settings != null && !Settings.DebugLogging) return;
    Main.Instance?.Log("[Replay] " + message);
  }

  private static string FormatNullable(int? value)
  {
    return value.HasValue ? value.Value.ToString() : "none";
  }
}
