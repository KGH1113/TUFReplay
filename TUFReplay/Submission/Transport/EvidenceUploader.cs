using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Submission.Capture;

namespace TUFReplay.Submission.Transport;

/// <summary>Single consumer owns framing, retransmission and completion. It never touches Unity.</summary>
public sealed class EvidenceUploader
{
  private readonly Func<IUploadConnection> _connect;
  private readonly EvidenceCaptureBuffer _capture;
  private readonly UploadJournal _journal = new UploadJournal();
  private readonly EvidenceChunker _chunker;
  private readonly UploadRecoveryWindow _recovery;
  private readonly Action<UploadProgress> _progress;

  public EvidenceUploader(
    Func<IUploadConnection> connect,
    EvidenceCaptureBuffer capture,
    UploadRecoveryWindow recovery = null,
    Action<UploadProgress> progress = null
  )
  {
    _connect = connect;
    _capture = capture;
    _chunker = new EvidenceChunker(capture);
    _recovery = recovery ?? new UploadRecoveryWindow();
    _progress = progress;
  }

  public async Task Run(CancellationToken cancellation)
  {
    while (true)
    {
      cancellation.ThrowIfCancellationRequested();
      if (_capture.Failure != null)
        throw new UploadRejectedException(_capture.Failure);
      try
      {
        using var connection = _connect();
        using (var deadline = _recovery.Attempt(cancellation))
        {
          Report(UploadProgressKind.Connecting);
          await connection.Connect(deadline.Token).ConfigureAwait(false);
          Report(UploadProgressKind.Connected);
          await connection
            .SendControl(
              new
              {
                type = "hello",
                protocol_version = 1,
                last_acknowledged_sequence = _journal.AcknowledgedSequence,
              },
              deadline.Token
            )
            .ConfigureAwait(false);
          Report(UploadProgressKind.HelloSent);
          var response = await connection.Receive(deadline.Token).ConfigureAwait(false);
          deadline.Token.ThrowIfCancellationRequested();
          _recovery.EnsureWithinDeadline();
          if (UploadControlReader.Apply(response, _journal))
          {
            if (!_chunker.Finished || _journal.AcknowledgedSequence != _journal.LastSequence)
              throw new UploadRejectedException("premature_seal");
            Report(UploadProgressKind.Sealed);
            return;
          }
          Report(UploadProgressKind.Ready, _journal.AcknowledgedSequence);
          _recovery.Recovered();
        }
        if (await SendUntilDisconnected(connection, cancellation).ConfigureAwait(false))
          return;
      }
      catch (Exception error) when (!(error is UploadRejectedException) && !cancellation.IsCancellationRequested)
      {
        TimeSpan delay = _recovery.Failed();
        Report(UploadProgressKind.Reconnecting, delayMilliseconds: (int)delay.TotalMilliseconds);
        await Task.Delay(delay, cancellation).ConfigureAwait(false);
      }
    }
  }

  private async Task<bool> SendUntilDisconnected(IUploadConnection connection, CancellationToken cancellation)
  {
    var heartbeat = Stopwatch.StartNew();
    while (true)
    {
      cancellation.ThrowIfCancellationRequested();
      if (_capture.Failure != null)
      {
        await connection.SendControl(new { type = "fail" }, cancellation).ConfigureAwait(false);
        throw new UploadRejectedException(_capture.Failure);
      }
      // Replay byte-identical unacknowledged frames before draining more captured data.
      foreach (var frame in _journal.Pending.ToArray())
      {
        await connection.SendFrame(frame, cancellation).ConfigureAwait(false);
        Report(UploadProgressKind.FrameSent, frame.Sequence, frame.Bytes.Length);
        if (UploadControlReader.Apply(await connection.Receive(cancellation).ConfigureAwait(false), _journal))
          throw new UploadRejectedException("premature_seal");
        Report(UploadProgressKind.Acknowledged, _journal.AcknowledgedSequence);
      }
      foreach (var chunk in _chunker.Drain())
      {
        if (!_journal.TryAppend(chunk.Kind, chunk.Bytes, out var frame))
          throw new UploadRejectedException("upload_window_overflow");
        // All drained records enter the journal before any I/O can interrupt enumeration.
      }
      if (_journal.PendingBytes > 0)
        continue;
      if (_chunker.Finished)
      {
        await connection
          .SendControl(
            new
            {
              type = "complete",
              final_sequence = _journal.LastSequence,
              input_count = _capture.InputCount,
              hit_context_count = _capture.HitCount,
            },
            cancellation
          )
          .ConfigureAwait(false);
        Report(UploadProgressKind.CompleteSent, _journal.LastSequence);
        if (!UploadControlReader.Apply(await connection.Receive(cancellation).ConfigureAwait(false), _journal))
          throw new UploadRejectedException("seal_receipt_required");
        Report(UploadProgressKind.Sealed, _journal.AcknowledgedSequence);
        return true;
      }
      if (heartbeat.ElapsedMilliseconds >= 1000)
      {
        await connection.SendControl(new { type = "heartbeat" }, cancellation).ConfigureAwait(false);
        if (UploadControlReader.Apply(await connection.Receive(cancellation).ConfigureAwait(false), _journal))
          throw new UploadRejectedException("premature_seal");
        heartbeat.Restart();
      }
      await Task.Delay(50, cancellation).ConfigureAwait(false);
    }
  }

  private void Report(UploadProgressKind kind, long sequence = -1L, int bytes = 0, int delayMilliseconds = 0) =>
    _progress?.Invoke(new UploadProgress(kind, sequence, bytes, delayMilliseconds));
}
