using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Recording;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.NativeInput;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Application.Replay;

public static class ReplayPlaybackCoordinator
{
  private const double LevelOpenTimeoutSeconds = 30d;
  private static readonly object Gate = new object();
  private static readonly object CommandGate = new object();

  private static ReplayPlaybackStatus _status = ReplayPlaybackStatus.Idle();
  private static PendingReplay _operation;
  private static PendingReplay _preparingOperation;
  private static bool _waitingForEditor;
  private static bool _returnRequested;
  private static string _returnTerminalState;
  private static int _returnNotBeforeFrame;
  private static bool _forcedFail;

  public static bool IsBusy
  {
    get
    {
      lock (Gate)
      {
        return _status.State == ReplayPlaybackStates.Preparing
          || _status.State == ReplayPlaybackStates.OpeningLevel
          || _status.State == ReplayPlaybackStates.WaitingForFocus
          || _status.State == ReplayPlaybackStates.Starting
          || _status.State == ReplayPlaybackStates.Playing
          || _status.State == ReplayPlaybackStates.ReturningToEditor;
      }
    }
  }

  internal static bool IsTimelinePlaying
  {
    get
    {
      lock (Gate)
        return _status.State == ReplayPlaybackStates.Playing;
    }
  }

  public static bool ShouldCancelForEditorQuitToMenu
  {
    get
    {
      lock (Gate)
      {
        return _status.State == ReplayPlaybackStates.WaitingForFocus
          || _status.State == ReplayPlaybackStates.Starting
          || _status.State == ReplayPlaybackStates.Playing;
      }
    }
  }

  public static ReplayPlaybackStatus Play(string runId, string levelPath = null)
  {
    lock (CommandGate)
    {
      string operationId = Guid.NewGuid().ToString("N");
      SetStatus(
        new ReplayPlaybackStatus
        {
          OperationId = operationId,
          RunId = runId,
          State = ReplayPlaybackStates.Preparing,
          Message = "Preparing replay.",
        }
      );

      if (ReplayLevelFilePickerCoordinator.IsPicking)
      {
        SetError(operationId, runId, "file_picker_busy", "A level file picker is still open.");
        return GetStatus();
      }

      if (!TryPrepare(operationId, runId, levelPath, out PendingReplay pending, out string code, out string message))
      {
        SetError(operationId, runId, code, message);
        return GetStatus();
      }

      CancelPendingPreparation();
      _preparingOperation = pending;
      UnityMainThread.Post(() => CancelCurrentReplayForReplacement(operationId));
      QueueMicrophonePreparation(pending);
      return GetStatus();
    }
  }

  internal static ReplayPlaybackStatus PlayEphemeral(
    StoredReplayRun run,
    string levelPath,
    StoredMicrophoneRecording microphoneRecording,
    Pcm16WaveInfo microphoneWave,
    Pcm16LimiterEnvelope microphoneLimiterEnvelope,
    int microphoneOffsetMs,
    int microphoneVolumeDb
  )
  {
    lock (CommandGate)
    {
      string operationId = Guid.NewGuid().ToString("N");
      SetStatus(
        new ReplayPlaybackStatus
        {
          OperationId = operationId,
          RunId = run?.Id,
          State = ReplayPlaybackStates.Preparing,
          Message = "Preparing calibration replay.",
        }
      );
      try
      {
        if (run == null)
          throw new InvalidDataException("The calibration replay is unavailable.");
        ReplayMetadata meta = JsonConvert.DeserializeObject<ReplayMetadata>(run.MetaJson ?? "{}");
        if (meta?.gameplayStartSongPosition == null)
          throw new InvalidDataException("The calibration replay timing metadata is missing.");
        List<RecordedInput> inputs = ReplayInputParser.Parse(run.InputCsv, out long fallbackTerminal);
        List<ReplayHitContext> hitContexts = ReplayHitContextParser.Parse(run.HitContextCsv);
        if (hitContexts.Count == 0)
          throw new InvalidDataException("The calibration replay has no hit contexts.");
        long terminalTimeUs = Math.Max(fallbackTerminal, meta.terminalTimeUs ?? fallbackTerminal);
        var pending = new PendingReplay(operationId, run, levelPath, meta, inputs, hitContexts, terminalTimeUs)
        {
          AllowBackground = true,
          MicrophoneRecording = microphoneRecording,
          MicrophoneWave = microphoneWave,
          MicrophoneLimiterEnvelope = microphoneLimiterEnvelope,
          MicrophoneOffsetMs = microphoneOffsetMs,
          MicrophoneVolumeDb = microphoneVolumeDb,
        };
        CancelPendingPreparation();
        CancelCurrentReplayForReplacement(operationId);
        BeginOnMainThread(pending);
      }
      catch (Exception exception)
      {
        ReplayMicrophonePlaybackFiles.Delete(microphoneRecording?.FilePath);
        SetError(operationId, run?.Id, "calibration_preview_invalid", exception.Message);
      }
      return GetStatus();
    }
  }

