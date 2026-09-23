using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay.Activity.Models;
using TUFReplay.Replay.Models;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Protocol;
using TUFReplay.Submission.Transport;

internal static class SubmissionCaptureSuite
{
  public static void RunAll()
  {
    VerifySubmissionResultSnapshot();
    VerifyPostClearEvidence();
    var buffer = new EvidenceCaptureBuffer(2);
    buffer.Write(Input(1));
    buffer.Write(Input(2));
    buffer.Write(Input(3));
    Require(buffer.Failure == "capture_queue_overflow" && buffer.InputCount == 2, "overflow is terminal");
    Require(
      TUFReplay.Recording.Capture.SubmissionKeyCount.Count(
        new List<RecordedInput>
        {
          new(0, 1, RecordInputFlags.Down),
          new(1, 1, RecordInputFlags.Down),
          new(2, 1, 0),
          new(3, 2, RecordInputFlags.Down),
          new(99, 3, RecordInputFlags.Down),
        },
        10
      ) == 2,
      "key count excludes releases, repeated keys and post-clear inputs"
    );
    Require(buffer.TryRead(out var first) && first.Input.TimeUs == 1, "first record preserved");
    Require(buffer.TryRead(out var second) && second.Input.TimeUs == 2, "second record preserved");
    buffer.Write(Input(4));
    Require(!buffer.TryRead(out _), "failed capture never resumes");

    var shared = new EvidenceCaptureBuffer();
    var consumer = Task.Run(() =>
    {
      for (int expected = 0; expected < 100000; expected++)
      {
        CaptureRecord record;
        while (!shared.TryRead(out record))
          Thread.Yield();
        Require(record.Input.TimeUs == expected, "ring ordering across wraparound");
        Volatile.Write(ref _consumed, expected + 1);
      }
    });
    _consumed = 0;
    for (int i = 0; i < 100000; i++)
    {
      while (i - Volatile.Read(ref _consumed) >= 4096)
        Thread.Yield();
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
      Require(
        fields.Length == 13 && fields[1] == "1.25" && fields[11] == "" && fields[12] == "456",
        "timestamp remains independent of optional judgment and locale"
      );
    }
    finally
    {
      CultureInfo.CurrentCulture = culture;
    }

    var complete = new EvidenceCaptureBuffer();
    for (int i = 0; i < 513; i++)
      complete.Write(Input(i));
    complete.Complete("{\"version\":1}");
    complete.Write(Input(999));
    var chunker = new EvidenceChunker(complete);
    var chunks = new List<(byte Kind, byte[] Bytes)>();
    while (!chunker.Finished)
      chunks.AddRange(chunker.Drain());
    Require(chunks.Last().Kind == 2, "metadata follows every capture record");
    Require(
      chunks.Where(c => c.Kind == 0).Sum(c => Encoding.UTF8.GetString(c.Bytes).Count(x => x == '\n')) == 513,
      "full batches lose no tail records"
    );

    var journal = new UploadJournal(64);
    Require(journal.TryAppend(0, new byte[] { 1, 2, 3 }, out var frame), "frame append");
    Require(BinaryPrimitives.ReadInt64BigEndian(frame.Bytes.AsSpan(8, 8)) == 0 && frame.Bytes[4] == 1, "wire header");
    byte[] original = frame.Bytes.ToArray();
    Require(journal.Pending.Single().Bytes.SequenceEqual(original), "retransmission is byte identical");
    journal.Acknowledge(0);
    journal.Acknowledge(0);
    Require(journal.PendingBytes == 0, "cumulative ack releases memory");
    bool rejected = false;
    try
    {
      journal.Acknowledge(1);
    }
    catch (InvalidOperationException)
    {
      rejected = true;
    }
    Require(rejected, "future ack rejected");
    Console.WriteLine("Submission capture, framing and retransmission tests passed.");
  }

  private static void VerifyPostClearEvidence()
  {
    // Exercise the recorder's actual enqueue path without starting Unity input capture.
    var session = new TUFReplay.Recording.Sessions.RecordingSession();
    var capture = new EvidenceCaptureBuffer();
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    typeof(TUFReplay.Recording.Sessions.RecordingSession).GetField("_evidenceSink", flags)!.SetValue(session, capture);
    session.Data.WonTimeUs = 100;
    session.Data.TerminalTimeUs = 300;
    var addInput = typeof(TUFReplay.Recording.Sessions.RecordingSession).GetMethod("AddInputLocked", flags)!;
    addInput.Invoke(session, new object[] { 100L, 1, RecordInputFlags.Down, 1, 0UL });
    addInput.Invoke(session, new object[] { 200L, 1, (RecordInputFlags)0, 1, 0UL });
    addInput.Invoke(session, new object[] { 250L, 2, RecordInputFlags.Down, 2, 0UL });
    Require(capture.InputCount == 3, "evidence retains releases and new key presses after clear");
    Require(
      capture.TryRead(out _) && capture.TryRead(out var release) && release.Input.TimeUs == 200,
      "post-clear release keeps its recorded timestamp"
    );
    Require(
      TUFReplay.Recording.Capture.SubmissionKeyCount.Count(session.Data.Inputs, 100) == 1,
      "post-clear rendering evidence does not change submitted key count"
    );
    var meta = JObject.Parse(session.Data.ToActivityMetaJson());
    Require(
      (long)meta["wonTimeUs"] == 100 && (long)meta["terminalTimeUs"] == 300,
      "playback termination and scoring termination remain independent"
    );
  }

