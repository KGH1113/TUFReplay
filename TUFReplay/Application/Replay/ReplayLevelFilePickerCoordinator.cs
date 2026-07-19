using System;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;
using UnityFileDialog;

namespace TUFReplay.Application.Replay;

public static class ReplayLevelFilePickerCoordinator
{
  private static readonly object Gate = new object();
  private static ReplayLevelFilePickerStatus _status;

  public static bool IsPicking
  {
    get
    {
      lock (Gate)
        return _status?.State == ReplayLevelFilePickerStates.Picking;
    }
  }

  public static bool TryStart(
    string runId,
    out ReplayLevelFilePickerStatus status,
    out string errorCode,
    out string errorMessage
  )
  {
    status = null;
    errorCode = null;
    errorMessage = null;
    if (ReplayPlaybackCoordinator.IsBusy)
      return Error("replay_busy", "A replay is already in progress.", out errorCode, out errorMessage);

    StoredReplayRun run = RunRepository.GetReplayRun(runId);
    if (run == null)
      return Error("run_not_found", "The recorded run was not found.", out errorCode, out errorMessage);

    lock (Gate)
    {
      if (_status?.State == ReplayLevelFilePickerStates.Picking)
        return Error("file_picker_busy", "Another level file picker is already open.", out errorCode, out errorMessage);

      string operationId = Guid.NewGuid().ToString("N");
      _status = new ReplayLevelFilePickerStatus
      {
        OperationId = operationId,
        RunId = runId,
        State = ReplayLevelFilePickerStates.Picking,
        Message = "Waiting for level file selection.",
      };
      status = Clone(_status);
      UnityMainThread.Post(() => BeginPickOnMainThread(operationId, run));
      return true;
    }
  }

  public static bool TryGetStatus(
    string operationId,
    out ReplayLevelFilePickerStatus status,
    out string errorCode,
    out string errorMessage
  )
  {
    errorCode = null;
    errorMessage = null;
    lock (Gate)
    {
      if (_status == null || !string.Equals(_status.OperationId, operationId, StringComparison.Ordinal))
      {
        status = null;
        return Error(
          "file_picker_not_found",
          "The level file picker operation was not found.",
          out errorCode,
          out errorMessage
        );
      }

      status = Clone(_status);
      return true;
    }
  }

  public static void Shutdown()
  {
    lock (Gate)
      _status = null;
  }

  private static void BeginPickOnMainThread(string operationId, StoredReplayRun run)
  {
    if (!IsCurrent(operationId))
      return;

    if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
    {
      ThreadPool.QueueUserWorkItem(_ => PickOnMac(operationId, run));
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
      SetError(operationId, "file_picker_failed", "The level file picker failed: " + exception.GetType().Name);
      return;
    }

    CompleteSelectionOnMainThread(operationId, run, selectedPath);
  }

  private static void PickOnMac(string operationId, StoredReplayRun run)
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

    UnityMainThread.Post(() =>
    {
      if (!IsCurrent(operationId))
        return;
      if (error != null)
      {
        SetError(operationId, "file_picker_failed", error);
        return;
      }

      CompleteSelectionOnMainThread(operationId, run, cancelled ? null : selectedPath);
    });
  }

  private static void CompleteSelectionOnMainThread(string operationId, StoredReplayRun run, string selectedPath)
  {
    if (!IsCurrent(operationId))
      return;
    if (string.IsNullOrWhiteSpace(selectedPath))
    {
      SetStatus(
        operationId,
        new ReplayLevelFilePickerStatus
        {
          OperationId = operationId,
          RunId = run.Id,
          State = ReplayLevelFilePickerStates.Cancelled,
          Message = "Level file selection was cancelled.",
        }
      );
      return;
    }

    if (
      !ReplayLevelHashValidator.ValidateTarget(
        run,
        selectedPath,
        out string canonicalPath,
        out string validationCode,
        out string validationMessage
      )
    )
    {
      SetError(operationId, validationCode, validationMessage);
      return;
    }

    Persistence.UpdateLastUsedFolder(canonicalPath);
    SetStatus(
      operationId,
      new ReplayLevelFilePickerStatus
      {
        OperationId = operationId,
        RunId = run.Id,
        State = ReplayLevelFilePickerStates.Selected,
        LevelPath = canonicalPath,
        Message = "Level file selected. It will be verified after loading.",
      }
    );
  }

  private static bool IsCurrent(string operationId)
  {
    lock (Gate)
      return string.Equals(_status?.OperationId, operationId, StringComparison.Ordinal);
  }

  private static void SetError(string operationId, string code, string message)
  {
    string runId;
    lock (Gate)
    {
      if (!string.Equals(_status?.OperationId, operationId, StringComparison.Ordinal))
        return;
      runId = _status.RunId;
    }

    SetStatus(
      operationId,
      new ReplayLevelFilePickerStatus
      {
        OperationId = operationId,
        RunId = runId,
        State = ReplayLevelFilePickerStates.Error,
        ErrorCode = code,
        Message = message,
      }
    );
  }

  private static void SetStatus(string operationId, ReplayLevelFilePickerStatus status)
  {
    lock (Gate)
    {
      if (string.Equals(_status?.OperationId, operationId, StringComparison.Ordinal))
        _status = status;
    }
  }

  private static ReplayLevelFilePickerStatus Clone(ReplayLevelFilePickerStatus status)
  {
    return new ReplayLevelFilePickerStatus
    {
      OperationId = status.OperationId,
      RunId = status.RunId,
      State = status.State,
      LevelPath = status.LevelPath,
      ErrorCode = status.ErrorCode,
      Message = status.Message,
    };
  }

  private static bool Error(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
