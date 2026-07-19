using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Application.Calibration;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Replay;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Settings;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Features.Calibration;

public sealed class MicrophoneCalibrationFeature
{
  private const double LevelOpenTimeoutSeconds = 30d;

  private readonly object _gate = new object();
  private MicrophoneCalibrationStatus _status = new MicrophoneCalibrationStatus();
  private MicrophoneCalibrationResult _result;
  private StoredReplayRun _run;
  private CapturedMicrophoneRecording _recording;
  private CalibrationGameAudioTap _audioTap;
  private MicrophoneCalibrationTicker _ticker;
  private string _levelPath;
  private byte[] _levelGameplayHash;
  private string _previewReplayOperationId;
  private bool _originalRunInBackground;
  private bool _changedRunInBackground;
  private double _levelOpenStartedAt;

  public bool Active =>
    _status.State != MicrophoneCalibrationStates.Idle && _status.State != MicrophoneCalibrationStates.Error;

  public void Enable()
  {
    if (_ticker != null)
      return;
    var gameObject = new GameObject("TUFReplay Microphone Calibration");
    UnityEngine.Object.DontDestroyOnLoad(gameObject);
    _ticker = gameObject.AddComponent<MicrophoneCalibrationTicker>();
    _ticker.Feature = this;
  }

  public void Disable()
  {
    Close(null);
    if (_ticker != null)
      UnityEngine.Object.Destroy(_ticker.gameObject);
    _ticker = null;
  }

  public MicrophoneCalibrationStatus Start()
  {
    Main.Instance?.Log(
      "[Calibration] Start requested. scene="
        + ADOBase.sceneName
        + ", editorPlayMode="
        + (scnEditor.instance?.playMode == true)
    );
    if (IsGameplayActive())
      return Rejected("gameplay_active", "Finish the current gameplay run before starting calibration.");

    CleanupSession();
    string levelPath = Path.Combine(Main.Instance.Path, "Assets", "calibration", "level.adofai");
    string songPath = Path.Combine(Path.GetDirectoryName(levelPath) ?? string.Empty, "calibration_old.ogg");
    if (!File.Exists(levelPath) || !File.Exists(songPath))
      return Error("calibration_assets_missing", "The packaged calibration level or song is missing.");

    string operationId = Guid.NewGuid().ToString("N");
    _levelPath = LevelPathIdentity.Canonicalize(levelPath);
    SetStatus(
      new MicrophoneCalibrationStatus
      {
        OperationId = operationId,
        State = MicrophoneCalibrationStates.Arming,
        Message = "Preparing microphone access.",
        MicrophoneOffsetMs = TUFReplaySettingStore.Current?.MicrophoneOffsetMs ?? 0,
        MicrophoneVolumeDb = TUFReplaySettingStore.Current?.MicrophoneVolumeDb ?? 0,
      }
    );
    if (FeatureRegistry.MicrophoneRecording == null)
      return Error("microphone_unavailable", "The microphone capture feature is unavailable.", operationId);
    if (!FeatureRegistry.MicrophoneRecording.ArmForCalibration(out string error))
      return Error("microphone_arm_failed", error ?? "The microphone could not be armed.", operationId);
    return GetStatus();
  }

  public void Tick()
  {
    string state;
    lock (_gate)
      state = _status.State;
    if (state == MicrophoneCalibrationStates.Arming)
    {
      MicrophoneArmStatus arm = FeatureRegistry.MicrophoneRecording?.GetArmStatus();
      if (arm?.State == MicrophoneArmState.Armed)
        OpenCalibrationLevel();
      else if (arm?.State == MicrophoneArmState.Failed)
        Error("microphone_arm_failed", arm.Error ?? "The microphone could not be armed.", GetStatus().OperationId);
      return;
    }

    if (state == MicrophoneCalibrationStates.OpeningLevel)
    {
      if (IsCalibrationEditorReady())
      {
        Main.Instance?.Log("[Calibration] Calibration level opened automatically.");
        UpdateState(MicrophoneCalibrationStates.WaitingForRun, "Play and clear the calibration level in ADOFAI.");
      }
      else if (Time.realtimeSinceStartupAsDouble - _levelOpenStartedAt > LevelOpenTimeoutSeconds)
        Error(
          "calibration_level_open_timeout",
          "ADOFAI did not finish opening the calibration level.",
          GetStatus().OperationId
        );
      return;
    }

    if (state == MicrophoneCalibrationStates.PreviewStarting || state == MicrophoneCalibrationStates.PreviewPlaying)
      TickPreview();
  }

