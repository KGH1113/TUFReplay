using System;
using System.Threading.Tasks;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Microphone.Recording;
using TUFReplay.Microphone.Timing;
using TUFReplay.ModApi.Recording;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Microphone;
using TUFReplay.Recording.Patches;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
{
  public event System.Action GameplayStateChanged;

  public void NotifyGameplayStateChanged() => GameplayStateChanged?.Invoke();

  public static RecordingFeature Instance;
  public static TUFReplaySetting Settings;

  public RecordingSession Session { get; private set; }
  public bool Active { get; private set; }

  private bool _clearReached;

  // Like a clear, a failed run keeps recording through the fail screen and is saved when it ends.
  private bool _failed;
  private bool _runSaved;
  private RunRecord _currentRun;
  private Task<bool> _runPersistence = Task.FromResult(false);
  internal Task<bool> RunPersistence => _runPersistence;
  internal Task PendingActivityWrites => _activity.PendingWrites;
  private byte[] _gameplayHash;
  private int? _gameplayHashVersion;
  private bool _microphoneCaptureStarted;
  private MicrophoneCaptureTimeline _microphoneTimeline;
  private PendingMicrophoneDisposition _pendingEditorRecording;
  private bool _calibrationRun;
  private RunStateCapture _runStates;

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
    ObserveWebcamWon();
    ObserveMicrophoneWon();
    if (_calibrationRun)
      return;
    Main.Instance.Log("[Recording] Clear reached; input and microphone capture continue until editor return.");
  }

  public void OnHitPostfixCompleted()
  {
    if (!_calibrationRun || !_clearReached || _runSaved || !Session.IsRecording)
      return;

    // Won is raised from inside scrPlayer.Hit. Complete the transient run only after
    // that method's postfix has stored the winning hit's resolved judgment.
    Session.MarkTerminal();
    Session.StopInputCapture("calibration_cleared");
    RunRecord calibrationRun = CompleteCalibrationRun("cleared", RecordingSession.GetLevelTileCount());
    EndMicrophoneRun(recording =>
      UnityMainThread.Post(() => FeatureRegistry.MicrophoneCalibration?.OnRunCleared(calibrationRun, recording))
    );
    Main.Instance.Log("[Recording] Calibration clear captured without activity persistence.");
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording || _runSaved || _failed)
      return;

    _failed = true;
    if (_calibrationRun)
    {
      Session.MarkTerminal();
      Session.StopInputCapture("failed");
      _runSaved = true;
      EndMicrophoneRun(recording =>
        UnityMainThread.Post(() =>
          FeatureRegistry.MicrophoneCalibration?.OnRunDiscarded(recording, "Calibration failed. Try again.")
        )
      );
      Main.Instance.Log("[Recording] Calibration attempt failed; waiting for retry.");
      return;
    }

    Session.MarkFailed();
    ObserveWebcamFailure();
    ObserveMicrophoneFailure();
    Main.Instance.Log(
      "[Recording] Run failed; input, camera and microphone capture continue until editor return or retry."
    );
  }

  // Saves a failed run with what it recorded on the fail screen, then ends its camera and
  // microphone. Editor return, a retry and a session stop end the fail screen.
  internal void FinishFailedRun()
  {
    if (!_failed || _runSaved || !Session.IsRecording)
      return;

    Session.MarkTerminal();
    Session.StopInputCapture("failed");
    bool saved = SaveActivityRun("failed", Session.GetLastReachedTile());
    EndMicrophoneRun(recording => RecordingMicrophoneDisposition.Complete(recording, saved), persistWebcam: saved);
  }

  public void OnReturnedToEditor()
  {
    if (!Session.IsRecording)
      return;

    FinishFailedRun();
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
        EndMicrophoneRun(recording => RecordingMicrophoneDisposition.Complete(recording, saved), persistWebcam: saved);
      }
      else if (saved)
        QueueEditorRecording();
      else
        EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
    }
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
    var webcam = _pendingEditorWebcamRecording;
    _pendingEditorWebcamRecording = null;
    webcam?.CompleteDisposition(persist: true);
    NotifyGameplayStateChanged();
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
    RecordInputTracker.PrepareSource();
  }

  public void Disable()
  {
    if (!Active)
      return;
    Active = false;

    StopSession();
    DiscardPendingEditorRecording();
    RecordInputTracker.Shutdown();
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
    if (Session.IsRecording)
    {
      FeatureRegistry.MicrophoneRecording?.ArmForLevel();
      FeatureRegistry.WebcamRecording?.ArmForLevel();
    }
    Main.Instance.Log("[Recording] " + logMessage + ". tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
  }

  public bool PrepareRunForInputCapture()
  {
    if (ReplaySessionService.HasActiveContext)
      return false;
    if (!Session.IsRecording)
      return false;
    // A transient fail may have ended capture before an activity draft existed.
    // Reusing that payload would retain its terminal boundary and clamp the new
    // countdown's inputs to it, even though the later gameplay keeps recording.
    if (!_runSaved && !_failed && !Session.Data.TerminalTimeUs.HasValue)
      return true;

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
    if (_calibrationRun)
    {
      EndMicrophoneRun(recording => FeatureRegistry.MicrophoneRecording?.Discard(recording));
      Session.Stop();
      RecordingPatches.ResetHitContextState();
      _calibrationRun = false;
      return;
    }
    FinishFailedRun();
    bool persistWebcam = false;
    if (Session.IsRecording && _clearReached && !_runSaved)
    {
      Session.MarkTerminal();
      Session.StopInputCapture("session_stop_after_clear");
      persistWebcam = SaveActivityRun("cleared", RecordingSession.GetLevelTileCount());
    }

    EndMicrophoneRun(
      recording => FeatureRegistry.MicrophoneRecording?.Discard(recording),
      persistWebcam: persistWebcam
    );
    FeatureRegistry.MicrophoneRecording?.Disarm();
    Session.Stop();
    RecordingPatches.ResetHitContextState();
  }

  public void OnGameplayStarted()
  {
    if (ReplaySessionService.HasActiveContext)
      return;

    Session.MarkGameplayStarted(RecordingClock.IsTimelineAdvancing());
    if (!PrepareActivityRun(RecordingSession.GetLevelTileCount()))
      return;
    // If activity creation was busy during countdown, recover using the
    // original input boundary rather than starting from gameplay time.
    StartWebcamRun();
    StartMicrophoneRun();
    if (_calibrationRun)
      FeatureRegistry.MicrophoneCalibration?.OnRunStarted();
  }

  public void OnInputCaptureStarted()
  {
    if (_calibrationRun || !Session.IsRecording)
      return;

    if (!PrepareActivityRun(RecordingSession.GetLevelTileCount()))
      return;
    StartWebcamRun();
    StartMicrophoneRun();
  }

  private void ResetRunState()
  {
    // A retry can start directly from the fail screen without an editor return.
    // Save the failed run before resetting IDs.
    FinishFailedRun();
    DiscardPendingEditorRecording();
    _clearReached = false;
    _failed = false;
    _runSaved = false;
    _currentRun = null;
    DiscardRunStates();
    _runPersistence = Task.FromResult(false);
    _microphoneCaptureStarted = false;
    _microphoneTimeline = null;
    _webcamCaptureStarted = false;
    _webcamTimeline = null;
  }
}
