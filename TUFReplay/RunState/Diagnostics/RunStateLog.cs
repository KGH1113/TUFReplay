using System;
using TUFReplay.RunState.Providers;

namespace TUFReplay.RunState.Diagnostics;

/// <summary>Writes run state messages to the mod log, where mod authors look for them.</summary>
internal static class RunStateLog
{
  /// <summary>Replaces the mod log, for tests.</summary>
  internal static Action<string> Sink;

  public static void Write(string message)
  {
    Action<string> sink = Sink;
    if (sink != null)
      sink(message);
    else
      Main.Instance?.Log(message);
  }

  public static void ProviderFailed(RunStateProvider provider, string method, Exception exception)
  {
    Exception cause = exception is RunStateProviderException ? exception.InnerException : exception;
    string message = "[RunState] " + provider.Id + ": " + provider.ProviderTypeName + "." + method + " threw " + cause;
    Action<string> sink = Sink;
    if (sink != null)
      sink(message);
    else
      Main.Instance?.LogException("RunState", new InvalidOperationException(message));
  }
}
