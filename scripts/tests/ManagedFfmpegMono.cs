using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

// Exercises the built production installer in standalone Unity Mono, without loading the game.
internal static class ManagedFfmpegMono
{
  private static int Main(string[] args)
  {
    try
    {
      Run(args).GetAwaiter().GetResult();
      return 0;
    }
    catch (Exception error)
    {
      Console.Error.WriteLine(error);
      return 1;
    }
  }

  private static async Task Run(string[] args)
  {
    var assembly = Assembly.LoadFrom(args[0]);
    var platformType = assembly.GetType("TUFReplay.Shared.Media.FfmpegPlatform", true);
    var installerType = assembly.GetType("TUFReplay.Shared.Media.ManagedFfmpegInstaller", true);
    var platform = Activator.CreateInstance(
      platformType,
      new object[] { "mono-fixture", "ffmpeg", "https://fixture.invalid/ffmpeg.zip", false, null }
    );
    using (
      var installer = (IDisposable)Activator.CreateInstance(installerType, new object[] { args[1], platform, null })
    )
    {
      await ((Task)installerType.GetProperty("Work").GetValue(installer)).ConfigureAwait(false);
      var method = installerType.GetMethod("AcquireInstallGateAsync", BindingFlags.Instance | BindingFlags.NonPublic);
      if (args.Length > 3)
      {
        using (var contender = await Acquire(method, installer, 150, CancellationToken.None).ConfigureAwait(false))
          Require(contender == null, "a second Mono process entered a held installation gate");
        return;
      }
      var held = await Acquire(method, installer, 2000, CancellationToken.None).ConfigureAwait(false);
      Require(held != null, "first gate acquisition failed");
      try
      {
        using (
          var child = Process.Start(
            new ProcessStartInfo(
              args[2],
              Quote(Assembly.GetExecutingAssembly().Location)
                + " "
                + Quote(args[0])
                + " "
                + Quote(args[1])
                + " "
                + Quote(args[2])
                + " probe"
            )
            {
              UseShellExecute = false,
            }
          )
        )
        {
          if (!child.WaitForExit(12000))
          {
            child.Kill();
            throw new Exception("Mono lock contender timed out");
          }
          Require(child.ExitCode == 0, "cross-process exclusion failed");
        }
        using (var contender = await Acquire(method, installer, 150, CancellationToken.None).ConfigureAwait(false))
          Require(contender == null, "same-process gate contention did not time out");
        using (var cancel = new CancellationTokenSource())
        {
          var pending = Acquire(method, installer, 2000, cancel.Token);
          cancel.Cancel();
          try
          {
            using (await pending.ConfigureAwait(false)) { }
            throw new Exception("Mono gate ignored cancellation");
          }
          catch (OperationCanceledException) { }
        }
        await Task.Yield();
        var released = new TaskCompletionSource<bool>();
        new Thread(() =>
        {
          try
          {
            held.Dispose();
            released.SetResult(true);
          }
          catch (Exception error)
          {
            released.SetException(error);
          }
        }).Start();
        await released.Task.ConfigureAwait(false);
        using (var next = await Acquire(method, installer, 2000, CancellationToken.None).ConfigureAwait(false))
          Require(next != null, "Mono gate could not be reacquired after cross-thread disposal");
      }
      finally
      {
        held.Dispose();
      }
    }
    Console.WriteLine(
      "PASS: production FFmpeg gate under Unity Mono: cross-process exclusion, timeout, cancellation and cross-thread release."
    );
  }

  private static Task<IDisposable> Acquire(
    MethodInfo method,
    object installer,
    int milliseconds,
    CancellationToken token
  )
  {
    return (Task<IDisposable>)method.Invoke(installer, new object[] { TimeSpan.FromMilliseconds(milliseconds), token });
  }

  private static string Quote(string value)
  {
    return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
  }

  private static void Require(bool condition, string message)
  {
    if (!condition)
      throw new Exception(message);
  }
}
