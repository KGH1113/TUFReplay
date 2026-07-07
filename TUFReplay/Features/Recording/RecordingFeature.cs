using JALib.Core;
using TUFHelper.ModScripts.Json;
using TUFHelper.Utils;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Recording;
using TUFReplay.Application.Replay;
using TUFReplay.Features.Gameplay;
using TUFReplay.Features.Replay;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Features.Recording;

public class RecordingFeature : Feature
{
  public static RecordingFeature Instance;
  public static RecordingSetting Settings;

  public RecordingSession Session { get; private set; }

  private bool _clearReached;
  private bool _failed;
  private bool _runSaved;
  private RunRecord _currentRun;

  private readonly RecordingActivityTracker _activity = new RecordingActivityTracker();

  public RecordingFeature() : base(Main.Instance, nameof(RecordingFeature), true, typeof(RecordingPatches), typeof(RecordingSetting))
  {
    Instance = this;
    Settings = (RecordingSetting)Setting;
    Session = new RecordingSession();
    AddMultiFeatures(typeof(GameplayPatches));
  }

  public void OnClearReached()
  {
    if (!Session.IsRecording) return;
    _clearReached = true;
    Session.StopInputCapture("cleared");
    SaveActivityRun("cleared", RecordingSession.GetLevelTileCount());
    Main.Instance.Log("[Recording] Clear reached");
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording) return;

    _failed = true;
    Session.StopInputCapture("failed");
    SaveActivityRun("failed", Session.GetLastReachedTile());
    Main.Instance.Log("[Recording] Run failed.");
  }

  public void OnReturnedToEditor()
  {
    if (!Session.IsRecording) return;

    Session.StopInputCapture("editor");
    if (!_runSaved) SaveActivityRun("aborted", Session.GetLastReachedTile());
    StopSession();

    if (_clearReached && !_failed && Session.HasRecordableData)
    {
      Main.Instance.Log("[Recording] Clear data captured. inputs=" + Session.InputCount + ", hitContexts=" + Session.HitContextCount);
    }
  }

  protected override void OnEnable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    ADOFAIGameplayHandler.Editor_PlayButtonPressed += OnPlayButtonPressed;
    RecordInputTracker.Reset();
    _activity.StartAppSession();
  }

  protected override void OnDisable()
  {
    ADOFAIGameplayHandler.Editor_PlayButtonPressed -= OnPlayButtonPressed;
    RecordInputTracker.Reset();
    StopSession();
    _activity.CloseLevel();
    _activity.StopAppSession();
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
    LevelListInfoElementJson levelInfo = TufHelperGateway.GetLevelInfo(e);
    int? levelId = levelInfo?.ID;
    if (!levelId.HasValue)
    {
      Instance?.StopSession();
      Instance?._activity.CloseLevel();
      return;
    }

    if (ReplaySessionService.IsActiveReplayLevel(levelId.Value)) return;

    if (!RecordingGuard.CanRecord(out string reason))
    {
      Instance?.StopSession();
      Main.Instance.Log("[Recording] Skipped. tufLevelId=" + levelId.Value + ", reason=" + reason);
      return;
    }

    Instance._clearReached = false;
    Instance._failed = false;
    Instance._runSaved = false;
    Instance._currentRun = null;

    int levelTileCount = RecordingSession.GetLevelTileCount();
    Instance._activity.OpenLevel(levelId.Value, levelTileCount);
    RecordingPatches.ResetHitContextState();
    Instance.Session.Start(levelId.Value, levelInfo, Settings == null || Settings.AutoRecord);
    Main.Instance.Log("TUF level opened: " + levelId.Value);
  }

  public void StopSession()
  {
    Session.Stop();
    RecordingPatches.ResetHitContextState();
  }

  public void OnGameplayStarted()
  {
    Session.MarkGameplayStarted();
    PrepareActivityRun(RecordingSession.GetLevelTileCount());
  }

  private void PrepareActivityRun(int levelTileCount)
  {
    if (!Session.IsRecording) return;
    if (_currentRun != null) return;

    int startTile = RecordingSession.GetCurrentTile();
    _currentRun = _activity.CreateRunDraft(Session.Data, startTile, levelTileCount);
  }

  private void SaveActivityRun(string result, int? lastTile)
  {
    if (_runSaved) return;
    if (_currentRun == null) return;

    RunRecord run = Session.CompleteRunRecord(_currentRun, lastTile, result);
    _activity.SaveRun(run);
    _runSaved = true;

    Main.Instance.Log(
      "[Recording] Saved activity run. result=" + result +
      ", startTile=" + run.StartTile +
      ", lastTile=" + (run.LastTile.HasValue ? run.LastTile.Value.ToString() : "null") +
      ", inputs=" + run.InputCount +
      ", hitContexts=" + run.HitContextCount
    );
  }
}
