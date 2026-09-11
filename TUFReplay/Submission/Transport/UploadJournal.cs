using System;
using System.Collections.Generic;
using TUFReplay.Submission.Protocol;

namespace TUFReplay.Submission.Transport;

/// <summary>Bounded retransmission window, owned exclusively by the sender task.</summary>
public sealed class UploadJournal
{
  private readonly Queue<UploadFrame> _pending = new Queue<UploadFrame>();
  private readonly int _byteLimit;
  private int _bytes;
  public long LastSequence { get; private set; } = -1;
  public long AcknowledgedSequence { get; private set; } = -1;
  public int PendingBytes => _bytes;
  public IEnumerable<UploadFrame> Pending => _pending;

  public UploadJournal(int byteLimit = 4 * 1024 * 1024)
  {
    if (byteLimit < 20) throw new ArgumentOutOfRangeException(nameof(byteLimit));
    _byteLimit = byteLimit;
  }

  public bool TryAppend(byte kind, byte[] payload, out UploadFrame frame)
  {
    frame = null;
    if (payload == null || payload.Length + 20 > _byteLimit - _bytes) return false;
    frame = new UploadFrame(kind, LastSequence + 1, payload);
    _pending.Enqueue(frame);
    _bytes += frame.Bytes.Length;
    LastSequence = frame.Sequence;
    return true;
  }

  public void Acknowledge(long sequence)
  {
    if (sequence < AcknowledgedSequence || sequence > LastSequence)
      throw new InvalidOperationException("Server acknowledgement is outside the retained evidence window.");
    while (_pending.Count > 0 && _pending.Peek().Sequence <= sequence)
      _bytes -= _pending.Dequeue().Bytes.Length;
    AcknowledgedSequence = sequence;
  }
}
