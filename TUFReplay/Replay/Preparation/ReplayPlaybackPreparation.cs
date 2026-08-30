using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Queries;
using TUFReplay.Activity.Repositories;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Playback;
using TUFReplay.Microphone.Processing;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Unity;
using UnityEngine;

namespace TUFReplay.Replay.Preparation;

public static partial class ReplayPlaybackCoordinator
{
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
    ClearEditorTransitionState();
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

    if (!HasCurrentNativeInputFormat(meta))
      return Error(
        "input_migration_required",
        "This replay uses a legacy input format and must be migrated before playback.",
        out errorCode,
        out errorMessage
      );
    if (!meta.gameplayStartSongPosition.HasValue)
      return Error("metadata_invalid", "Replay gameplay timing metadata is missing.", out errorCode, out errorMessage);
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

  internal static bool HasCurrentNativeInputFormat(ReplayMetadata meta)
  {
    return meta != null
      && meta.formatVersion == 3
      && string.Equals(meta.inputFormat, RecordedRunPayload.NativeInputFormatV2, StringComparison.Ordinal)
      && string.Equals(meta.inputTimeBase, ReplayInputTimeBases.Hybrid, StringComparison.Ordinal)
      && string.Equals(meta.inputKeySpace, NativeInputKeyCodeMapper.NativeKeySpace, StringComparison.OrdinalIgnoreCase)
      && !string.IsNullOrWhiteSpace(meta.inputNativePlatform)
      && !string.IsNullOrWhiteSpace(meta.inputCapture)
      && meta.inputCapture.IndexOf("skyhook", StringComparison.OrdinalIgnoreCase) < 0
      && meta.inputCapture.IndexOf("polling", StringComparison.OrdinalIgnoreCase) < 0;
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
    ClearEditorTransitionState();
    _forcedFail = false;

    if (scnEditor.instance != null && scnEditor.instance.playMode)
    {
      BeginEditorTransition();
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
        TUFReplaySetting settings = TUFReplay.Shared.Settings.TUFReplaySettingStore.Current;
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
      && !string.Equals(meta.inputFormat, RecordedRunPayload.NativeInputFormatV2, StringComparison.Ordinal)
    )
      return Error(
        "native_platform_mismatch",
        "This legacy replay was recorded on a different operating system.",
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
}