  public static ReplayPlaybackStatus GetStatus()
  {
    lock (Gate)
      return Clone(_status);
  }

  public static void Cancel(string reason = "cancelled")
  {
    lock (CommandGate)
    {
      ReplayLevelOpenService.ReleaseHeldBlack();
      CancelPendingPreparation();
      PendingReplay operation = _operation;
      ReplaySessionService.ClearActiveContext();
      operation?.CleanupPreparedMicrophone();
      if (scnEditor.instance != null && scnEditor.instance.playMode)
        scnEditor.instance.SwitchToEditMode();
      if (operation != null)
        SetTerminal(operation, ReplayPlaybackStates.Cancelled, reason);
      else
        SetStatus(ReplayPlaybackStatus.Idle());
      _operation = null;
      _waitingForEditor = false;
      _returnRequested = false;
      _forcedFail = false;
    }
  }

  public static void Tick()
  {
    PendingReplay operation = _operation;
    if (operation == null || !IsCurrent(operation.OperationId))
      return;

    if (_returnRequested)
    {
      TickReturnToEditor(operation);
      return;
    }

    if (_waitingForEditor)
    {
      if (scnEditor.instance == null || scnEditor.instance.playMode)
        return;
      _waitingForEditor = false;
      PrepareLevel(operation);
      return;
    }

    string state = GetStatus().State;
    if (state == ReplayPlaybackStates.OpeningLevel)
    {
      if (!IsEditorReady())
        operation.LevelOpenObservedTransition = true;
      if (IsExpectedLevelReady(operation))
      {
        WaitForFocusOrStart(operation);
      }
      else if (operation.HasLoadedLevelValidation && !operation.LoadedLevelValidationPassed)
      {
        Fail(
          operation.LoadedLevelValidationCode ?? "level_gameplay_modified",
          operation.LoadedLevelValidationMessage ?? LevelFileAccessValidator.ModifiedMessage
        );
      }
      else if (Time.realtimeSinceStartupAsDouble - operation.LevelOpenStartedAt > LevelOpenTimeoutSeconds)
      {
        Fail("level_open_timeout", "ADOFAI did not finish opening the recorded level.");
      }
      return;
    }

    if (state == ReplayPlaybackStates.WaitingForFocus)
      WaitForFocusOrStart(operation);
  }

  public static void OnGameStateChanged(States state)
  {
    PendingReplay operation = _operation;
    if (operation == null || !IsCurrent(operation.OperationId))
      return;

    switch (state)
    {
      case States.Countdown:
      case States.PlayerControl:
        SetOperationState(operation, ReplayPlaybackStates.Playing, "Replay is playing.");
        break;

      case States.Fail:
        SetOperationState(operation, ReplayPlaybackStates.Playing, "Replay reached fail; waiting for the fail screen.");
        break;

      case States.Fail2:
        CompleteWithoutEditorReturn(operation, "Replay reached the fail screen.");
        break;
    }
  }

