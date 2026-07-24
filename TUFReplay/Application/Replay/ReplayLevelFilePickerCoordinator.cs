using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;
using UnityFileDialog;

namespace TUFReplay.Application.Replay;

public static class ReplayLevelFilePickerCoordinator
{
  private sealed class PickOperation
  {
    public readonly long Generation;
    public readonly string Id;
    public readonly StoredReplayRun Run;
    public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    public readonly TaskCompletionSource<ReplayLevelFilePickerResult> Completion =
      new TaskCompletionSource<ReplayLevelFilePickerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

    public PickOperation(long generation, StoredReplayRun run)
    {
      Generation = generation;
      Id = Guid.NewGuid().ToString("N");
      Run = run;
    }
  }

  private static readonly object Gate = new object();
  private static long _generation;
  private static PickOperation _active;

  public static bool IsPicking
  {
    get
    {
      lock (Gate)
        return _active != null;
    }
  }

  // AdofaiIpc 1.x handlers are synchronous. The IPC server thread waits here while
  // the picker and ADOFAI's own level decoder run on Unity's main thread.
  public static ReplayLevelFilePickerResult Pick(string runId)
  {
    if (ReplayPlaybackCoordinator.IsBusy)
      return Error(runId, "replay_busy", "A replay is already in progress.");

    StoredReplayRun run = RunRepository.GetReplayRun(runId);
    if (run == null)
      return Error(runId, "run_not_found", "The recorded run was not found.");

    PickOperation operation;
    lock (Gate)
    {
      if (_active != null)
        return Error(runId, "file_picker_busy", "Another level file picker is already open.");

      operation = new PickOperation(++_generation, run);
      _active = operation;
    }

    UnityMainThread.Post(() => BeginPickOnMainThread(operation));
    try
    {
      return operation.Completion.Task.GetAwaiter().GetResult();
    }
    finally
    {
      lock (Gate)
      {
        if (ReferenceEquals(_active, operation))
          _active = null;
      }
      operation.Cancellation.Dispose();
    }
  }

  public static void Shutdown()
  {
    PickOperation operation;
    lock (Gate)
    {
      operation = _active;
      _active = null;
      _generation++;
    }

    if (operation == null)
      return;
    operation.Cancellation.Cancel();
    operation.Completion.TrySetResult(Cancelled(operation.Run.Id));
  }

  private static void BeginPickOnMainThread(PickOperation operation)
  {
    if (!IsCurrent(operation))
      return;

    if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
    {
      ThreadPool.QueueUserWorkItem(_ => PickOnMac(operation));
      return;
    }

    string selectedPath;
    try
    {
      selectedPath = FileBrowser.PickFile(
        Persistence.GetLastUsedFolder(),
        RDString.Get("editor.dialog.adofaiLevelDescription"),
        GCS.levelExtensions,
        RDString.Get("editor.dialog.openFile")
      );
    }
    catch (Exception exception)
    {
      Complete(operation, Error(operation.Run.Id, "file_picker_failed", PickerFailure(exception)));
      return;
    }

    QueueValidation(operation, selectedPath);
  }

  private static void PickOnMac(PickOperation operation)
  {
    string selectedPath = null;
    string error = null;
    bool cancelled = false;
    try
    {
      using Process process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/usr/bin/osascript",
          Arguments =
            "-e \"POSIX path of (choose file with prompt \\\"Open ADOFAI level\\\" of type {\\\"adofai\\\"})\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        },
      };
      process.Start();
      string output = process.StandardOutput.ReadToEnd();
      string standardError = process.StandardError.ReadToEnd();
      process.WaitForExit();
      if (process.ExitCode == 0)
        selectedPath = output.Trim();
      else if (standardError.Contains("(-128)"))
        cancelled = true;
      else
        error = string.IsNullOrWhiteSpace(standardError) ? "The macOS file picker failed." : standardError.Trim();
    }
    catch (Exception exception)
    {
      error = "The macOS file picker failed: " + exception.GetType().Name;
    }