  public bool IsCalibrationLevel()
  {
    return Active
      && GameplayChartHash.IsSupported(GameplayChartHash.Version, _levelGameplayHash)
      && GameplayChartHash.TryComputeCurrent(out byte[] currentHash, out _)
      && GameplayChartHash.Equals(_levelGameplayHash, currentHash);
  }

  public void OnRunStarted()
  {
    if (!Active)
      return;
    DestroyAudioTap();
    AudioListener listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
    if (listener != null)
    {
      _audioTap = listener.gameObject.AddComponent<CalibrationGameAudioTap>();
      _audioTap.BeginCapture();
    }
    UpdateState(MicrophoneCalibrationStates.Recording, "Recording the calibration run.");
  }

  public void OnRunCleared(RunRecord run, CapturedMicrophoneRecording recording)
  {
    if (!Active)
    {
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
      return;
    }
    if (run == null || recording == null)
    {
      OnRunDiscarded(recording, "The calibration run did not contain microphone audio.");
      return;
    }

    ReplayMetadata metadata = JsonConvert.DeserializeObject<ReplayMetadata>(run.MetaJson ?? "{}");
    double durationMs = Math.Max(1d, (metadata?.terminalTimeUs ?? 0L) / 1000d);
    float[] gameWaveform;
    try
    {
      gameWaveform = _audioTap?.Finish(durationMs) ?? new float[CalibrationWaveformBuilder.BinCount];
    }
    catch (Exception exception)
    {
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
      Error("game_waveform_failed", exception.Message, GetStatus().OperationId);
      return;
    }
    DestroyAudioTap();
    _run = ToStoredReplayRun(run);
    _recording = recording;
    UpdateState(MicrophoneCalibrationStates.Processing, "Building calibration waveforms.");
    string operationId = GetStatus().OperationId;
    ThreadPool.QueueUserWorkItem(_ =>
    {
      try
      {
        float[] microphoneWaveform = CalibrationWaveformBuilder.FromPcm16(recording, durationMs);
        UnityMainThread.Post(() => CompleteResult(operationId, durationMs, gameWaveform, microphoneWaveform));
      }
      catch (Exception exception)
      {
        UnityMainThread.Post(() => ErrorIfCurrent(operationId, "microphone_waveform_failed", exception.Message));
      }
    });
  }

  public void OnRunDiscarded(CapturedMicrophoneRecording recording, string message)
  {
    DestroyAudioTap();
    FeatureRegistry.MicrophoneRecording?.Discard(recording);
    if (Active)
      UpdateState(MicrophoneCalibrationStates.WaitingForRun, message ?? "Try the calibration level again.");
  }

  public MicrophoneCalibrationStatus PlayPreview(string operationId)
  {
    if (!IsCurrent(operationId))
      return StaleOperation();
    if (_run == null || _recording == null || _result == null)
      return Error("calibration_result_unavailable", "Clear the calibration level before playing a test.", operationId);
    StopPreview();
    UpdateState(MicrophoneCalibrationStates.PreviewStarting, "Preparing calibration replay.");
    string copyPath = ReplayMicrophonePlaybackFiles.ForOperation("calibration-" + Guid.NewGuid().ToString("N"));
    CapturedMicrophoneRecording source = _recording;
    ThreadPool.QueueUserWorkItem(_ =>
    {
      try
      {
        File.Copy(source.TempPath, copyPath, true);
        var stored = new StoredMicrophoneRecording
        {
          RunId = source.RunId,
          FilePath = copyPath,
          Format = "wav/pcm16",
          SampleRate = source.SampleRate,
          Channels = source.Channels,
          FrameCount = source.FrameCount,
          DeviceId = source.DeviceId,
          CaptureStartOffsetUs = source.CaptureStartOffsetUs,
          ByteLength = new FileInfo(copyPath).Length,
        };
        UnityMainThread.Post(() => StartPreparedPreview(operationId, stored));
      }
      catch (Exception exception)
      {
        ReplayMicrophonePlaybackFiles.Delete(copyPath);
        UnityMainThread.Post(() =>
          ErrorIfPreviewStarting(operationId, "calibration_preview_prepare_failed", exception.Message)
        );
      }
    });
    return GetStatus();
  }

