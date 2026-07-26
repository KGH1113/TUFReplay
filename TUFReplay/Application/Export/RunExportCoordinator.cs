using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;
using UnityFileDialog;

namespace TUFReplay.Application.Export;

public static class RunExportCoordinator
{
  private sealed class ExportOperation
  {
    public readonly int Generation;
    public readonly RunRecord Run;
    public readonly LevelSession Level;
    public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
    public readonly TaskCompletionSource<RunExportResult> Completion =
      new TaskCompletionSource<RunExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

    public ExportOperation(int generation, RunRecord run, LevelSession level)
    {
      Generation = generation;
      Run = run;
      Level = level;
    }
  }

  private static readonly object Gate = new object();
  private static ExportOperation _active;
  private static int _generation;

  public static bool IsExporting
  {
    get
    {
      lock (Gate)
        return _active != null;
    }
  }

  public static bool IsExportingRun(string runId)
  {
    lock (Gate)
      return _active != null && _active.Run.Id == runId;
  }

  public static RunExportResult Export(string runId)
  {
    RunRecord run = RunRepository.Get(runId);
    if (run == null)
      return Error(runId, "run_not_found", "Run was not found.");
    LevelSession level = LevelSessionRepository.Get(run.LevelSessionId);
    if (level == null)
      return Error(runId, "export_failed", "The run's level session was not found.");

    ExportOperation operation;
    lock (Gate)
    {
      if (_active != null)
        return Error(runId, "export_busy", "Another run export is already in progress.");
      operation = new ExportOperation(++_generation, run, level);
      _active = operation;
    }

    UnityMainThread.Post(() => BeginSaveOnMainThread(operation));
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
    ExportOperation operation;
    lock (Gate)
    {
      operation = _active;
      _active = null;
      _generation++;
    }
    if (operation == null)
      return;

    operation.Cancellation.Cancel();
    operation.Completion.TrySetResult(
      Error(operation.Run.Id, "export_failed", "The run export was cancelled during shutdown.")
    );
  }

  private static void BeginSaveOnMainThread(ExportOperation operation)
  {
    if (!IsCurrent(operation))
      return;

    if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
    {
      ThreadPool.QueueUserWorkItem(_ => SaveOnMac(operation));
      return;
    }

    string selectedPath;
    try
    {
      selectedPath = FileBrowser.SaveFile(
        null,
        DefaultFileName(operation.Run, operation.Level),
        "TUFReplay run",
        new[] { "tufreplay" },
        "Export run"
      );
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Export] Save dialog failed: " + exception.GetType().Name);
      Complete(operation, Error(operation.Run.Id, "export_failed", "The save dialog could not be opened."));
      return;
    }

    if (string.IsNullOrWhiteSpace(selectedPath))
    {
      Complete(operation, new RunExportResult
      {
        RunId = operation.Run.Id,
        Outcome = RunExportOutcomes.Cancelled,
      });
      return;
    }