  public static void OnReplayTimeAdvanced(long nowUs)
  {
    PendingReplay operation = _operation;
    if (operation == null || !IsCurrent(operation.OperationId))
      return;
    if (!ReplaySessionService.NativeInputFinished || nowUs < operation.TerminalTimeUs)
      return;

    if (string.Equals(operation.Run.Result, "cleared", StringComparison.OrdinalIgnoreCase))
    {
      if (TryGetControllerState(out States state) && state == States.Won)
        CompleteWithoutEditorReturn(operation, "Replay reached the clear screen.");
      return;
    }

    if (string.Equals(operation.Run.Result, "aborted", StringComparison.OrdinalIgnoreCase))
    {
      RequestReturn(operation, ReplayPlaybackStates.Completed, "Replay reached its recorded abort point.");
      return;
    }

    if (
      !string.Equals(operation.Run.Result, "failed", StringComparison.OrdinalIgnoreCase)
      || _forcedFail
      || !ReplaySessionService.HitContextFinished
    )
      return;

    _forcedFail = true;
    ReplayFailPolicy.ApplyReplayNoFail(false);
    if (ADOBase.controller?.playerOne != null)
    {
      ReplaySessionService.AllowReplayMarkFailOnce();
      try
      {
        ADOBase.controller.playerOne.Die();
      }
      finally
      {
        ReplaySessionService.SuppressReplayMarkFail();
      }
    }
    else
      Fail("controller_missing", "ADOFAI controller disappeared before the recorded fail.");
  }

  public static void OnReturnedToEditor()
  {
    PendingReplay operation = _operation;
    if (operation == null || !IsCurrent(operation.OperationId))
      return;

    if (_waitingForEditor)
    {
      ReplaySessionService.ClearActiveContext();
      return;
    }

    if (_returnRequested)
    {
      FinishReturn(operation);
      return;
    }

    string state = GetStatus().State;
    if (state == ReplayPlaybackStates.Starting || state == ReplayPlaybackStates.Playing)
    {
      ReplaySessionService.ClearActiveContext();
      SetTerminal(operation, ReplayPlaybackStates.Cancelled, "Replay cancelled with Escape.");
      operation.CleanupPreparedMicrophone();
      _operation = null;
    }
  }

  public static void Fail(string errorCode, string message)
  {
    PendingReplay operation = _operation;
    if (operation == null || !IsCurrent(operation.OperationId))
      return;

    ReplaySessionService.ClearActiveContext();
    operation.CleanupPreparedMicrophone();
    _returnRequested = false;
    _waitingForEditor = false;
    SetError(operation.OperationId, operation.Run.Id, errorCode, message);
    _operation = null;
  }

  public static void Shutdown()
  {
    ReplayLevelOpenService.ReleaseHeldBlack();
    CancelPendingPreparation();
    ReplaySessionService.ClearActiveContext();
    _operation?.CleanupPreparedMicrophone();
    _operation = null;
    _waitingForEditor = false;
    _returnRequested = false;
    _forcedFail = false;
    SetStatus(ReplayPlaybackStatus.Idle());
  }

  private static void QueueMicrophonePreparation(PendingReplay operation)
  {
    ThreadPool.QueueUserWorkItem(_ =>
    {
      string path = ReplayMicrophonePlaybackFiles.ForOperation(operation.OperationId);
      try
      {
        StoredMicrophoneRecording recording = MicrophoneRecordingRepository.CopyForPlayback(
          operation.Run.Id,
          path,
          operation.PreparationCancellation.Token
        );
        if (recording != null)
        {
          Pcm16WaveInfo wave = Pcm16WaveFile.ReadAndValidate(recording);
          Pcm16LimiterEnvelope limiterEnvelope = Pcm16WaveAnalyzer.Analyze(
            recording,
            wave,
            operation.PreparationCancellation.Token
          );
          operation.PreparationCancellation.Token.ThrowIfCancellationRequested();
          operation.MicrophoneRecording = recording;
          operation.MicrophoneWave = wave;
          operation.MicrophoneLimiterEnvelope = limiterEnvelope;
        }
      }
      catch (OperationCanceledException)
      {
        ReplayMicrophonePlaybackFiles.Delete(path);
        ReplayMicrophonePlaybackFiles.Delete(path + ".copying");
      }
      catch (Exception exception)
      {
        ReplayMicrophonePlaybackFiles.Delete(path);
        ReplayMicrophonePlaybackFiles.Delete(path + ".copying");
        Main.Instance?.Log(
          "[Replay/Microphone] Recording unavailable; replay will continue without it. error=" + exception.Message
        );
      }
      finally
      {
        UnityMainThread.Post(() => BeginOnMainThread(operation));
      }
    });
  }

  private static void CancelPendingPreparation()
  {
    PendingReplay preparing = _preparingOperation;
    _preparingOperation = null;
    preparing?.PreparationCancellation.Cancel();
  }