  public MicrophoneCalibrationStatus StopPreview()
  {
    if (_previewReplayOperationId != null)
      ReplayPlaybackCoordinator.Cancel("Calibration preview stopped.");
    _previewReplayOperationId = null;
    RestoreRunInBackground();
    if (_result != null && Active)
    {
      UpdateState(MicrophoneCalibrationStates.Editing, "Calibration preview stopped.");
      lock (_gate)
        _status.PlaybackPositionMs = 0d;
    }
    return GetStatus();
  }

  public MicrophoneCalibrationStatus SetOffset(string operationId, int offsetMs)
  {
    if (!IsCurrent(operationId))
      return StaleOperation();
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    settings.MicrophoneOffsetMs = Math.Max(
      TUFReplaySetting.MinMicrophoneOffsetMs,
      Math.Min(TUFReplaySetting.MaxMicrophoneOffsetMs, offsetMs)
    );
    TUFReplaySettingStore.Save();
    lock (_gate)
      _status.MicrophoneOffsetMs = settings.MicrophoneOffsetMs;
    ReplaySessionService.UpdateActiveMicrophoneSettings(settings.MicrophoneOffsetMs, settings.MicrophoneVolumeDb);
    return GetStatus();
  }

  public MicrophoneCalibrationStatus SetVolume(string operationId, int volumeDb)
  {
    if (!IsCurrent(operationId))
      return StaleOperation();
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    settings.MicrophoneVolumeDb = Math.Max(
      TUFReplaySetting.MinMicrophoneVolumeDb,
      Math.Min(TUFReplaySetting.MaxMicrophoneVolumeDb, volumeDb)
    );
    TUFReplaySettingStore.Save();
    lock (_gate)
      _status.MicrophoneVolumeDb = settings.MicrophoneVolumeDb;
    ReplaySessionService.UpdateActiveMicrophoneSettings(settings.MicrophoneOffsetMs, settings.MicrophoneVolumeDb);
    return GetStatus();
  }

  public MicrophoneCalibrationStatus Close(string operationId)
  {
    if (operationId != null && !IsCurrent(operationId))
      return StaleOperation();
    CleanupSession();
    SetStatus(new MicrophoneCalibrationStatus { State = MicrophoneCalibrationStates.Idle });
    return GetStatus();
  }

  public MicrophoneCalibrationStatus GetStatus()
  {
    lock (_gate)
      return Clone(_status);
  }

  public MicrophoneCalibrationResult GetResult(string operationId, int revision)
  {
    lock (_gate)
    {
      if (!string.Equals(_status.OperationId, operationId, StringComparison.Ordinal) || _result == null)
        return null;
      if (revision > 0 && revision != _result.Revision)
        return null;
      return new MicrophoneCalibrationResult
      {
        OperationId = _result.OperationId,
        Revision = _result.Revision,
        DurationMs = _result.DurationMs,
        GameWaveform = (float[])_result.GameWaveform.Clone(),
        MicrophoneWaveform = (float[])_result.MicrophoneWaveform.Clone(),
      };
    }
  }

  public bool IsCurrentOperation(string operationId) => IsCurrent(operationId);

  private void CompleteResult(string operationId, double durationMs, float[] gameWaveform, float[] microphoneWaveform)
  {
    if (!IsCurrent(operationId))
      return;
    int revision;
    lock (_gate)
    {
      revision = _status.ResultRevision + 1;
      _result = new MicrophoneCalibrationResult
      {
        OperationId = operationId,
        Revision = revision,
        DurationMs = durationMs,
        GameWaveform = gameWaveform,
        MicrophoneWaveform = microphoneWaveform,
      };
      _status.DurationMs = durationMs;
      _status.PlaybackPositionMs = 0d;
      _status.ResultRevision = revision;
      _status.State = MicrophoneCalibrationStates.Editing;
      _status.Message = "Drag the microphone waveform to align it with the game audio.";
    }
    Main.Instance?.Log("[Calibration] Waveforms ready. durationMs=" + durationMs + ", revision=" + revision);
  }

