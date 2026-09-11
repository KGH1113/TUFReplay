using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using TUFReplay.Replay.Models;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Protocol;
using TUFReplay.Submission.Transport;

internal static class SubmissionCaptureSuite
{
  public static void RunAll()
  {
    var buffer = new EvidenceCaptureBuffer(2);
    buffer.Write(Input(1)); buffer.Write(Input(2)); buffer.Write(Input(3));
    Require(buffer.Failure == "capture_queue_overflow" && buffer.InputCount == 2, "overflow is terminal");
    Require(TUFReplay.Recording.Capture.SubmissionKeyCount.Count(new List<RecordedInput> {
      new(0, 1, RecordInputFlags.Down), new(1, 1, RecordInputFlags.Down),
      new(2, 1, 0), new(3, 2, RecordInputFlags.Down), new(99, 3, RecordInputFlags.Down),
    }, 10) == 2, "key count excludes releases, repeated keys and post-clear inputs");
    Require(buffer.TryRead(out var first) && first.Input.TimeUs == 1, "first record preserved");
    Require(buffer.TryRead(out var second) && second.Input.TimeUs == 2, "second record preserved");
    buffer.Write(Input(4));
    Require(!buffer.TryRead(out _), "failed capture never resumes");

    var shared = new EvidenceCaptureBuffer();
    var consumer = Task.Run(() => {
      for (int expected = 0; expected < 100000; expected++)
      {
        CaptureRecord record;
        while (!shared.TryRead(out record)) Thread.Yield();
        Require(record.Input.TimeUs == expected, "ring ordering across wraparound");
        Volatile.Write(ref _consumed, expected + 1);
      }
    });
    _consumed = 0;
    for (int i = 0; i < 100000; i++)
    {
      while (i - Volatile.Read(ref _consumed) >= 4096) Thread.Yield();
      shared.Write(Input(i));
    }
    consumer.GetAwaiter().GetResult();
    Require(shared.Failure == null, "bounded concurrent capture");

    var culture = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
      var text = new StringBuilder();
      EvidenceRecordWriter.Append(text, new CaptureRecord(new RecordedHitContext { CurrAngle = 1.25, TimeUs = 456 }));
      var fields = text.ToString().TrimEnd('\n').Split(',');
      Require(fields.Length == 13 && fields[1] == "1.25" && fields[11] == "" && fields[12] == "456",
        "timestamp remains independent of optional judgment and locale");
    }
    finally { CultureInfo.CurrentCulture = culture; }

    var complete = new EvidenceCaptureBuffer();
    for (int i = 0; i < 513; i++) complete.Write(Input(i));
    complete.Complete("{\"version\":1}");
    complete.Write(Input(999));
    var chunker = new EvidenceChunker(complete);
    var chunks = new List<(byte Kind, byte[] Bytes)>();
    while (!chunker.Finished) chunks.AddRange(chunker.Drain());
    Require(chunks.Last().Kind == 2, "metadata follows every capture record");
    Require(chunks.Where(c => c.Kind == 0).Sum(c => Encoding.UTF8.GetString(c.Bytes).Count(x => x == '\n')) == 513,
      "full batches lose no tail records");

    var journal = new UploadJournal(64);
    Require(journal.TryAppend(0, new byte[] { 1, 2, 3 }, out var frame), "frame append");
    Require(BinaryPrimitives.ReadInt64BigEndian(frame.Bytes.AsSpan(8, 8)) == 0 && frame.Bytes[4] == 1, "wire header");
    byte[] original = frame.Bytes.ToArray();
    Require(journal.Pending.Single().Bytes.SequenceEqual(original), "retransmission is byte identical");
    journal.Acknowledge(0); journal.Acknowledge(0);
    Require(journal.PendingBytes == 0, "cumulative ack releases memory");
    bool rejected = false;
    try { journal.Acknowledge(1); } catch (InvalidOperationException) { rejected = true; }
    Require(rejected, "future ack rejected");
    Console.WriteLine("Submission capture, framing and retransmission tests passed.");
  }

  private static int _consumed;
  private static RecordedInput Input(long time) => new(time, 1, RecordInputFlags.Down, 1, 0);
  private static void Require(bool value, string message)
  {
    if (!value) throw new InvalidOperationException(message);
  }
}