  private static void CancelCurrentReplayForReplacement(string replacementOperationId)
  {
    if (!IsCurrent(replacementOperationId))
      return;

    PendingReplay previous = _operation;
    ReplaySessionService.ClearActiveContext();
    previous?.CleanupPreparedMicrophone();
    _operation = null;
    _waitingForEditor = false;
    _returnRequested = false;
    _forcedFail = false;
  }

  private static bool TryPrepare(
    string operationId,
    string runId,
    string requestedLevelPath,
    out PendingReplay pending,
    out string errorCode,
    out string errorMessage
  )
  {
    pending = null;
    errorCode = null;
    errorMessage = null;

    StoredReplayRun run = RunRepository.GetReplayRun(runId);
    if (run == null)
      return Error("run_not_found", "The recorded run was not found.", out errorCode, out errorMessage);

    string playbackLevelPath = LevelPathIdentity.Canonicalize(
      string.IsNullOrWhiteSpace(requestedLevelPath) ? run.LevelPath : requestedLevelPath
    );
    if (playbackLevelPath == null)
      return Error("level_file_missing", LevelFileAccessValidator.MissingMessage, out errorCode, out errorMessage);

    ReplayMetadata meta;
    try
    {
      meta = JsonConvert.DeserializeObject<ReplayMetadata>(run.MetaJson ?? "{}");
    }
    catch
    {
      return Error("metadata_invalid", "Replay metadata could not be parsed.", out errorCode, out errorMessage);
    }

    if (meta == null || (meta.formatVersion != 1 && meta.formatVersion != 2 && meta.formatVersion != 3))
      return Error("format_unsupported", "This replay format is not supported.", out errorCode, out errorMessage);
    if (!meta.gameplayStartSongPosition.HasValue)
      return Error("metadata_invalid", "Replay gameplay timing metadata is missing.", out errorCode, out errorMessage);
    if (
      meta.formatVersion >= 2
      && !string.Equals(meta.inputTimeBase, RecordingClock.HybridInputTimeBase, StringComparison.Ordinal)
    )
      return Error("time_base_unsupported", "This replay time base is not supported.", out errorCode, out errorMessage);

    if (!ValidateNativePlatform(meta, run.InputCsv, out errorCode, out errorMessage))
      return false;

    List<RecordedInput> parsedInputs = ReplayInputParser.Parse(run.InputCsv);
    List<RecordedInput> inputs = NativeInputKeyCodeMapper.NormalizeForPlayback(parsedInputs, meta, out int dropped);
    List<ReplayHitContext> hitContexts = ReplayHitContextParser.Parse(run.HitContextCsv);
    if (
      (run.InputCsv?.Length > 0 && parsedInputs.Count == 0) || (run.HitContextCsv?.Length > 0 && hitContexts.Count == 0)
    )
      return Error("payload_invalid", "Replay payload data is malformed.", out errorCode, out errorMessage);
    if (inputs.Count == 0 && hitContexts.Count == 0)
      return Error("payload_empty", "This run has no replay data.", out errorCode, out errorMessage);
    if (dropped > 0)
      Main.Instance?.Log("[Replay] Dropped legacy input keys during normalization. count=" + dropped);

    if (run.StartTile < 0)
      return Error("start_tile_invalid", "The recorded start tile is invalid.", out errorCode, out errorMessage);
    if (!IsSupportedResult(run.Result))
      return Error("result_unsupported", "This run result cannot be replayed.", out errorCode, out errorMessage);

    long fallbackTerminal = inputs.Count == 0 ? 0L : Math.Max(0L, inputs.Max(input => input.TimeUs));
    long terminalTimeUs = Math.Max(fallbackTerminal, meta.terminalTimeUs ?? fallbackTerminal);
    pending = new PendingReplay(operationId, run, playbackLevelPath, meta, inputs, hitContexts, terminalTimeUs);
    return true;
  }

