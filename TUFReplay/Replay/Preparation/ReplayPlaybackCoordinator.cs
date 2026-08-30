using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Activity.Queries;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Playback;
using TUFReplay.Microphone.Processing;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.Unity;
using UnityEngine;

namespace TUFReplay.Replay.Preparation;

public static partial class ReplayPlaybackCoordinator
{
  private const double LevelOpenTimeoutSeconds = 30d;
  private const double EditorTransitionTimeoutSeconds = 10d;
  private static readonly object Gate = new object();
  private static readonly object CommandGate = new object();

  private static ReplayPlaybackStatus _status = ReplayPlaybackStatus.Idle();
  private static PendingReplay _operation;
  private static PendingReplay _preparingOperation;
  private static bool _waitingForEditor;
  private static bool _editorTransitionRequested;
  private static double _editorTransitionStartedAt;
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
      {
        ReplayEditorTransitionResult transition = ReplayEditorTransition.Request(scnEditor.instance);
        if (transition.Exception != null)
          Main.Instance?.LogException("Replay editor transition during cancel", transition.Exception);
      }
      if (operation != null)
        SetTerminal(operation, ReplayPlaybackStates.Cancelled, reason);
      else
        SetStatus(ReplayPlaybackStatus.Idle());
      _operation = null;
      ClearEditorTransitionState();
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
      if (scnEditor.instance != null && !scnEditor.instance.playMode)
      {
        ClearEditorTransitionState();
        PrepareLevel(operation);
        return;
      }

      if (EditorTransitionTimedOut())
      {
        Fail("editor_transition_timeout", "ADOFAI did not return to the editor within 10 seconds.");
        return;
      }

      if (scnEditor.instance == null || scnEditor.instance.playMode)
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
    ClearEditorTransitionState();
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
    ClearEditorTransitionState();
    _returnRequested = false;
    _forcedFail = false;
    SetStatus(ReplayPlaybackStatus.Idle());
  }

  private static bool BeginEditorTransition()
  {
    _waitingForEditor = true;
    _editorTransitionRequested = true;
    _editorTransitionStartedAt = Time.realtimeSinceStartupAsDouble;

    ReplayEditorTransitionResult transition = ReplayEditorTransition.Request(scnEditor.instance);
    if (transition.Recovered)
    {
      Main.Instance?.Log(
        "[Replay/Lifecycle] Recovered editor transition after an external Harmony exception. error="
          + transition.Exception.Message
      );
      return true;
    }

    if (!transition.Failed)
      return true;

    string message = "ADOFAI could not return to the editor because another patch failed.";
    if (transition.Exception != null)
      Main.Instance?.LogException("Replay editor transition", transition.Exception);
    Fail("editor_transition_failed", message);
    return false;
  }

  private static bool EditorTransitionTimedOut()
  {
    return _editorTransitionRequested
      && ReplayEditorTransition.HasTimedOut(
        _editorTransitionStartedAt,
        Time.realtimeSinceStartupAsDouble,
        EditorTransitionTimeoutSeconds
      );
  }

  private static void ClearEditorTransitionState()
  {
    _waitingForEditor = false;
    _editorTransitionRequested = false;
    _editorTransitionStartedAt = 0d;
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
}
