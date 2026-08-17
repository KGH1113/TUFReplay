using System;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Microphone;
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
  private byte[] _legacyGameplayHash;
  private int? _legacyGameplayHashVersion;
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
  }

  public void OnRunFailed()
  {
    if (!Session.IsRecording || _runSaved)
      return;

    _failed = true;
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
    EndMicrophoneRun(recording => CompleteMicrophoneDisposition(recording, saved));
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
        EndMicrophoneRun(recording => CompleteMicrophoneDisposition(recording, saved));
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
    _legacyGameplayHash = null;
    _legacyGameplayHashVersion = null;
    if (GameplayChartHash.TryComputeCurrent(out byte[] hash, out string error))
    {
      _gameplayHash = hash;
      _gameplayHashVersion = GameplayChartHash.Version;
      if (GameplayChartHash.TryComputeCurrent(2, out byte[] legacyHash, out _))
      {
        _legacyGameplayHash = legacyHash;
        _legacyGameplayHashVersion = 2;
      }
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

  private bool PrepareActivityRun(int levelTileCount)
  {
    if (!Session.IsRecording)
      return false;
    if (_currentRun != null)
      return true;

    if (!_calibrationRun && !OpenActivityLevel(levelTileCount))
      return false;

    int startTile = RecordingSession.GetCurrentTile();
    _currentRun = _calibrationRun
      ? CreateCalibrationRunDraft(Session.Data, startTile, levelTileCount)
      : _activity.CreateRunDraft(Session.Data, startTile, levelTileCount);
    return _currentRun != null;
  }

  private bool OpenActivityLevel(int levelTileCount)
  {
    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
      return false;

    if (
      _activity.OpenLevel(
        levelPath,
        Session.TufLevelId,
        levelTileCount,
        _gameplayHash,
        _gameplayHashVersion,
        _legacyGameplayHash,
        _legacyGameplayHashVersion
      )
    )
      return true;

    Main.Instance.Log("[Recording] Activity database is busy; recording will retry when gameplay starts.");
    return false;
  }

  private static RunRecord CreateCalibrationRunDraft(RecordedRunPayload data, int startTile, int levelTileCount)
  {
    return new RunRecord
    {
      Id = Guid.NewGuid().ToString("N"),
      RunIndex = 0,
      SegmentGroupIndex = 0,
      StartedAtUtc = data.StartedAtUtc,
      LevelTileCount = levelTileCount,
      StartTile = startTile,
      NoFailMode = data.NoFailMode,
      GameplayStartSongPosition = data.GameplayStartSongPosition,
      LevelPitchPercent = data.LevelPitchPercent,
      EffectivePitch = data.EffectivePitch,
      GameplayHash = data.GameplayHash == null ? null : (byte[])data.GameplayHash.Clone(),
      GameplayHashVersion = data.GameplayHashVersion,
      MetaJson = data.ToActivityMetaJson(),
    };
  }

  private RunRecord CompleteCalibrationRun(string result, int? lastTile)
  {
    if (_currentRun == null || _runSaved)
      return null;
    _runSaved = true;
    return Session.CompleteRunRecord(_currentRun, lastTile, result);
  }

  private bool SaveActivityRun(string result, int? lastTile)
  {
    if (_runSaved)
      return false;
    if (_currentRun == null)
      return false;

    RunRecord run = Session.CompleteRunRecord(_currentRun, lastTile, result);
    _runSaved = true;

    if (run.InputCount <= 0)
    {
      Main.Instance.Log("[Recording] Skipped activity run without native input. result=" + result);
      return false;
    }

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

  private void EndMicrophoneRun(Action<CapturedMicrophoneRecording> completed)
  {
    if (!_microphoneCaptureStarted)
    {
      completed?.Invoke(null);
      return;
    }
    bool adjustTimeline = _microphoneTimelineAnchored;
    long timelineCorrectionUs = _microphoneTimelineCorrectionUs;
    _microphoneCaptureStarted = false;
    _microphoneCaptureStartedAt = null;
    _microphoneTimelineCorrectionUs = 0L;
    _microphoneTimelineAnchored = false;

    if (FeatureRegistry.MicrophoneRecording == null)
    {
      completed?.Invoke(null);
      return;
    }
    FeatureRegistry.MicrophoneRecording.EndRun(recording =>
    {
      if (recording != null && adjustTimeline)
        recording.CaptureStartOffsetUs += timelineCorrectionUs;
      completed?.Invoke(recording);
    });
  }

  private void StartMicrophoneRun()
  {
    if (_currentRun == null || _microphoneCaptureStarted)
      return;

    FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id);
    _microphoneCaptureStarted = true;
    _microphoneCaptureStartedAt = RecordingClock.CurrentUnscaledTime();
  }

  internal void TryAnchorMicrophoneTimeline()
  {
    if (!_microphoneCaptureStarted || _microphoneTimelineAnchored || !Session.Data.GameplayStartSongPosition.HasValue)
      return;

    scrConductor conductor = ADOBase.conductor;
    if (conductor == null || !conductor.enabled || UnityEngine.Time.timeScale <= 0f || UnityEngine.AudioListener.pause)
      return;

    double now = RecordingClock.CurrentUnscaledTime();
    double startedAt = _microphoneCaptureStartedAt ?? now;
    double captureElapsedSeconds = Math.Max(0d, now - startedAt);
    long timelineTimeUs = Math.Max(
      0L,
      RecordingClock.ToRecordTimeUs(RecordingClock.CurrentSongPosition(), Session.Data.GameplayStartSongPosition)
    );
    double gameplayRate = Session.Data.EffectivePitch ?? conductor.song?.pitch ?? 1f;

    _microphoneTimelineCorrectionUs = MicrophoneTimelineAnchor.CalculateCorrectionUs(
      timelineTimeUs,
      gameplayRate,
      captureElapsedSeconds
    );
    _microphoneTimelineAnchored = true;
    Main.Instance.Log(
      "[Recording/Microphone] Timeline anchored. timelineUs="
        + timelineTimeUs
        + ", captureElapsedUs="
        + (long)(captureElapsedSeconds * 1_000_000d)
        + ", rate="
        + gameplayRate
        + ", correctionUs="
        + _microphoneTimelineCorrectionUs
    );
  }

  private static void CompleteMicrophoneDisposition(CapturedMicrophoneRecording recording, bool persist)
  {
    if (persist)
      FeatureRegistry.MicrophoneRecording?.Persist(recording);
    else
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
  }

  private void QueueEditorRecording()
  {
    DiscardPendingEditorRecording();
    var pending = new PendingMicrophoneDisposition(CompleteMicrophoneDisposition);
    _pendingEditorRecording = pending;
    EndMicrophoneRun(pending.CompleteCapture);
  }

  private void DiscardPendingEditorRecording()
  {
    PendingMicrophoneDisposition recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    recording?.CompleteDisposition(persist: false);
  }
}