  private static void BeginOnMainThread(PendingReplay operation)
  {
    if (!IsCurrent(operation.OperationId) || operation.PreparationCancellation.IsCancellationRequested)
    {
      operation.CleanupPreparedMicrophone();
      return;
    }

    lock (CommandGate)
    {
      if (ReferenceEquals(_preparingOperation, operation))
        _preparingOperation = null;
    }

    if (
      !ReplayLevelHashValidator.ValidateTarget(
        operation.Run,
        operation.PlaybackLevelPath,
        out string canonicalPath,
        out string validationCode,
        out string validationMessage
      )
    )
    {
      operation.CleanupPreparedMicrophone();
      SetError(operation.OperationId, operation.Run.Id, validationCode, validationMessage);
      return;
    }
    operation.PlaybackLevelPath = canonicalPath;

    ReplaySessionService.ClearActiveContext();
    _operation = operation;
    _returnRequested = false;
    _waitingForEditor = false;
    _forcedFail = false;

    if (scnEditor.instance != null && scnEditor.instance.playMode)
    {
      _waitingForEditor = true;
      scnEditor.instance.SwitchToEditMode();
      return;
    }

    PrepareLevel(operation);
  }

  private static void PrepareLevel(PendingReplay operation)
  {
    if (!IsCurrent(operation.OperationId))
      return;

    if (IsExpectedLevelReady(operation))
    {
      ReplayLevelOpenService.ReleaseHeldBlack();
      WaitForFocusOrStart(operation);
      return;
    }

    operation.LevelOpenStartedAt = Time.realtimeSinceStartupAsDouble;
    operation.LevelDataBeforeOpen = scnEditor.instance?.levelData;
    operation.LevelOpenRequested = true;
    operation.LevelOpenObservedTransition = false;
    operation.HasLoadedLevelValidation = false;
    operation.ValidatedLevelData = null;
    operation.ValidatedLevelPath = null;
    operation.LoadedLevelValidationPassed = false;
    operation.LoadedLevelValidationCode = null;
    operation.LoadedLevelValidationMessage = null;
    operation.ValidatedLoadedGameplayHash = null;
    operation.ValidatedLoadedGameplayHashVersion = 0;
    SetOperationState(operation, ReplayPlaybackStates.OpeningLevel, "Opening recorded level in ADOFAI.");
    ReplayLevelOpenService.OpenEditor(operation.PlaybackLevelPath);
  }

  private static void WaitForFocusOrStart(PendingReplay operation)
  {
    if (!operation.PathEditingLockApplied)
    {
      scnEditor.instance.LockPathEditing(true);
      operation.PathEditingLockApplied = true;
    }

    if (operation.AllowBackground)
    {
      operation.NativeInputFocusGuard = AlwaysReadyFocusGuard.Instance;
      StartReplay(operation);
      return;
    }
    if (operation.NativeInputFocusGuard == null)
      operation.NativeInputFocusGuard = NativeInputFocusGuardFactory.Create();

    if (!operation.NativeInputFocusGuard.IsStable(out _))
    {
      if (GetStatus().State != ReplayPlaybackStates.WaitingForFocus)
        SetOperationState(operation, ReplayPlaybackStates.WaitingForFocus, "Focus ADOFAI to start replay.");
      return;
    }

    StartReplay(operation);
  }

