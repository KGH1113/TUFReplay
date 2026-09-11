using System;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Microphone.Recording;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Microphone;
using TUFReplay.Recording.Patches;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
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
  private double? _microphoneCaptureStartedAt;
  private long _microphoneTimelineCorrectionUs;
  private bool _microphoneTimelineAnchored;
  private PendingMicrophoneDisposition _pendingEditorRecording;
  private bool _calibrationRun;

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
    if (_calibrationRun)
    {
      Session.MarkTerminal();
      Session.StopInputCapture("calibration_cleared");
      RunRecord calibrationRun = CompleteCalibrationRun("cleared", RecordingSession.GetLevelTileCount());
      EndMicrophoneRun(recording =>
        UnityMainThread.Post(() => FeatureRegistry.MicrophoneCalibration?.OnRunCleared(calibrationRun, recording))
      );
      Main.Instance.Log("[Recording] Calibration clear captured without activity persistence.");
      return;
    }
    Main.Instance.Log("[Recording] Clear reached; input and microphone capture continue until editor return.");
    FeatureRegistry.Submission?.Clear(Session);
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording || _runSaved)
      return;

    _failed = true;
    FeatureRegistry.Submission?.Abort(Session, "run_failed");
    Session.MarkTerminal();
    Session.StopInputCapture("failed");
    if (_calibrationRun)
    {
      _runSaved = true;
      EndMicrophoneRun(recording =>
        UnityMainThread.Post(() =>
          FeatureRegistry.MicrophoneCalibration?.OnRunDiscarded(recording, "Calibration failed. Try again.")
        )
      );
      Main.Instance.Log("[Recording] Calibration attempt failed; waiting for retry.");
      return;
    }
    bool saved = SaveActivityRun("failed", Session.GetLastReachedTile());
    EndMicrophoneRun(recording => RecordingMicrophoneDisposition.Complete(recording, saved));
    Main.Instance.Log("[Recording] Run failed.");
  }

  public void OnReturnedToEditor()
  {
    if (!Session.IsRecording)
      return;

    Session.MarkTerminal();
    Session.StopInputCapture("editor");
    if (_calibrationRun)
    {
      if (!_clearReached)
        EndMicrophoneRun(recording =>
          UnityMainThread.Post(() =>
            FeatureRegistry.MicrophoneCalibration?.OnRunDiscarded(recording, "Calibration stopped. Try again.")
          )
        );
      else
        EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
      StopSession();
      return;
    }
    if (!_runSaved)
    {
      bool saved = SaveActivityRun(
        _clearReached ? "cleared" : "aborted",
        _clearReached ? RecordingSession.GetLevelTileCount() : Session.GetLastReachedTile()
      );
      if (!_clearReached)
      {
        EndMicrophoneRun(recording => RecordingMicrophoneDisposition.Complete(recording, saved));
      }
      else if (saved)
        QueueEditorRecording();
      else
        EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
    }
    else
      EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
    StopSession();

    if (_clearReached && !_failed && Session.HasRecordableData)
    {
      Main.Instance.Log(
        "[Recording] Clear data captured. inputs=" + Session.InputCount + ", hitContexts=" + Session.HitContextCount
      );
    }
  }

  public void OnEditorReturnCompleted()
  {
    PendingMicrophoneDisposition recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    recording?.CompleteDisposition(persist: true);
  }

  public void OnEditorReturnFailed()
  {
    DiscardPendingEditorRecording();
  }

  public void Enable()
  {
    if (Active)
      return;
    Active = true;

    RecordInputTracker.Reset();
  }

  public void Disable()
  {
    if (!Active)
      return;
    Active = false;

    StopSession();
    DiscardPendingEditorRecording();
    RecordInputTracker.Reset();
    _activity.CloseLevel();
    _activity.StopAppSession();
  }

  public void OnEditorPlay()
  {
    if (ReplaySessionService.HasActiveContext)
    {
      StopSession();
      Main.Instance.Log("[Recording] Skipped replay playback run.");
      return;
    }

    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
    {
      StopSession();
      _activity.CloseLevel();
      return;
    }

    if (FeatureRegistry.MicrophoneCalibration?.IsCalibrationLevel() == true)
    {
      ResetRunState();
      _calibrationRun = true;
      RecordingPatches.ResetHitContextState();
      CaptureGameplayHash();
      Session.Start(null, true, _gameplayHash, _gameplayHashVersion);
      Main.Instance.Log("[Recording] Calibration level opened in transient recording mode.");
      return;
    }

    PrepareCurrentLevelRecording("Custom level opened");
  }

  public void OnReplayEndedInPlayMode()
  {
    if (!Active || Session.IsRecording || ReplaySessionService.HasActiveContext)
      return;
    if (scnEditor.instance == null || !scnEditor.instance.playMode)
      return;
    if (FeatureRegistry.MicrophoneCalibration?.IsCalibrationLevel() == true)
      return;

    PrepareCurrentLevelRecording("Replay ended; recording rearmed for the next run");
  }

  private void PrepareCurrentLevelRecording(string logMessage)
  {
    _calibrationRun = false;

    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
    {
      StopSession();
      _activity.CloseLevel();
      return;
    }

    int? tufLevelId = TufHelperGateway.ResolveTufLevelId(levelPath);
    ReplaySessionService.ClearActiveContextIfLevelChanged();
    if (ReplaySessionService.IsActiveReplayLevel())
      return;

    if (!RecordingGuard.CanRecord(out string reason))
    {
      StopSession();
      Main.Instance.Log("[Recording] Skipped. reason=" + reason);
      return;
    }

    ResetRunState();

    CaptureGameplayHash();
    RecordingPatches.ResetHitContextState();
    Session.Start(tufLevelId, Settings == null || Settings.AutoRecord, _gameplayHash, _gameplayHashVersion);
    FeatureRegistry.Submission?.SetLevel(levelPath, tufLevelId);
    if (Session.IsRecording)
      FeatureRegistry.MicrophoneRecording?.ArmForLevel();
    Main.Instance.Log("[Recording] " + logMessage + ". tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
  }

  public bool PrepareRunForInputCapture()
  {
    if (ReplaySessionService.HasActiveContext)
      return false;
    if (!Session.IsRecording)
      return false;
    if (!_runSaved)
    {
      FeatureRegistry.Submission?.Begin(Session);
      return true;
    }

    int? tufLevelId = Session.TufLevelId;
    ResetRunState();
    RecordingPatches.ResetHitContextState();
    Session.Start(
      tufLevelId,
      _calibrationRun || Settings == null || Settings.AutoRecord,
      _gameplayHash,
      _gameplayHashVersion
    );

    Main.Instance.Log("[Recording] Prepared retry run. tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
    FeatureRegistry.Submission?.Begin(Session);
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
    FeatureRegistry.Submission?.FinalizeClear(Session);
    if (_calibrationRun)
    {
      EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
      Session.Stop();
      RecordingPatches.ResetHitContextState();
      _calibrationRun = false;
      return;
    }
    if (Session.IsRecording && _clearReached && !_runSaved)
    {
      Session.MarkTerminal();
      Session.StopInputCapture("session_stop_after_clear");
      SaveActivityRun("cleared", RecordingSession.GetLevelTileCount());
    }

    EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
    FeatureRegistry.MicrophoneRecording?.Disarm();
    Session.Stop();
    RecordingPatches.ResetHitContextState();
  }

  public void OnGameplayStarted()
  {
    if (ReplaySessionService.HasActiveContext)
      return;

    Session.MarkGameplayStarted();
    if (!PrepareActivityRun(RecordingSession.GetLevelTileCount()))
      return;
    StartMicrophoneRun();
    if (_calibrationRun)
      FeatureRegistry.MicrophoneCalibration?.OnRunStarted();
    TryAnchorMicrophoneTimeline();
  }

  public void OnInputCaptureStarted()
  {
    if (_calibrationRun || !Session.IsRecording)
      return;

    if (!PrepareActivityRun(RecordingSession.GetLevelTileCount()))
      return;
    StartMicrophoneRun();
  }

  private void ResetRunState()
  {
    DiscardPendingEditorRecording();
    _clearReached = false;
    _failed = false;
    _runSaved = false;
    _currentRun = null;
    _microphoneCaptureStarted = false;
    _microphoneCaptureStartedAt = null;
    _microphoneTimelineCorrectionUs = 0L;
    _microphoneTimelineAnchored = false;
  }
}
