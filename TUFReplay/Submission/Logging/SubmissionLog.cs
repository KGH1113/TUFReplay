using System;
using TUFReplay.Submission.Transport;

namespace TUFReplay.Submission.Logging;

/// <summary>Submission diagnostics written to the Unity Mod Manager log.</summary>
internal static class SubmissionLog
{
  internal static void Publish(string message)
  {
    if (string.IsNullOrWhiteSpace(message))
      return;
    Main.Instance?.Log("[AutoSubmission] " + message);
  }

  internal static void Publish(Guid runId, UploadProgress progress)
  {
    if (progress.Kind == UploadProgressKind.FrameSent
      || progress.Kind == UploadProgressKind.Acknowledged
      || progress.Kind == UploadProgressKind.HelloSent)
      return;

    string run = runId.ToString("N").Substring(0, 8);
    switch (progress.Kind)
    {
      case UploadProgressKind.Connecting:
        Publish("[" + run + "] WS connecting");
        break;
      case UploadProgressKind.Connected:
        Publish("[" + run + "] WS connected");
        break;
      case UploadProgressKind.Ready:
        Publish("[" + run + "] server ready");
        break;
      case UploadProgressKind.Reconnecting:
        Publish("[" + run + "] network lost; retry in " + progress.DelayMilliseconds + " ms (30 s window)");
        break;
      case UploadProgressKind.CompleteSent:
        Publish("[" + run + "] level finished; complete sent");
        break;
      case UploadProgressKind.Sealed:
        Publish("[" + run + "] server sealed; ready for web submission");
        break;
    }
  }
}
