using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Queries;
using TUFReplay.Composition;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Sessions;
using UnityEngine;

namespace TUFReplay.Replay.Preparation;

public static partial class ReplayPlaybackCoordinator
{
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
    if (passed && GameplayChartHash.TryCompute(editor.levelData, out byte[] loadedGameplayHash, out _))
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
    ClearEditorTransitionState();
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
    ClearEditorTransitionState();
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
      if (!_editorTransitionRequested)
      {
        _editorTransitionRequested = true;
        _editorTransitionStartedAt = Time.realtimeSinceStartupAsDouble;
        ReplayEditorTransitionResult transition = ReplayEditorTransition.Request(scnEditor.instance);
        if (transition.Recovered)
        {
          Main.Instance?.Log(
            "[Replay/Lifecycle] Recovered replay return after an external Harmony exception. error="
              + transition.Exception.Message
          );
        }
        else if (transition.Failed)
        {
          if (transition.Exception != null)
            Main.Instance?.LogException("Replay return editor transition", transition.Exception);
          Fail("editor_transition_failed", "ADOFAI could not return to the editor because another patch failed.");
        }
      }
      else if (EditorTransitionTimedOut())
      {
        Fail("editor_transition_timeout", "ADOFAI did not return to the editor within 10 seconds.");
      }
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
    ClearEditorTransitionState();
    _forcedFail = false;
    _operation = null;
  }
}