  private void StartPreparedPreview(string operationId, StoredMicrophoneRecording recording)
  {
    if (!IsState(operationId, MicrophoneCalibrationStates.PreviewStarting) || _run == null)
    {
      ReplayMicrophonePlaybackFiles.Delete(recording.FilePath);
      return;
    }
    _originalRunInBackground = UnityEngine.Application.runInBackground;
    _changedRunInBackground = true;
    UnityEngine.Application.runInBackground = true;
    ReplayPlaybackStatus replayStatus = ReplayPlaybackCoordinator.PlayEphemeral(_run, _levelPath, recording);
    if (replayStatus.State == ReplayPlaybackStates.Error)
    {
      RestoreRunInBackground();
      Error(replayStatus.ErrorCode, replayStatus.Message, operationId);
      return;
    }
    _previewReplayOperationId = replayStatus.OperationId;
  }

  private void TickPreview()
  {
    ReplayPlaybackStatus replay = ReplayPlaybackCoordinator.GetStatus();
    if (_previewReplayOperationId == null)
      return;
    if (!string.Equals(replay.OperationId, _previewReplayOperationId, StringComparison.Ordinal))
    {
      StopPreview();
      return;
    }
    if (replay.State == ReplayPlaybackStates.Error)
    {
      _previewReplayOperationId = null;
      RestoreRunInBackground();
      Error(replay.ErrorCode, replay.Message, GetStatus().OperationId);
      return;
    }
    if (replay.State == ReplayPlaybackStates.Completed || replay.State == ReplayPlaybackStates.Cancelled)
    {
      bool completed = replay.State == ReplayPlaybackStates.Completed;
      _previewReplayOperationId = null;
      RestoreRunInBackground();
      UpdateState(MicrophoneCalibrationStates.Editing, completed ? "Calibration preview finished." : replay.Message);
      lock (_gate)
        _status.PlaybackPositionMs = completed ? _status.DurationMs : 0d;
      return;
    }

    if (replay.State == ReplayPlaybackStates.Playing)
      UpdateState(MicrophoneCalibrationStates.PreviewPlaying, "Playing calibration preview in ADOFAI.");
    if (ReplaySessionService.TryGetPlaybackSnapshot(out long replayTimeUs, out double timelineRate))
    {
      double positionMs = replayTimeUs / Math.Max(0.0001d, timelineRate) / 1000d;
      lock (_gate)
        _status.PlaybackPositionMs = Math.Max(0d, Math.Min(_status.DurationMs, positionMs));
    }
  }

  private void CleanupSession()
  {
    StopPreview();
    DestroyAudioTap();
    if (_recording != null)
      FeatureRegistry.MicrophoneRecording?.Discard(_recording);
    _recording = null;
    _run = null;
    _result = null;
    _levelPath = null;
    _levelGameplayHash = null;
    _levelOpenStartedAt = 0d;
    FeatureRegistry.MicrophoneRecording?.Disarm();
  }

  private void OpenCalibrationLevel()
  {
    string operationId = GetStatus().OperationId;
    try
    {
      UpdateState(MicrophoneCalibrationStates.OpeningLevel, "Opening the calibration level automatically.");
      _levelOpenStartedAt = Time.realtimeSinceStartupAsDouble;
      Main.Instance?.Log("[Calibration] Opening packaged calibration level: " + _levelPath);
      ReplayLevelOpenService.OpenEditor(_levelPath);
    }
    catch (Exception exception)
    {
      Error("calibration_level_open_failed", exception.Message, operationId);
    }
  }

  private void DestroyAudioTap()
  {
    if (_audioTap != null)
      UnityEngine.Object.Destroy(_audioTap);
    _audioTap = null;
  }

  private void RestoreRunInBackground()
  {
    if (_changedRunInBackground)
      UnityEngine.Application.runInBackground = _originalRunInBackground;
    _changedRunInBackground = false;
  }

  private bool IsCalibrationEditorReady()
  {
    scnEditor editor = scnEditor.instance;
    if (
      !IsCalibrationEditorBaseReady()
      || !LevelPathIdentity.Equals(_levelPath, LevelPathIdentity.Current())
      || !GameplayChartHash.TryCompute(editor.levelData, out byte[] currentHash, out _)
    )
      return false;

    _levelGameplayHash = currentHash;
    Main.Instance?.Log("[Calibration] Captured loaded calibration gameplay hash.");
    return true;
  }

  private static bool IsCalibrationEditorBaseReady()
  {
    scnEditor editor = scnEditor.instance;
    return editor != null && editor.initialized && !editor.isLoading;
  }