  private static void StartReplay(PendingReplay operation)
  {
    if (!IsExpectedLevelReady(operation))
    {
      Fail(
        operation.LoadedLevelValidationCode ?? "level_not_ready",
        operation.LoadedLevelValidationMessage ?? "The recorded level is no longer ready."
      );
      return;
    }

    scnEditor editor = scnEditor.instance;
    string loadedLevelPath = LevelPathIdentity.Current();
    if (loadedLevelPath != null)
      operation.PlaybackLevelPath = loadedLevelPath;

    if (operation.Run.StartTile >= editor.floors.Count)
    {
      Fail("start_tile_invalid", "The recorded start tile is outside the current chart.");
      return;
    }

    ReplayInputScheduler scheduler = new ReplayInputScheduler(operation.Inputs);
    INativeInputFocusGuard focusGuard =
      operation.NativeInputFocusGuard
      ?? throw new InvalidOperationException("Native input focus guard is unavailable.");
    IReplayMicrophonePlayer microphonePlayer = null;
    if (
      operation.MicrophoneRecording != null
      && operation.MicrophoneWave != null
      && operation.MicrophoneLimiterEnvelope != null
    )
    {
      try
      {
        TUFReplaySetting settings = Infrastructure.Settings.TUFReplaySettingStore.Current;
        microphonePlayer = new ReplayMicrophonePlayer(
          operation.MicrophoneRecording,
          operation.MicrophoneWave,
          operation.MicrophoneLimiterEnvelope,
          operation.MicrophoneOffsetMs ?? settings?.MicrophoneOffsetMs ?? 0,
          operation.MicrophoneVolumeDb ?? settings?.MicrophoneVolumeDb ?? 0
        );
        operation.TransferMicrophoneOwnership();
      }
      catch (Exception exception)
      {
        operation.CleanupPreparedMicrophone();
        Main.Instance?.Log(
          "[Replay/Microphone] Playback initialization failed; replay will continue without it. error="
            + exception.Message
        );
      }
    }

    ActiveReplayContext context = new ActiveReplayContext
    {
      OperationId = operation.OperationId,
      RunId = operation.Run.Id,
      LevelPath = operation.PlaybackLevelPath,
      GameplayHash = (byte[])operation.ValidatedLoadedGameplayHash.Clone(),
      GameplayHashVersion = operation.ValidatedLoadedGameplayHashVersion,
      Result = operation.Run.Result,
      TufLevelId = operation.Run.TufLevelId,
      StartTile = operation.Run.StartTile,
      JudgmentDifficulty = operation.Run.JudgmentDifficulty,
      NoFailMode = operation.Run.NoFailMode,
      TerminalTimeUs = operation.TerminalTimeUs,
      Inputs = operation.Inputs,
      HitContexts = operation.HitContexts,
      NativeInputScheduler = scheduler,
      NativeInputPlayer = new ReplayNativeInputPlayer(
        scheduler,
        operation.AllowBackground ? NullNativeInputEmitter.Instance : NativeInputEmitterFactory.Create(focusGuard),
        focusGuard
      ),
      HitContextPlayer = new ReplayHitContextPlayer(operation.HitContexts),
      MicrophonePlayer = microphonePlayer,
      Meta = operation.Meta,
    };

    ReplaySessionService.InstallActiveContext(context);
    ReplaySessionService.ApplyReplayNoFailNow();
    ReplaySessionService.ApplyReplayPitchNow();
    ReplaySessionService.ApplyReplayJudgmentDifficultyNow();
    editor.SelectFloor(editor.floors[operation.Run.StartTile]);
    SetOperationState(operation, ReplayPlaybackStates.Starting, "Starting replay.");
    editor.Play();
  }

  private static bool IsExpectedLevelReady(PendingReplay operation)
  {
    scnEditor editor = scnEditor.instance;
    if (!IsEditorReady())
      return false;

    string currentPath = LevelPathIdentity.Current();
    if (!LevelPathIdentity.Equals(operation.PlaybackLevelPath, currentPath))
      return false;

    if (
      operation.LevelOpenRequested
      && !operation.LevelOpenObservedTransition
      && ReferenceEquals(operation.LevelDataBeforeOpen, editor.levelData)
    )
      return false;

    if (
      operation.HasLoadedLevelValidation
      && ReferenceEquals(operation.ValidatedLevelData, editor.levelData)
      && LevelPathIdentity.Equals(operation.ValidatedLevelPath, currentPath)
    )
      return operation.LoadedLevelValidationPassed;

    bool passed = ReplayLevelHashValidator.ValidateLoaded(
      operation.Run,
      editor.levelData,
      currentPath,
      out operation.LoadedLevelValidationCode,
      out operation.LoadedLevelValidationMessage
    );
    operation.HasLoadedLevelValidation = true;
    operation.ValidatedLevelData = editor.levelData;
    operation.ValidatedLevelPath = currentPath;
    operation.LoadedLevelValidationPassed = passed;
    if (
      passed
      && GameplayChartHash.TryCompute(
        editor.levelData,
        operation.Run.GameplayHashVersion.Value,
        out byte[] loadedGameplayHash,
        out _
      )
    )
    {
      operation.ValidatedLoadedGameplayHash = loadedGameplayHash;
      operation.ValidatedLoadedGameplayHashVersion = operation.Run.GameplayHashVersion.Value;
    }
    else if (passed)
    {
      operation.LoadedLevelValidationPassed = false;
      operation.LoadedLevelValidationCode = "level_file_invalid";
      operation.LoadedLevelValidationMessage = LevelFileAccessValidator.InvalidMessage;
    }
    return operation.LoadedLevelValidationPassed;
  }

