using System;

namespace TUFReplay.Replay.Preparation;

internal enum ReplayEditorTransitionOutcome
{
  AlreadyInEditor,
  Pending,
  Completed,
  RecoveredAfterExternalException,
  Failed,
}

internal readonly struct ReplayEditorTransitionResult
{
  public readonly ReplayEditorTransitionOutcome Outcome;
  public readonly Exception Exception;

  public ReplayEditorTransitionResult(ReplayEditorTransitionOutcome outcome, Exception exception = null)
  {
    Outcome = outcome;
    Exception = exception;
  }

  public bool Failed => Outcome == ReplayEditorTransitionOutcome.Failed;
  public bool Recovered => Outcome == ReplayEditorTransitionOutcome.RecoveredAfterExternalException;
}

internal static class ReplayEditorTransition
{
  public static ReplayEditorTransitionResult Request(scnEditor editor)
  {
    if (editor == null || !editor.playMode)
      return new ReplayEditorTransitionResult(ReplayEditorTransitionOutcome.AlreadyInEditor);

    try
    {
      editor.SwitchToEditMode();
      return new ReplayEditorTransitionResult(
        editor.playMode ? ReplayEditorTransitionOutcome.Pending : ReplayEditorTransitionOutcome.Completed
      );
    }
    catch (Exception exception)
    {
      return ClassifyAfterException(editor.playMode, exception);
    }
  }

  internal static ReplayEditorTransitionResult ClassifyAfterException(
    bool playModeAfterException,
    Exception exception
  )
  {
    return new ReplayEditorTransitionResult(
      playModeAfterException
        ? ReplayEditorTransitionOutcome.Failed
        : ReplayEditorTransitionOutcome.RecoveredAfterExternalException,
      exception
    );
  }

  internal static bool HasTimedOut(double startedAt, double now, double timeoutSeconds)
  {
    return timeoutSeconds >= 0d && now - startedAt >= timeoutSeconds;
  }
}
