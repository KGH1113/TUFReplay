using System;
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
  private readonly MicrophoneCalibrationState _state = new MicrophoneCalibrationState();
  private readonly MicrophoneCalibrationLevel _level = new MicrophoneCalibrationLevel();
  private readonly MicrophoneCalibrationPreview _preview;
  private int _songStartFrame;
  private int _songSampleRate;
  private MicrophoneCalibrationTicker _ticker;

  public MicrophoneCalibrationFeature()
  {
    _preview = new MicrophoneCalibrationPreview(_state);
  }

  public bool Active => _state.Active;

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
    if (TUFReplaySettingStore.Current?.MicrophoneEnabled == false)
      return MicrophoneCalibrationState.Rejected(
        "microphone_disabled",
        "Turn on microphone input before starting calibration."
      );
    if (MicrophoneCalibrationLevel.IsGameplayActive())
      return MicrophoneCalibrationState.Rejected(
        "gameplay_active",
        "Finish the current gameplay run before starting calibration."
      );

    CleanupSession();
    if (!_level.TryPrepare(Main.Instance.PayloadPath))
      return Error("calibration_assets_missing", "The packaged calibration level, song, or waveform is missing.");

    string operationId = Guid.NewGuid().ToString("N");
    _state.Set(
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
    string state = GetStatus().State;
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
      if (_level.IsEditorReady())
      {
        Main.Instance?.Log("[Calibration] Captured loaded calibration gameplay hash.");
        Main.Instance?.Log("[Calibration] Calibration level opened automatically.");
        _state.Update(MicrophoneCalibrationStates.WaitingForRun, "Play and clear the calibration level in ADOFAI.");
      }
      else if (_level.HasOpenTimedOut())
        Error(
          "calibration_level_open_timeout",
          "ADOFAI did not finish opening the calibration level.",
          GetStatus().OperationId
        );
      return;
    }

    if (state == MicrophoneCalibrationStates.PreviewStarting || state == MicrophoneCalibrationStates.PreviewPlaying)
      _preview.Tick();
  }

  public bool IsCalibrationLevel() => Active && _level.IsCurrent();

  public void OnRunStarted()
  {
    if (!Active)
      return;
    scrConductor conductor = ADOBase.conductor;
    AudioSource song = conductor?.song;
    _songSampleRate = song?.clip?.frequency ?? 0;
    double originDspTime = AudioSettings.dspTime;
    _songStartFrame =
      conductor == null ? 0
      : GCS.d_oldConductor || GCS.d_webglConductor ? song?.timeSamples ?? 0
      : CalibrationWaveformBuilder.SourceFrameAtDspTime(
        originDspTime,
        conductor.dspTimeSong,
        conductor.separateCountdownTime,
        conductor.crotchetAtStart,
        conductor.adjustedCountdownTicks,
        song?.pitch ?? 1f,
        _songSampleRate
      );
    Main.Instance?.Log(
      "[Calibration] Song reference startFrame="
        + _songStartFrame
        + ", originDspTime="
        + originDspTime
        + ", songStartDspTime="
        + conductor?.dspTimeSong
        + ", separateCountdown="
        + conductor?.separateCountdownTime
    );
    _state.Update(MicrophoneCalibrationStates.Recording, "Recording the calibration run.");
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
    if (_songSampleRate <= 0)
    {
      FeatureRegistry.MicrophoneRecording?.Discard(recording);
      Error("game_waveform_failed", "The calibration song sample rate is unavailable.", GetStatus().OperationId);
      return;
    }
    int songStartFrame = _songStartFrame;
    int songSampleRate = _songSampleRate;
    string referenceWaveformPath = _level.ReferenceWaveformPath;
    ResetAudioCapture();
    _preview.SetSource(ToStoredReplayRun(run), recording);
    _state.Update(MicrophoneCalibrationStates.Processing, "Building calibration waveforms.");
    string operationId = GetStatus().OperationId;
    ThreadPool.QueueUserWorkItem(_ =>
      BuildWaveforms(operationId, recording, referenceWaveformPath, songStartFrame, songSampleRate, durationMs)
    );
  }

  public void OnRunDiscarded(CapturedMicrophoneRecording recording, string message)
  {
    ResetAudioCapture();
    FeatureRegistry.MicrophoneRecording?.Discard(recording);
    if (Active)
      _state.Update(MicrophoneCalibrationStates.WaitingForRun, message ?? "Try the calibration level again.");
  }

  public MicrophoneCalibrationStatus PlayPreview(string operationId)
  {
    if (!_state.IsCurrent(operationId))
      return _state.StaleOperation();
    return _preview.Play(operationId, _level.Path);
  }

  public MicrophoneCalibrationStatus StopPreview() => _preview.Stop();

  public MicrophoneCalibrationStatus SetOffset(string operationId, int offsetMs)
  {
    if (!_state.IsCurrent(operationId))
      return _state.StaleOperation();
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    settings.MicrophoneOffsetMs = Math.Max(
      TUFReplaySetting.MinMicrophoneOffsetMs,
      Math.Min(TUFReplaySetting.MaxMicrophoneOffsetMs, offsetMs)
    );
    TUFReplaySettingStore.Save();
    _state.SetOffset(settings.MicrophoneOffsetMs);
    ReplaySessionService.UpdateActiveMicrophoneSettings(settings.MicrophoneOffsetMs, settings.MicrophoneVolumeDb);
    return GetStatus();
  }

  public MicrophoneCalibrationStatus SetVolume(string operationId, int volumeDb)
  {
    if (!_state.IsCurrent(operationId))
      return _state.StaleOperation();
    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    settings.MicrophoneVolumeDb = Math.Max(
      TUFReplaySetting.MinMicrophoneVolumeDb,
      Math.Min(TUFReplaySetting.MaxMicrophoneVolumeDb, volumeDb)
    );
    TUFReplaySettingStore.Save();
    _state.SetVolume(settings.MicrophoneVolumeDb);
    ReplaySessionService.UpdateActiveMicrophoneSettings(settings.MicrophoneOffsetMs, settings.MicrophoneVolumeDb);
    return GetStatus();
  }

  public MicrophoneCalibrationStatus Close(string operationId)
  {
    if (operationId != null && !_state.IsCurrent(operationId))
      return _state.StaleOperation();
    CleanupSession();
    _state.Set(new MicrophoneCalibrationStatus { State = MicrophoneCalibrationStates.Idle });
    return GetStatus();
  }

  public MicrophoneCalibrationStatus GetStatus() => _state.GetStatus();

  public MicrophoneCalibrationResult GetResult(string operationId, int revision) =>
    _state.GetResult(operationId, revision);

  public bool IsCurrentOperation(string operationId) => _state.IsCurrent(operationId);

  private void BuildWaveforms(
    string operationId,
    CapturedMicrophoneRecording recording,
    string referenceWaveformPath,
    int songStartFrame,
    int songSampleRate,
    double durationMs
  )
  {
    try
    {
      CalibrationReferenceWaveform referenceWaveform = CalibrationReferenceWaveform.Read(referenceWaveformPath);
      float[] songWaveform = CalibrationWaveformBuilder.FromReferenceWaveform(
        referenceWaveform,
        songStartFrame,
        songSampleRate,
        durationMs
      );
      float[] microphoneWaveform = CalibrationWaveformBuilder.FromPcm16(recording, durationMs);
      UnityMainThread.Post(() => CompleteResult(operationId, durationMs, songWaveform, microphoneWaveform));
    }
    catch (Exception exception)
    {
      UnityMainThread.Post(() =>
      {
        if (_state.IsCurrent(operationId))
          Error("calibration_waveform_failed", exception.Message, operationId);
      });
    }
  }

  private void CompleteResult(string operationId, double durationMs, float[] songWaveform, float[] microphoneWaveform)
  {
    int revision = _state.CompleteResult(operationId, durationMs, songWaveform, microphoneWaveform);
    if (revision > 0)
      Main.Instance?.Log("[Calibration] Waveforms ready. durationMs=" + durationMs + ", revision=" + revision);
  }

  private void CleanupSession()
  {
    _preview.Reset();
    ResetAudioCapture();
    _state.ClearResult();
    _level.Reset();
    FeatureRegistry.MicrophoneRecording?.Disarm();
  }

  private void OpenCalibrationLevel()
  {
    string operationId = GetStatus().OperationId;
    try
    {
      _state.Update(MicrophoneCalibrationStates.OpeningLevel, "Opening the calibration level automatically.");
      Main.Instance?.Log("[Calibration] Opening packaged calibration level: " + _level.Path);
      _level.Open();
    }
    catch (Exception exception)
    {
      Error("calibration_level_open_failed", exception.Message, operationId);
    }
  }

  private void ResetAudioCapture()
  {
    _songStartFrame = 0;
    _songSampleRate = 0;
  }

  private MicrophoneCalibrationStatus Error(string code, string message, string operationId = null) =>
    _state.Error(code, message, operationId);

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
}