  private static bool IsEditorReady()
  {
    scnEditor editor = scnEditor.instance;
    return editor != null && editor.initialized && !editor.isLoading && !editor.playMode && editor.floors != null;
  }

  private static void RequestReturn(PendingReplay operation, string terminalState, string message)
  {
    if (_returnRequested || !IsCurrent(operation.OperationId))
      return;

    _returnRequested = true;
    _returnTerminalState = terminalState;
    _returnNotBeforeFrame = Time.frameCount + 1;
    ReplaySessionService.ClearActiveContext();
    SetOperationState(operation, ReplayPlaybackStates.ReturningToEditor, message);
  }

  private static void CompleteWithoutEditorReturn(PendingReplay operation, string message)
  {
    if (!IsCurrent(operation.OperationId))
      return;

    bool shouldRearmRecording = !operation.AllowBackground;
    ReplaySessionService.ClearActiveContext();
    operation.CleanupPreparedMicrophone();
    SetTerminal(operation, ReplayPlaybackStates.Completed, message);
    _returnRequested = false;
    _waitingForEditor = false;
    _forcedFail = false;
    _operation = null;

    if (shouldRearmRecording)
    {
      try
      {
        FeatureRegistry.Recording?.OnReplayEndedInPlayMode();
      }
      catch (Exception exception)
      {
        Main.Instance?.LogException("OnReplayEndedInPlayMode", exception);
      }
    }
  }

  private static void TickReturnToEditor(PendingReplay operation)
  {
    if (Time.frameCount < _returnNotBeforeFrame)
      return;

    if (scnEditor.instance != null && scnEditor.instance.playMode)
    {
      scnEditor.instance.SwitchToEditMode();
      return;
    }

    FinishReturn(operation);
  }

  private static void FinishReturn(PendingReplay operation)
  {
    ReplaySessionService.ClearActiveContext();
    operation.CleanupPreparedMicrophone();
    string terminalState = _returnTerminalState ?? ReplayPlaybackStates.Completed;
    SetTerminal(
      operation,
      terminalState,
      terminalState == ReplayPlaybackStates.Cancelled ? "Replay cancelled." : "Replay finished."
    );
    _returnRequested = false;
    _waitingForEditor = false;
    _forcedFail = false;
    _operation = null;
  }

  private static void SetOperationState(PendingReplay operation, string state, string message)
  {
    if (!IsCurrent(operation.OperationId))
      return;
    SetStatus(
      new ReplayPlaybackStatus
      {
        OperationId = operation.OperationId,
        RunId = operation.Run.Id,
        State = state,
        Message = message,
      }
    );
  }

  private static void SetTerminal(PendingReplay operation, string state, string message)
  {
    SetStatus(
      new ReplayPlaybackStatus
      {
        OperationId = operation.OperationId,
        RunId = operation.Run.Id,
        State = state,
        Message = message,
      }
    );
  }

  private static void SetError(string operationId, string runId, string code, string message)
  {
    ReplayLevelOpenService.ReleaseHeldBlack();
    SetStatus(
      new ReplayPlaybackStatus
      {
        OperationId = operationId,
        RunId = runId,
        State = ReplayPlaybackStates.Error,
        ErrorCode = code,
        Message = message,
      }
    );
  }

  private static void SetStatus(ReplayPlaybackStatus status)
  {
    lock (Gate)
      _status = status;
  }

  private static bool IsCurrent(string operationId)
  {
    lock (Gate)
      return string.Equals(_status.OperationId, operationId, StringComparison.Ordinal);
  }

  private static ReplayPlaybackStatus Clone(ReplayPlaybackStatus status)
  {
    return new ReplayPlaybackStatus
    {
      OperationId = status.OperationId,
      RunId = status.RunId,
      State = status.State,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
    };
  }

  private static bool ValidateNativePlatform(
    ReplayMetadata meta,
    byte[] inputCsv,
    out string errorCode,
    out string errorMessage
  )
  {
    errorCode = null;
    errorMessage = null;
    if (inputCsv == null || inputCsv.Length == 0)
      return true;
    if (!string.Equals(meta.inputKeySpace, NativeInputKeyCodeMapper.NativeKeySpace, StringComparison.OrdinalIgnoreCase))
      return true;

    string current = CurrentPlatform();
    if (current == "unsupported")
      return Error(
        "native_input_unsupported",
        "Native replay input is not supported on this platform.",
        out errorCode,
        out errorMessage
      );
    if (
      !string.IsNullOrWhiteSpace(meta.inputNativePlatform)
      && !string.Equals(meta.inputNativePlatform, current, StringComparison.OrdinalIgnoreCase)
    )
      return Error(
        "native_platform_mismatch",
        "This replay was recorded on a different operating system.",
        out errorCode,
        out errorMessage
      );
    return true;
  }

