using JALib.Core;
using TUFHelper.ModScripts.Json;
using TUFHelper.Utils;
using TUFReplay.Replay;
using TUFReplay.Shared;
using UnityEngine;

namespace TUFReplay.Recording;

public class Recording : Feature
{
  public static Recording Instance;
  public static RecordingSetting Settings;

  public RecordingSession Session { get; private set; }

  private bool _clearReached;
  private bool _failed;

  public Recording() : base(Main.Instance, nameof(Recording), true, typeof(RecordingPatches), typeof(RecordingSetting))
  {
    Instance = this;
    Settings = (RecordingSetting)Setting;
    Session = new RecordingSession();
  }

  public void OnClearReached()
  {
    if (!Session.IsRecording) return;
    _clearReached = true;
    Main.Instance.Log("[Recording] Clear reached");
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording) return;

    _failed = true;
    Session.StopInputCapture("failed");
    Main.Instance.Log("[Recording] Run failed.");
  }

  public void OnReturnedToEditor()
  {
    if (!Session.IsRecording) return;

    Session.StopInputCapture("editor");
    Session.Stop();

    if (!_clearReached || _failed) return;
    if (!Session.HasRecordableData) return;

    PlayRecordRepository.Save(Session.ToPlayRecord());
    Main.Instance.Log("[Recording] Saved clear. inputs=" + Session.InputCount + ", hitContexts=" + Session.HitContextCount);
  }

  protected override void OnEnable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
    RecordInputTracker.Reset();
  }

  protected override void OnDisable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    RecordInputTracker.Reset();
    Session.Stop();
  }

  protected override void OnGUI()
  {
    if (Settings == null)
    {
      GUILayout.Label("Recording setting is not ready.");
      return;
    }

    Main.SettingGUI.AddSettingToggle(ref Settings.AutoRecord, "Auto record TUF levels");

    string levelId = Session.LevelId.HasValue ? Session.LevelId.Value.ToString() : "none";
    GUILayout.Label("TUF level id: " + levelId);
    GUILayout.Label("Recording: " + (Session.IsRecording ? "on" : "off"));
    GUILayout.Label("Input capture: " + (Session.IsCapturingInput ? "on" : "off"));
    GUILayout.Label("Inputs: " + Session.InputCount);
    GUILayout.Label("Hit contexts: " + Session.HitContextCount);
  }

  private static void OnPlayButtonPressed(object sender, PlayButtonEventArgs e)
  {
    LevelListInfoElementJson levelInfo = TUFHelperAPI.GetLevelInfo(e);
    int? levelId = levelInfo?.ID;
    if (!levelId.HasValue)
    {
      Instance?.Session.Stop();
      return;
    }

    if (ReplayService.IsActiveReplayLevel(levelId.Value)) return;

    if (!RecordingGuard.CanRecord(out string reason))
    {
      Instance?.Session.Stop();
      Main.Instance.Log("[Recording] Skipped. tufLevelId=" + levelId.Value + ", reason=" + reason);
      return;
    }

    Instance._clearReached = false;
    Instance._failed = false;
    Instance?.Session.Start(levelId.Value, levelInfo);
    Main.Instance.Log("TUF level opened: " + levelId.Value);
  }
}
