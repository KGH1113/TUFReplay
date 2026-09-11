using System;
using System.Threading;
using TUFReplay.Recording.Capture;
using TUFReplay.Replay.Models;

namespace TUFReplay.Submission.Capture;

/// <summary>One serialized producer (RecordingSession) and one background consumer.
/// The fixed value-type ring retains at most Capacity records, even while offline.</summary>
public sealed class EvidenceCaptureBuffer : IRecordingEvidenceSink
{
  private readonly CaptureRecord[] _records;
  private long _written;
  private long _read;
  private string _failure;
  private string _completionMeta;
  public int Capacity => _records.Length;
  public string Failure => Volatile.Read(ref _failure);
  public string CompletionMeta => Volatile.Read(ref _completionMeta);
  public long InputCount { get; private set; }
  public long HitCount { get; private set; }

  public EvidenceCaptureBuffer(int capacity = 8192)
  {
    if (capacity < 1 || capacity > 8192) throw new ArgumentOutOfRangeException(nameof(capacity));
    _records = new CaptureRecord[capacity];
  }

  public void Write(RecordedInput input)
  {
    if (TryWrite(new CaptureRecord(input))) InputCount++;
  }

  public void Write(RecordedHitContext hit)
  {
    if (TryWrite(new CaptureRecord(hit))) HitCount++;
  }

  public void Write(RecordingStateRecord state) => TryWrite(new CaptureRecord(state));

  private bool TryWrite(CaptureRecord record)
  {
    if (Failure != null || CompletionMeta != null) return false;
    long position = _written;
    if (position - Volatile.Read(ref _read) >= Capacity)
    {
      Invalidate("capture_queue_overflow");
      return false;
    }
    _records[position % Capacity] = record;
    Volatile.Write(ref _written, position + 1);
    return true;
  }

  public bool TryRead(out CaptureRecord record)
  {
    long position = _read;
    if (position == Volatile.Read(ref _written)) { record = default; return false; }
    record = _records[position % Capacity];
    Volatile.Write(ref _read, position + 1);
    return true;
  }

  public void Complete(string metadata)
  {
    if (string.IsNullOrEmpty(metadata)) { Invalidate("missing_metadata"); return; }
    Interlocked.CompareExchange(ref _completionMeta, metadata, null);
  }

  public void Invalidate(string reason) => Interlocked.CompareExchange(ref _failure, reason, null);
}
