using System;
using System.IO;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Recording;
using TUFReplay.Application.Replay;
using TUFReplay.Features.Replay;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Features.Recording;

public class RecordingFeature
{
  public static RecordingFeature Instance;
  public static TUFReplaySetting Settings;

  public RecordingSession Session { get; private set; }
  public bool Active { get; private set; }

  private bool _clearReached;
  private bool _failed;
  private bool _runSaved;
  private RunRecord _currentRun;

  private readonly RecordingActivityTracker _activity = new RecordingActivityTracker();

  public RecordingFeature()
  {
    Instance = this;
    Settings = Main.Settings;
    Session = new RecordingSession();
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

  public void Enable()
  {
    if (Active) return;
    Active = true;

    RecordInputTracker.Reset();
    _activity.StartAppSession();
  }

  public void Disable()
  {
    if (!Active) return;
    Active = false;

    RecordInputTracker.Reset();
    StopSession();
    _activity.CloseLevel();
    _activity.StopAppSession();
  }

  public void OnEditorPlay()
  {
    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
    {
      StopSession();
      _activity.CloseLevel();
      return;
    }

    int? tufLevelId = TufHelperGateway.ResolveTufLevelId(levelPath);
    ReplaySessionService.ClearActiveContextIfLevelChanged(tufLevelId);
    if (tufLevelId.HasValue && ReplaySessionService.IsActiveReplayLevel(tufLevelId.Value)) return;

    if (!RecordingGuard.CanRecord(out string reason))
    {
      StopSession();
      Main.Instance.Log("[Recording] Skipped. reason=" + reason);
      return;
    }

    _clearReached = false;
    _failed = false;
    _runSaved = false;
    _currentRun = null;

    int levelTileCount = RecordingSession.GetLevelTileCount();
    _activity.OpenLevel(levelPath, tufLevelId, levelTileCount);
    RecordingPatches.ResetHitContextState();
    Session.Start(tufLevelId, Settings == null || Settings.AutoRecord);
    Main.Instance.Log("[Recording] Custom level opened. tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
  }

  private static string CanonicalLevelPath()
  {
    try
    {
      string path = ADOBase.levelPath;
      if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".adofai", StringComparison.OrdinalIgnoreCase)) return null;
      path = Path.GetFullPath(path);
      return File.Exists(path) ? path : null;
    }
    catch
    {
      return null;
    }
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