  private static void VerifySubmissionResultSnapshot()
  {
    var competitiveCounts = EmptyHitCounts();
    SetHitCount(competitiveCounts, "FailOverload", 0);
    SetHitCount(competitiveCounts, "TooEarly", 1);
    SetHitCount(competitiveCounts, "VeryEarly", 2);
    SetHitCount(competitiveCounts, "EarlyPerfect", 3);
    SetHitCount(competitiveCounts, "PerfectMinus", 4);
    SetHitCount(competitiveCounts, "XPerfect", 5);
    SetHitCount(competitiveCounts, "PerfectPlus", 6);
    SetHitCount(competitiveCounts, "Auto", 1);
    SetHitCount(competitiveCounts, "LatePerfect", 7);
    SetHitCount(competitiveCounts, "VeryLate", 8);
    SetHitCount(competitiveCounts, "TooLate", 9);
    SetHitCount(competitiveCounts, "FailMiss", 0);
    Require(
      SubmissionResultSnapshot.TryCreate(
        competitiveCounts,
        RunJudgmentSystem.ModernCompetitive,
        "3.4.1f1",
        1,
        out SubmissionResultSnapshot competitive
      ),
      "competitive result snapshot is valid"
    );
    Require(
      competitive.judgments.SequenceEqual(new[] { 0, 1, 2, 3, 6, 7, 8, 9, 0 }),
      "competitive XPerfect and Auto use the documented nine-slot order"
    );
    Require(
      competitive.perfectMinus == 4
        && competitive.perfectPlus == 6
        && competitive.adofaiVersion == 3
        && competitive.isXPerfectMode
        && competitive.isNoHoldTap,
      "competitive split results retain mode, version, and non-normal hold behavior"
    );

    var classicCounts = EmptyHitCounts();
    SetHitCount(classicCounts, "FailOverload", 0);
    SetHitCount(classicCounts, "TooEarly", 0);
    SetHitCount(classicCounts, "VeryEarly", 0);
    SetHitCount(classicCounts, "EarlyPerfect", 0);
    SetHitCount(classicCounts, "PerfectMinus", 4);
    SetHitCount(classicCounts, "XPerfect", 5);
    SetHitCount(classicCounts, "PerfectPlus", 6);
    SetHitCount(classicCounts, "Auto", 1);
    SetHitCount(classicCounts, "LatePerfect", 0);
    SetHitCount(classicCounts, "VeryLate", 0);
    SetHitCount(classicCounts, "TooLate", 0);
    SetHitCount(classicCounts, "FailMiss", 0);
    Require(
      SubmissionResultSnapshot.TryCreate(
        classicCounts,
        RunJudgmentSystem.ModernClassic,
        "v3.3.1",
        0,
        out SubmissionResultSnapshot classic
      ),
      "modern classic result snapshot is valid"
    );
    Require(
      classic.judgments[4] == 16
        && classic.perfectMinus == 0
        && classic.perfectPlus == 0
        && classic.adofaiVersion == 2
        && !classic.isXPerfectMode
        && !classic.isNoHoldTap,
      "modern classic margins collapse to the single Perfect slot"
    );

    var legacyCounts = EmptyHitCounts();
    SetHitCount(legacyCounts, "FailOverload", 0);
    SetHitCount(legacyCounts, "TooEarly", 0);
    SetHitCount(legacyCounts, "VeryEarly", 0);
    SetHitCount(legacyCounts, "EarlyPerfect", 0);
    SetHitCount(legacyCounts, "Perfect", 9);
    SetHitCount(legacyCounts, "Auto", 1);
    SetHitCount(legacyCounts, "LatePerfect", 0);
    SetHitCount(legacyCounts, "VeryLate", 0);
    SetHitCount(legacyCounts, "TooLate", 0);
    SetHitCount(legacyCounts, "FailMiss", 0);
    Require(
      SubmissionResultSnapshot.TryCreate(
        legacyCounts,
        RunJudgmentSystem.Legacy,
        "2.8.1",
        0,
        out SubmissionResultSnapshot legacy
      ),
      "legacy result snapshot is valid"
    );
    Require(
      legacy.judgments[4] == 10 && legacy.adofaiVersion == 1,
      "legacy Perfect and Auto use the single Perfect slot"
    );

    Require(
      !SubmissionResultSnapshot.TryCreate(legacyCounts, RunJudgmentSystem.Legacy, "1.9.9", 0, out _),
      "unsupported game versions abort result capture"
    );
    Require(
      !SubmissionResultSnapshot.TryCreate(legacyCounts, RunJudgmentSystem.Legacy, "2.8.1", 9, out _),
      "unknown hold settings abort result capture"
    );

    var metadata = JObject.Parse(new RecordedRunPayload { SubmissionResult = competitive }.ToActivityMetaJson());
    Require(
      (int)metadata["submissionResult"]["version"] == 1
        && ((JArray)metadata["submissionResult"]["judgments"]).Count == 9
        && (bool)metadata["submissionResult"]["isNoHoldTap"],
      "completed metadata serializes the version-1 result snapshot"
    );
  }

  private static Dictionary<string, int> EmptyHitCounts()
  {
    return new Dictionary<string, int>(StringComparer.Ordinal);
  }

  private static void SetHitCount(Dictionary<string, int> counts, string name, int count)
  {
    counts[name] = count;
  }

  private static int _consumed;

  private static RecordedInput Input(long time) => new(time, 1, RecordInputFlags.Down, 1, 0);

  private static void Require(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