    string destinationPath = EnsureExtension(selectedPath);
    _ = Task.Run(() => WriteArchive(operation, destinationPath));
  }

  private static void SaveOnMac(ExportOperation operation)
  {
    string selectedPath = null;
    string error = null;
    bool cancelled = false;
    CancellationToken cancellationToken = operation.Cancellation.Token;
    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/usr/bin/osascript",
          Arguments =
            "-e \"POSIX path of (choose file name with prompt \\\"Export TUFReplay run\\\" default name (system attribute \\\"TUFREPLAY_EXPORT_FILE_NAME\\\"))\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        },
      };
      process.StartInfo.EnvironmentVariables["TUFREPLAY_EXPORT_FILE_NAME"] = DefaultFileName(
        operation.Run,
        operation.Level
      );
      using CancellationTokenRegistration registration = cancellationToken.Register(() =>
      {
        try
        {
          if (!process.HasExited)
            process.Kill();
        }
        catch { }
      });

      cancellationToken.ThrowIfCancellationRequested();
      process.Start();
      string output = process.StandardOutput.ReadToEnd();
      string standardError = process.StandardError.ReadToEnd();
      process.WaitForExit();
      cancellationToken.ThrowIfCancellationRequested();
      if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
        selectedPath = output.Trim();
      else if (standardError.Contains("(-128)"))
        cancelled = true;
      else
        error = "The macOS save dialog failed.";
    }
    catch (OperationCanceledException)
    {
      return;
    }
    catch (Exception exception)
    {
      error = "The macOS save dialog failed: " + exception.GetType().Name;
    }

    if (!IsCurrent(operation))
      return;
    if (error != null)
    {
      Main.Instance?.Log("[Export] " + error);
      Complete(operation, Error(operation.Run.Id, "export_failed", error));
      return;
    }
    if (cancelled || string.IsNullOrWhiteSpace(selectedPath))
    {
      Complete(operation, Cancelled(operation.Run.Id));
      return;
    }

    WriteArchive(operation, EnsureExtension(selectedPath));
  }

  private static void WriteArchive(ExportOperation operation, string destinationPath)
  {
    string directory = Path.GetDirectoryName(destinationPath);
    string fileName = Path.GetFileName(destinationPath);
    string partialPath = Path.Combine(
      string.IsNullOrEmpty(directory) ? "." : directory,
      "." + fileName + "." + Guid.NewGuid().ToString("N") + ".tmp"
    );
    string backupPath = null;
    try
    {
      operation.Cancellation.Token.ThrowIfCancellationRequested();
      RunExportArchiveResult archiveResult;
      using (var stream = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        archiveResult = RunExportArchiveWriter.Write(
          operation.Run.Id,
          stream,
          operation.Cancellation.Token
        );
        stream.Flush(true);
      }

      operation.Cancellation.Token.ThrowIfCancellationRequested();
      if (File.Exists(destinationPath))
      {
        backupPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".replacing";
        File.Move(destinationPath, backupPath);
      }

      try
      {
        File.Move(partialPath, destinationPath);
      }
      catch
      {
        if (backupPath != null && File.Exists(backupPath) && !File.Exists(destinationPath))
          File.Move(backupPath, destinationPath);
        throw;
      }

      DeleteIfExists(backupPath);
      Complete(operation, new RunExportResult
      {
        RunId = operation.Run.Id,
        Outcome = RunExportOutcomes.Exported,
        FileName = fileName,
        ByteLength = new FileInfo(destinationPath).Length,
        IncludedMicrophone = archiveResult.IncludedMicrophone,
      });
    }
    catch (OperationCanceledException)
    {
      Complete(operation, Error(operation.Run.Id, "export_failed", "The run export was cancelled."));
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Export] Run export failed: " + exception.GetType().Name);
      Complete(operation, Error(operation.Run.Id, "export_failed", "The run could not be exported."));
    }
    finally
    {
      DeleteIfExists(partialPath);
      if (backupPath != null && File.Exists(backupPath) && !File.Exists(destinationPath))
        File.Move(backupPath, destinationPath);
      else
        DeleteIfExists(backupPath);
    }
  }

  private static bool IsCurrent(ExportOperation operation)
  {
    lock (Gate)
      return ReferenceEquals(_active, operation) && operation.Generation == _generation;
  }

  private static void Complete(ExportOperation operation, RunExportResult result)
  {
    if (!IsCurrent(operation))
      return;
    operation.Completion.TrySetResult(result);
  }

  private static string EnsureExtension(string path) =>
    path.EndsWith(".tufreplay", StringComparison.OrdinalIgnoreCase) ? path : path + ".tufreplay";

  private static string DefaultFileName(RunRecord run, LevelSession level)
  {
    string song = string.IsNullOrWhiteSpace(level.Song) ? "run" : level.Song.Trim();
    char[] invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':' }).Distinct().ToArray();
    foreach (char character in invalid)
      song = song.Replace(character, '-');
    if (song.Length > 80)
      song = song.Substring(0, 80).Trim();
    return song + " - run-" + run.RunIndex + ".tufreplay";
  }

  private static RunExportResult Error(string runId, string code, string message) =>
    new RunExportResult
    {
      RunId = runId,
      Outcome = RunExportOutcomes.Error,
      ErrorCode = code,
      Message = message,
    };

  private static RunExportResult Cancelled(string runId) =>
    new RunExportResult
    {
      RunId = runId,
      Outcome = RunExportOutcomes.Cancelled,
    };

  private static void DeleteIfExists(string path)
  {
    if (!string.IsNullOrEmpty(path) && File.Exists(path))
      File.Delete(path);
  }
}
