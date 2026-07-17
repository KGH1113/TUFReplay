using System;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Recording;
using TUFReplay.Application.Replay;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
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
  private byte[] _gameplayHash;
  private int? _gameplayHashVersion;
  private bool _microphoneCaptureStarted;

  private readonly RecordingActivityTracker _activity = new RecordingActivityTracker();

  public RecordingFeature()
  {
    Instance = this;
    Settings = Main.Settings;
    Session = new RecordingSession();
  }

  public void OnClearReached()
  {
    if (!Session.IsRecording || _clearReached)
      return;
    _clearReached = true;
    Session.MarkWonReached();
    CapturedMicrophoneRecording recording = EndMicrophoneRun();
    if (_currentRun != null && RecordingSession.GetLevelTileCount() > _currentRun.StartTile)
      FeatureRegistry.MicrophoneRecording?.Present(recording);
    else
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
    Main.Instance.Log("[Recording] Clear reached; input capture continues until editor return.");
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording || _runSaved)
      return;

    _failed = true;
    Session.MarkTerminal();
    Session.StopInputCapture("failed");
    CapturedMicrophoneRecording recording = EndMicrophoneRun();
    if (SaveActivityRun("failed", Session.GetLastReachedTile()))
      FeatureRegistry.MicrophoneRecording?.Present(recording);
    else
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
    Main.Instance.Log("[Recording] Run failed.");
  }

  public void OnReturnedToEditor()
  {
    if (!Session.IsRecording)
      return;

    Session.MarkTerminal();
    Session.StopInputCapture("editor");
    CapturedMicrophoneRecording recording = EndMicrophoneRun();
    if (!_runSaved)
    {
      bool saved = SaveActivityRun(
        _clearReached ? "cleared" : "aborted",
        _clearReached ? RecordingSession.GetLevelTileCount() : Session.GetLastReachedTile()
      );
      if (!_clearReached)
      {
        if (saved)
          FeatureRegistry.MicrophoneRecording?.Present(recording);
        else
          FeatureRegistry.MicrophoneRecording?.Discard(recording);
      }
      else
        FeatureRegistry.MicrophoneRecording?.Discard(recording);
    }
    else
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
    StopSession();

    if (_clearReached && !_failed && Session.HasRecordableData)
    {
      Main.Instance.Log(
        "[Recording] Clear data captured. inputs=" + Session.InputCount + ", hitContexts=" + Session.HitContextCount
      );
    }
  }

  public void Enable()
  {
    if (Active)
      return;
    Active = true;

    RecordInputTracker.Reset();
    _activity.StartAppSession();
  }

  public void Disable()
  {
    if (!Active)
      return;
    Active = false;

    StopSession();
    RecordInputTracker.Reset();
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
    ReplaySessionService.ClearActiveContextIfLevelChanged(levelPath);
    if (ReplaySessionService.IsActiveReplayLevel(levelPath))
      return;

    if (!RecordingGuard.CanRecord(out string reason))
    {
      StopSession();
      Main.Instance.Log("[Recording] Skipped. reason=" + reason);
      return;
    }

    ResetRunState();

    int levelTileCount = RecordingSession.GetLevelTileCount();
    _activity.OpenLevel(levelPath, tufLevelId, levelTileCount);
    RecordingPatches.ResetHitContextState();
    CaptureGameplayHash();
    Session.Start(tufLevelId, Settings == null || Settings.AutoRecord, _gameplayHash, _gameplayHashVersion);
    if (Session.IsRecording)
      FeatureRegistry.MicrophoneRecording?.ArmForLevel();
    Main.Instance.Log("[Recording] Custom level opened. tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
  }

  public bool PrepareRunForInputCapture()
  {
    if (!Session.IsRecording)
      return false;
    if (!_runSaved)
      return true;

    int? tufLevelId = Session.TufLevelId;
    ResetRunState();
    RecordingPatches.ResetHitContextState();
    Session.Start(tufLevelId, Settings == null || Settings.AutoRecord, _gameplayHash, _gameplayHashVersion);

    Main.Instance.Log("[Recording] Prepared retry run. tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
    return Session.IsRecording;
  }

  private static string CanonicalLevelPath()
  {
    return LevelPathIdentity.Current();
  }

  private void CaptureGameplayHash()
  {
    _gameplayHash = null;
    _gameplayHashVersion = null;
    if (GameplayChartHash.TryComputeCurrent(out byte[] hash, out string error))
    {
      _gameplayHash = hash;
      _gameplayHashVersion = GameplayChartHash.Version;
      return;
    }

    Main.Instance.Log("[Recording] Gameplay hash unavailable. reason=" + error);
  }

  public void StopSession()
  {
    if (Session.IsRecording && _clearReached && !_runSaved)
    {
      Session.MarkTerminal();
      Session.StopInputCapture("session_stop_after_clear");
      SaveActivityRun("cleared", RecordingSession.GetLevelTileCount());
    }

    FeatureRegistry.MicrophoneRecording?.Discard(EndMicrophoneRun());
    FeatureRegistry.MicrophoneRecording?.Disarm();
    Session.Stop();
    RecordingPatches.ResetHitContextState();
  }

  public void OnGameplayStarted()
  {
    Session.MarkGameplayStarted();
    PrepareActivityRun(RecordingSession.GetLevelTileCount());
    if (_currentRun != null && !_microphoneCaptureStarted)
    {
      FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id);
      _microphoneCaptureStarted = true;
    }
  }

  private void PrepareActivityRun(int levelTileCount)
  {
    if (!Session.IsRecording)
      return;
    if (_currentRun != null)
      return;

    int startTile = RecordingSession.GetCurrentTile();
    _currentRun = _activity.CreateRunDraft(Session.Data, startTile, levelTileCount);
  }

  private bool SaveActivityRun(string result, int? lastTile)
  {
    if (_runSaved)
      return false;
    if (_currentRun == null)
      return false;

    RunRecord run = Session.CompleteRunRecord(_currentRun, lastTile, result);
    _runSaved = true;

    if (!run.LastTile.HasValue || run.LastTile.Value <= run.StartTile)
    {
      Main.Instance.Log(
        "[Recording] Skipped activity run without forward progress. result="
          + result
          + ", startTile="
          + run.StartTile
          + ", lastTile="
          + (run.LastTile.HasValue ? run.LastTile.Value.ToString() : "null")
      );
      return false;
    }

    _activity.SaveRun(run);
    FeatureRegistry.MicrophoneRecording?.NotifyRunPersisted(run.Id);

    Main.Instance.Log(
      "[Recording] Saved activity run. result="
        + result
        + ", startTile="
        + run.StartTile
        + ", lastTile="
        + (run.LastTile.HasValue ? run.LastTile.Value.ToString() : "null")
        + ", inputs="
        + run.InputCount
        + ", hitContexts="
        + run.HitContextCount
    );
    return true;
  }

  private void ResetRunState()
  {
    _clearReached = false;
    _failed = false;
    _runSaved = false;
    _currentRun = null;
    _microphoneCaptureStarted = false;
  }

  private CapturedMicrophoneRecording EndMicrophoneRun()
  {
    if (!_microphoneCaptureStarted)
      return null;
    _microphoneCaptureStarted = false;
    return FeatureRegistry.MicrophoneRecording?.EndRun();
  }
}