    if (!IsCurrent(operation))
      return;
    if (error != null)
    {
      Complete(operation, Error(operation.Run.Id, "file_picker_failed", error));
      return;
    }
    QueueValidation(operation, cancelled ? null : selectedPath);
  }

  private static void QueueValidation(PickOperation operation, string selectedPath)
  {
    if (!IsCurrent(operation))
      return;
    if (string.IsNullOrWhiteSpace(selectedPath))
    {
      Complete(operation, Cancelled(operation.Run.Id));
      return;
    }

    UnityMainThread.Post(() => BeginValidationOnMainThread(operation, selectedPath));
  }

  private static void BeginValidationOnMainThread(PickOperation operation, string selectedPath)
  {
    if (!IsCurrent(operation) || operation.Cancellation.IsCancellationRequested)
      return;

    ReplayLevelOpenService.WipeForVerification(
      uiController => ValidateWhileBlack(operation, selectedPath, uiController),
      () => Complete(operation, Error(operation.Run.Id, "level_wipe_cancelled", "The level verification transition was interrupted."))
    );
  }

  private static void ValidateWhileBlack(
    PickOperation operation,
    string selectedPath,
    scrUIController uiController
  )
  {
    if (!IsCurrent(operation) || operation.Cancellation.IsCancellationRequested)
    {
      ReplayLevelOpenService.ReturnFromVerification(uiController);
      return;
    }

    ReplayLevelFilePickerResult result;
    try
    {
      result = ValidateSelection(operation.Run, selectedPath);
    }
    catch (Exception exception)
    {
      result = Error(
        operation.Run.Id,
        "level_hash_failed",
        "Level verification failed: " + exception.GetType().Name
      );
    }

    if (!IsCurrent(operation) || operation.Cancellation.IsCancellationRequested)
    {
      ReplayLevelOpenService.ReturnFromVerification(uiController);
      return;
    }

    if (result.Outcome != ReplayLevelFilePickerOutcomes.Selected)
    {
      ReplayLevelOpenService.ReturnFromVerification(uiController);
      Complete(operation, result);
      return;
    }

    try
    {
      Persistence.UpdateLastUsedFolder(result.LevelPath);
      ReplayLevelOpenService.HoldVerifiedLevel(result.LevelPath, uiController);
      if (!Complete(operation, result))
        ReplayLevelOpenService.ReleaseHeldBlack();
    }
    catch (Exception exception)
    {
      ReplayLevelOpenService.ReturnFromVerification(uiController);
      Complete(operation, Error(operation.Run.Id, "file_picker_failed", PickerFailure(exception)));
    }
  }

  private static ReplayLevelFilePickerResult ValidateSelection(StoredReplayRun run, string selectedPath)
  {
    string canonicalPath = LevelPathIdentity.Canonicalize(selectedPath);
    if (canonicalPath == null)
      return Error(run.Id, "level_unavailable", "The selected level file is unavailable.");

    if (!TryResolveReferenceHash(run, out byte[] referenceHash, out string errorCode, out string errorMessage))
      return Error(run.Id, errorCode, errorMessage);

    if (!GameplayChartHash.TryLoadCustomLevel(canonicalPath, out _, out byte[] selectedHash, out string hashError))
      return Error(run.Id, "level_hash_failed", hashError);

    if (!GameplayChartHash.Equals(referenceHash, selectedHash))
    {
      return new ReplayLevelFilePickerResult
      {
        RunId = run.Id,
        Outcome = ReplayLevelFilePickerOutcomes.Mismatch,
        LevelPath = canonicalPath,
        ErrorCode = "level_gameplay_mismatch",
        Message = "This file has different tiles. Choose another level file.",
      };
    }

    return new ReplayLevelFilePickerResult
    {
      RunId = run.Id,
      Outcome = ReplayLevelFilePickerOutcomes.Selected,
      LevelPath = canonicalPath,
      Message = "Matching level file selected.",
    };
  }

  private static bool TryResolveReferenceHash(
    StoredReplayRun run,
    out byte[] referenceHash,
    out string errorCode,
    out string errorMessage
  )
  {
    referenceHash = null;
    errorCode = null;
    errorMessage = null;
    if (run.GameplayHash != null || run.GameplayHashVersion.HasValue)
    {
      if (!GameplayChartHash.IsSupported(run.GameplayHashVersion, run.GameplayHash))
        return Fail("level_hash_unsupported", "This run uses an unsupported gameplay hash.", out errorCode, out errorMessage);
      referenceHash = run.GameplayHash;
      return true;
    }

    string originalPath = LevelPathIdentity.Canonicalize(run.LevelPath);
    if (originalPath == null)
    {
      return Fail(
        "level_hash_unavailable",
        "The original level file is required once to identify this older run.",
        out errorCode,
        out errorMessage
      );
    }
    if (!GameplayChartHash.TryLoadCustomLevel(originalPath, out _, out referenceHash, out string hashError))
      return Fail("level_hash_failed", hashError, out errorCode, out errorMessage);

    run.GameplayHash = referenceHash;
    run.GameplayHashVersion = GameplayChartHash.Version;
    RunRepository.UpdateGameplayHashIfMissing(run.Id, referenceHash, GameplayChartHash.Version);
    return true;
  }

  private static bool IsCurrent(PickOperation operation)
  {
    lock (Gate)
      return ReferenceEquals(_active, operation) && operation.Generation == _generation;
  }

  private static bool Complete(PickOperation operation, ReplayLevelFilePickerResult result)
  {
    return IsCurrent(operation) && operation.Completion.TrySetResult(result);
  }

  private static ReplayLevelFilePickerResult Cancelled(string runId) =>
    new ReplayLevelFilePickerResult
    {
      RunId = runId,
      Outcome = ReplayLevelFilePickerOutcomes.Cancelled,
      Message = "Level file selection was cancelled.",
    };

  private static ReplayLevelFilePickerResult Error(string runId, string code, string message) =>
    new ReplayLevelFilePickerResult
    {
      RunId = runId,
      Outcome = ReplayLevelFilePickerOutcomes.Error,
      ErrorCode = code,
      Message = message,
    };

  private static bool Fail(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }

  private static string PickerFailure(Exception exception) =>
    "The level file picker failed: " + exception.GetType().Name;
}