  private static string CurrentPlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";
    return "unsupported";
  }

  private static bool IsSupportedResult(string result)
  {
    return string.Equals(result, "cleared", StringComparison.OrdinalIgnoreCase)
      || string.Equals(result, "failed", StringComparison.OrdinalIgnoreCase)
      || string.Equals(result, "aborted", StringComparison.OrdinalIgnoreCase);
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }

  private static bool TryGetControllerState(out States state)
  {
    state = default;
    if (ADOBase.controller == null)
      return false;

    try
    {
      object machineState = ADOBase.controller.stateMachine?.GetState();
      if (machineState is States current)
      {
        state = current;
        return true;
      }
    }
    catch { }

    state = ADOBase.controller.state;
    return true;
  }

  private sealed class PendingReplay
  {
    public readonly string OperationId;
    public readonly StoredReplayRun Run;
    public string PlaybackLevelPath;
    public readonly ReplayMetadata Meta;
    public readonly List<RecordedInput> Inputs;
    public readonly List<ReplayHitContext> HitContexts;
    public readonly long TerminalTimeUs;
    public readonly CancellationTokenSource PreparationCancellation = new CancellationTokenSource();
    public double LevelOpenStartedAt;
    public object LevelDataBeforeOpen;
    public bool LevelOpenRequested;
    public bool LevelOpenObservedTransition;
    public bool HasLoadedLevelValidation;
    public object ValidatedLevelData;
    public string ValidatedLevelPath;
    public bool LoadedLevelValidationPassed;
    public string LoadedLevelValidationCode;
    public string LoadedLevelValidationMessage;
    public byte[] ValidatedLoadedGameplayHash;
    public int ValidatedLoadedGameplayHashVersion;
    public bool PathEditingLockApplied;
    public INativeInputFocusGuard NativeInputFocusGuard;
    public StoredMicrophoneRecording MicrophoneRecording;
    public Pcm16WaveInfo MicrophoneWave;
    public Pcm16LimiterEnvelope MicrophoneLimiterEnvelope;
    public int? MicrophoneOffsetMs;
    public int? MicrophoneVolumeDb;
    public bool AllowBackground;

    public PendingReplay(
      string operationId,
      StoredReplayRun run,
      string playbackLevelPath,
      ReplayMetadata meta,
      List<RecordedInput> inputs,
      List<ReplayHitContext> hitContexts,
      long terminalTimeUs
    )
    {
      OperationId = operationId;
      Run = run;
      PlaybackLevelPath = playbackLevelPath;
      Meta = meta;
      Inputs = inputs;
      HitContexts = hitContexts;
      TerminalTimeUs = terminalTimeUs;
    }

    public void TransferMicrophoneOwnership()
    {
      MicrophoneRecording = null;
      MicrophoneWave = null;
      MicrophoneLimiterEnvelope = null;
    }

    public void CleanupPreparedMicrophone()
    {
      string path = MicrophoneRecording?.FilePath;
      MicrophoneRecording = null;
      MicrophoneWave = null;
      MicrophoneLimiterEnvelope = null;
      ReplayMicrophonePlaybackFiles.Delete(path);
    }
  }

  private sealed class AlwaysReadyFocusGuard : INativeInputFocusGuard
  {
    public static readonly AlwaysReadyFocusGuard Instance = new AlwaysReadyFocusGuard();

    public bool IsStable(out string reason)
    {
      reason = null;
      return true;
    }

    public bool IsForegroundTarget(out string reason) => IsStable(out reason);

    public string Describe() => "calibration-background";
  }

  private sealed class NullNativeInputEmitter : INativeInputEmitter
  {
    public static readonly NullNativeInputEmitter Instance = new NullNativeInputEmitter();

    public bool IsSupported(int key) => true;

    public bool EmitBatch(NativeInputEmission[] emissions, int count) => true;
  }
}
