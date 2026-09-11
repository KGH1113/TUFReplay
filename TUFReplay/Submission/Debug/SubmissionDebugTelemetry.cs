using System;
using System.Collections.Concurrent;
using System.Threading;
using TUFReplay.Submission.Transport;

namespace TUFReplay.Submission.Debug;

/// <summary>Thread-safe, bounded handoff from submission workers to the Unity debug HUD.</summary>
internal static class SubmissionDebugTelemetry
{
  internal readonly struct Message
  {
    internal string Text { get; }
    internal bool IsTransfer { get; }

    internal Message(string text, bool isTransfer)
    {
      Text = text;
      IsTransfer = isTransfer;
    }
  }

  private const int Capacity = 32;
  private static readonly ConcurrentQueue<Message> Events = new ConcurrentQueue<Message>();
  private static int _count;

  internal static void Publish(string message, bool isTransfer = false)
  {
    if (string.IsNullOrWhiteSpace(message))
      return;

    Events.Enqueue(new Message(DateTime.Now.ToString("HH:mm:ss") + "  " + message, isTransfer));
    int count = Interlocked.Increment(ref _count);
    while (count > Capacity && Events.TryDequeue(out _))
      count = Interlocked.Decrement(ref _count);
  }

  internal static void Publish(Guid runId, UploadProgress progress)
  {
    string run = runId.ToString("N").Substring(0, 8);
    switch (progress.Kind)
    {
      case UploadProgressKind.Connecting:
        Publish("[" + run + "] WS connecting");
        break;
      case UploadProgressKind.Connected:
        Publish("[" + run + "] WS connected");
        break;
      case UploadProgressKind.HelloSent:
        Publish("[" + run + "] hello sent");
        break;
      case UploadProgressKind.Ready:
        Publish("[" + run + "] server ready, ACK " + progress.Sequence);
        break;
      case UploadProgressKind.FrameSent:
        Publish("[" + run + "] buffer sent: seq " + progress.Sequence + ", " + progress.Bytes + " B", true);
        break;
      case UploadProgressKind.Acknowledged:
        Publish("[" + run + "] ACK " + progress.Sequence, true);
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

  internal static bool TryTake(out Message message)
  {
    if (!Events.TryDequeue(out message))
      return false;
    Interlocked.Decrement(ref _count);
    return true;
  }
}
