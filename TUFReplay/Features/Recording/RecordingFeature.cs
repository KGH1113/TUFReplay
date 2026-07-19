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
  private double? _microphoneCaptureStartedAt;
  private long _microphonePrerollUs;
  private bool _microphoneGameplayStartAnchored;
  private CapturedMicrophoneRecording _pendingEditorRecording;
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
      CapturedMicrophoneRecording calibrationRecording = EndMicrophoneRun();
      RunRecord calibrationRun = CompleteCalibrationRun("cleared", RecordingSession.GetLevelTileCount());
      FeatureRegistry.MicrophoneCalibration?.OnRunCleared(calibrationRun, calibrationRecording);
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
    CapturedMicrophoneRecording recording = EndMicrophoneRun();
    if (_calibrationRun)
    {
      _runSaved = true;
      FeatureRegistry.MicrophoneCalibration?.OnRunDiscarded(recording, "Calibration failed. Try again.");
      Main.Instance.Log("[Recording] Calibration attempt failed; waiting for retry.");
      return;
    }
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
    if (_calibrationRun)
    {
      if (!_clearReached)
        FeatureRegistry.MicrophoneCalibration?.OnRunDiscarded(recording, "Calibration stopped. Try again.");
      else
        FeatureRegistry.MicrophoneRecording?.Discard(recording);
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
        if (saved)
          FeatureRegistry.MicrophoneRecording?.Present(recording);
        else
          FeatureRegistry.MicrophoneRecording?.Discard(recording);
      }
      else if (saved)
        QueueEditorRecording(recording);
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

  public void OnEditorReturnCompleted()
  {
    CapturedMicrophoneRecording recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    FeatureRegistry.MicrophoneRecording?.Present(recording);
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
    _activity.StartAppSession();
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
    _calibrationRun = false;

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

    int levelTileCount = RecordingSession.GetLevelTileCount();
    CaptureGameplayHash();
    _activity.OpenLevel(levelPath, tufLevelId, levelTileCount, _gameplayHash, _gameplayHashVersion);
    RecordingPatches.ResetHitContextState();
    Session.Start(tufLevelId, Settings == null || Settings.AutoRecord, _gameplayHash, _gameplayHashVersion);
    if (Session.IsRecording)
      FeatureRegistry.MicrophoneRecording?.ArmForLevel();
    Main.Instance.Log("[Recording] Custom level opened. tufLevelId=" + (tufLevelId?.ToString() ?? "null"));
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
      FeatureRegistry.MicrophoneRecording?.Discard(EndMicrophoneRun());
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

    FeatureRegistry.MicrophoneRecording?.Discard(EndMicrophoneRun());
    FeatureRegistry.MicrophoneRecording?.Disarm();
    Session.Stop();
    RecordingPatches.ResetHitContextState();
  }

  public void OnGameplayStarted()
  {
    if (ReplaySessionService.HasActiveContext)
      return;

    Session.MarkGameplayStarted();
    PrepareActivityRun(RecordingSession.GetLevelTileCount());
    if (_calibrationRun)
      FeatureRegistry.MicrophoneCalibration?.OnRunStarted();
    StartMicrophoneRun();
    AnchorMicrophoneToGameplayStart();
  }

  public void OnInputCaptureStarted()
  {
    if (_calibrationRun || !Session.IsRecording)
      return;

    PrepareActivityRun(RecordingSession.GetLevelTileCount());
    StartMicrophoneRun();
  }

  private void PrepareActivityRun(int levelTileCount)
  {
    if (!Session.IsRecording)
      return;
    if (_currentRun != null)
      return;

    int startTile = RecordingSession.GetCurrentTile();
    _currentRun = _calibrationRun
      ? CreateCalibrationRunDraft(Session.Data, startTile, levelTileCount)
      : _activity.CreateRunDraft(Session.Data, startTile, levelTileCount);
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
    _microphonePrerollUs = 0L;
    _microphoneGameplayStartAnchored = false;
  }

  private CapturedMicrophoneRecording EndMicrophoneRun()
  {
    if (!_microphoneCaptureStarted)
      return null;
    _microphoneCaptureStarted = false;
    CapturedMicrophoneRecording recording = FeatureRegistry.MicrophoneRecording?.EndRun();
    if (recording != null && _microphoneGameplayStartAnchored)
      recording.CaptureStartOffsetUs -= _microphonePrerollUs;
    _microphoneCaptureStartedAt = null;
    _microphonePrerollUs = 0L;
    _microphoneGameplayStartAnchored = false;
    return recording;
  }

  private void StartMicrophoneRun()
  {
    if (_currentRun == null || _microphoneCaptureStarted)
      return;

    FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id);
    _microphoneCaptureStarted = true;
    _microphoneCaptureStartedAt = RecordingClock.CurrentUnscaledTime();
  }

  private void AnchorMicrophoneToGameplayStart()
  {
    if (_calibrationRun || !_microphoneCaptureStarted || _microphoneGameplayStartAnchored)
      return;

    double startedAt = _microphoneCaptureStartedAt ?? RecordingClock.CurrentUnscaledTime();
    double elapsedSeconds = Math.Max(0d, RecordingClock.CurrentUnscaledTime() - startedAt);
    _microphonePrerollUs = (long)(elapsedSeconds * 1_000_000d);
    _microphoneGameplayStartAnchored = true;
  }

  private void QueueEditorRecording(CapturedMicrophoneRecording recording)
  {
    DiscardPendingEditorRecording();
    _pendingEditorRecording = recording;
  }

  private void DiscardPendingEditorRecording()
  {
    CapturedMicrophoneRecording recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    FeatureRegistry.MicrophoneRecording?.Discard(recording);
  }
}