  private static bool IsGameplayActive()
  {
    if (string.Equals(ADOBase.sceneName, "scnEditor", StringComparison.Ordinal))
      return scnEditor.instance?.playMode == true;

    bool gameplayScene =
      string.Equals(ADOBase.sceneName, "scnGame", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCLS", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCalibration", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnMinesweeper", StringComparison.Ordinal);
    if (!gameplayScene || ADOBase.controller == null)
      return false;

    States state = ADOBase.controller.state;
    return state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl;
  }

  private static StoredReplayRun ToStoredReplayRun(RunRecord run) =>
    new StoredReplayRun
    {
      Id = run.Id,
      LevelSessionId = run.LevelSessionId,
      TufLevelId = run.TufLevelId,
      LevelPath = null,
      LevelTileCount = run.LevelTileCount,
      StartTile = run.StartTile,
      LastTile = run.LastTile,
      Result = run.Result,
      InputCsv = run.InputCsv,
      HitContextCsv = run.HitContextCsv,
      MetaJson = run.MetaJson,
      GameplayHash = run.GameplayHash,
      GameplayHashVersion = run.GameplayHashVersion,
    };

  private bool IsCurrent(string operationId)
  {
    lock (_gate)
      return !string.IsNullOrEmpty(operationId)
        && string.Equals(_status.OperationId, operationId, StringComparison.Ordinal);
  }

  private bool IsState(string operationId, string state)
  {
    lock (_gate)
      return !string.IsNullOrEmpty(operationId)
        && string.Equals(_status.OperationId, operationId, StringComparison.Ordinal)
        && string.Equals(_status.State, state, StringComparison.Ordinal);
  }

  private MicrophoneCalibrationStatus StaleOperation()
  {
    MicrophoneCalibrationStatus status = GetStatus();
    status.ErrorCode = "calibration_operation_stale";
    status.Message = "The calibration operation is no longer active.";
    return status;
  }

  private MicrophoneCalibrationStatus Error(string code, string message, string operationId = null)
  {
    lock (_gate)
    {
      _status = new MicrophoneCalibrationStatus
      {
        OperationId = operationId ?? _status.OperationId,
        State = MicrophoneCalibrationStates.Error,
        ErrorCode = code,
        Message = message,
        MicrophoneOffsetMs = TUFReplaySettingStore.Current?.MicrophoneOffsetMs ?? 0,
        MicrophoneVolumeDb = TUFReplaySettingStore.Current?.MicrophoneVolumeDb ?? 0,
      };
      return Clone(_status);
    }
  }

  private void ErrorIfCurrent(string operationId, string code, string message)
  {
    if (IsCurrent(operationId))
      Error(code, message, operationId);
  }

  private void ErrorIfPreviewStarting(string operationId, string code, string message)
  {
    if (IsState(operationId, MicrophoneCalibrationStates.PreviewStarting))
      Error(code, message, operationId);
  }

  private static MicrophoneCalibrationStatus Rejected(string code, string message) =>
    new MicrophoneCalibrationStatus
    {
      State = MicrophoneCalibrationStates.Error,
      ErrorCode = code,
      Message = message,
      MicrophoneOffsetMs = TUFReplaySettingStore.Current?.MicrophoneOffsetMs ?? 0,
      MicrophoneVolumeDb = TUFReplaySettingStore.Current?.MicrophoneVolumeDb ?? 0,
    };

  private void UpdateState(string state, string message)
  {
    lock (_gate)
    {
      _status.State = state;
      _status.ErrorCode = null;
      _status.Message = message;
    }
  }

  private void SetStatus(MicrophoneCalibrationStatus status)
  {
    lock (_gate)
      _status = status;
  }

  private static MicrophoneCalibrationStatus Clone(MicrophoneCalibrationStatus status) =>
    new MicrophoneCalibrationStatus
    {
      OperationId = status.OperationId,
      State = status.State,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
      DurationMs = status.DurationMs,
      PlaybackPositionMs = status.PlaybackPositionMs,
      ResultRevision = status.ResultRevision,
      MicrophoneOffsetMs = status.MicrophoneOffsetMs,
      MicrophoneVolumeDb = status.MicrophoneVolumeDb,
    };
}

public sealed class MicrophoneCalibrationTicker : MonoBehaviour
{
  public MicrophoneCalibrationFeature Feature;

  private void Update()
  {
    UnityMainThread.DrainPending();
    Feature?.Tick();
  }
}
