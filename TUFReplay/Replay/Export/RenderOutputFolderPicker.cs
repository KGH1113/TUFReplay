using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Shared.Unity;
using UnityEngine;
using UnityFileDialog;

namespace TUFReplay.Replay.Export;

internal static class RenderOutputFolderPicker
{
  private static int _active;

  internal static async Task<string> Choose(CancellationToken cancellation)
  {
    if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
      throw new InvalidOperationException("A folder picker is already open.");
    try
    {
      var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
      UnityMainThread.Post(() =>
      {
        if (cancellation.IsCancellationRequested)
        {
          completion.TrySetCanceled();
          return;
        }
        if (Application.platform == RuntimePlatform.OSXPlayer)
          ThreadPool.QueueUserWorkItem(_ => PickMac(completion));
        else
        {
          try
          {
            var path = FileBrowser.PickFolder(
              Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
              string.Empty,
              Array.Empty<string>(),
              "Select render output folder"
            );
            completion.TrySetResult(string.IsNullOrWhiteSpace(path) ? null : path);
          }
          catch (Exception exception)
          {
            completion.TrySetException(exception);
          }
        }
      });
      // Keep the picker marked active until the OS dialog actually closes.
      return await completion.Task.ConfigureAwait(false);
    }
    finally
    {
      Interlocked.Exchange(ref _active, 0);
    }
  }

  private static void PickMac(TaskCompletionSource<string> completion)
  {
    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/usr/bin/osascript",
          Arguments = "-e \"POSIX path of (choose folder with prompt \\\"Select render output folder\\\")\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        },
      };
      process.Start();
      var path = process.StandardOutput.ReadToEnd().Trim();
      var error = process.StandardError.ReadToEnd();
      process.WaitForExit();
      if (process.ExitCode == 0)
        completion.TrySetResult(path);
      else if (error.Contains("(-128)"))
        completion.TrySetResult(null);
      else
        completion.TrySetException(new InvalidOperationException("Could not open the folder picker."));
    }
    catch (Exception exception)
    {
      completion.TrySetException(exception);
    }
  }
}
