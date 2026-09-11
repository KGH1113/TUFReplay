using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Protocol;

namespace TUFReplay.Submission.Transport;

/// <summary>Drains a bounded batch, preserving complete CSV records in each chunk.</summary>
public sealed class EvidenceChunker
{
  private readonly EvidenceCaptureBuffer _capture;
  private readonly StringBuilder[] _text = {
    new StringBuilder(18000), new StringBuilder(18000), new StringBuilder(),
    new StringBuilder(18000), new StringBuilder(18000), new StringBuilder(18000),
  };
  private bool _metaWritten;
  private readonly Stopwatch _flush = Stopwatch.StartNew();
  public bool Finished => _metaWritten;

  public EvidenceChunker(EvidenceCaptureBuffer capture) => _capture = capture;

  public IEnumerable<(byte Kind, byte[] Bytes)> Drain()
  {
    string completedMetadata = _capture.CompletionMeta;
    int count = 0;
    while (count++ < 256 && _capture.TryRead(out var record))
    {
      var target = _text[record.Kind];
      EvidenceRecordWriter.Append(target, record);
      if (target.Length >= 16000) yield return Flush(record.Kind);
    }
    if (completedMetadata != null || _flush.ElapsedMilliseconds >= 250)
    {
      for (byte kind = 0; kind < 6; kind++)
        if (_text[kind].Length > 0) yield return Flush(kind);
      _flush.Restart();
    }
    // A full batch may leave records in the ring. Finish only after an empty read.
    if (count <= 256 && completedMetadata != null && !_metaWritten)
    {
      byte[] metadata = Encoding.UTF8.GetBytes(completedMetadata);
      if (metadata.Length > 256 * 1024) throw new InvalidOperationException("Oversized metadata.");
      for (int offset = 0; offset < metadata.Length; offset += 16000)
      {
        var fragment = new byte[Math.Min(16000, metadata.Length - offset)];
        Array.Copy(metadata, offset, fragment, 0, fragment.Length);
        yield return (2, fragment);
      }
      _metaWritten = true;
    }
  }

  private (byte Kind, byte[] Bytes) Flush(byte kind)
  {
    var bytes = Encoding.UTF8.GetBytes(_text[kind].ToString());
    _text[kind].Clear();
    return (kind, bytes);
  }
}
