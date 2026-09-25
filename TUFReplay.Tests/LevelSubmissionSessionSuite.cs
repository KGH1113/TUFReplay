using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json.Linq;
using TUFReplay.Replay.Models;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Catalog;
using TUFReplay.Submission.Protocol;
using TUFReplay.Submission.Transport;

internal static class LevelSubmissionSessionSuite
{
  public static void RunAll()
  {
    ChartAdmission().GetAwaiter().GetResult();
    LocalActivityAdmission();
    RapidRestart().GetAwaiter().GetResult();
    RecoverLostResponses().GetAwaiter().GetResult();
    ApprovalTimeout().GetAwaiter().GetResult();
    RetireQueuedAttempts().GetAwaiter().GetResult();
    IdleReconnectAndDeniedStart().GetAwaiter().GetResult();
    QueueAndTerminalBounds();
    Console.WriteLine("Reusable level session, immediate restart and pre-approval capture tests passed.");
  }

  private static LevelSubmissionSession Session(Server server, TimeSpan? timeout = null) =>
    new(
      _ => Task.FromResult<IUploadConnection>(new Wire(server)),
      () => new InstalledLevelContext(42, "file", "hash", "level.adofai"),
      "2.8",
      "test",
      timeout
    );

  private static byte[] ChartHash => Enumerable.Repeat((byte)0x11, 32).ToArray();

  private static async Task ChartAdmission()
  {
    var server = new Server();
    using var session = Session(server);
    var missing = session.Begin(null);
    TestFixture.Assert(
      missing.Finished && missing.Failure == "submission_chart_identity_missing_or_unsupported",
      "Missing runtime identity must fail locally."
    );
    TestFixture.Assert(server.Runs.IsEmpty, "Missing identity must not create a server run.");
    var edited = session.Begin(new byte[32]);
    edited.Capture.Complete("{}"); // Failure must remain visible even after a very short clear.
    await Wait(() => edited.Finished);
    TestFixture.Assert(
      edited.Failure == "submission_chart_gameplay_mismatch" && server.Runs.IsEmpty && server.Frames.IsEmpty,
      "Edited charts must never upload or create runs."
    );
    var good = session.Begin(ChartHash);
    good.Capture.Complete("{}");
    await Wait(() => good.Finished);
    TestFixture.Assert(
      good.State == "sealed" && server.Runs.Count == 1,
      "A new matching attempt can upload after a denied attempt."
    );
  }

  private static void LocalActivityAdmission()
  {
    var id = Guid.NewGuid();
    var pending = new TUFReplay.Submission.Sessions.SubmissionRunLink(id);
    string stored = "unexpected";
    pending.Save(value => stored = value, value => stored = value);
    TestFixture.Assert(stored == null, "Unapproved activity must not expose a Submit run ID.");
    pending.Approve();
    TestFixture.Assert(stored == id.ToString(), "Late approval must link an already saved short run.");
    var early = new TUFReplay.Submission.Sessions.SubmissionRunLink(id);
    early.Approve();
    early.Save(value => stored = value, _ => throw new Exception("Unexpected late update"));
    early.Approve();
    TestFixture.Assert(
      stored == id.ToString(),
      "Approval before activity persistence must remain linked and idempotent."
    );
  }

  private static async Task RapidRestart()
  {
    var server = new Server
    {
      Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
    };
    using var session = Session(server);
    var attempts = Enumerable.Range(0, 6).Select(_ => session.Begin(ChartHash)).ToArray();
    foreach (var attempt in attempts)
      TestFixture.Assert(attempt != null, "Immediate starts must not wait for a server lease.");
    attempts[5].Capture.Write(new RecordedInput(1, 7, RecordInputFlags.Down));
    attempts[5].Capture.Write(new RecordedInput(2, 7, 0));
    attempts[5].Capture.Complete("{}");
    for (int i = 0; i < 5; i++)
      attempts[i].Capture.Invalidate("run_failed");
    await Wait(() => server.Starts > 0);
    TestFixture.Assert(server.Frames.IsEmpty, "No evidence may leave before run_start approval.");
    server.Gate.SetResult(true);
    await Wait(() => attempts.All(attempt => attempt.Finished));
    TestFixture.Assert(
      server.Connections == 1 && server.Runs.Count == 6 && server.Failures == 5,
      "Fail/restart must reuse one socket and preserve independent attempts."
    );
    TestFixture.Assert(
      attempts[5].State == "sealed"
        && server.Frames.Values.Any(bytes => Encoding.UTF8.GetString(bytes).Contains("1,7,1")),
      "Inputs captured before approval must be delivered in order."
    );
    session.Retire();
    await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    TestFixture.Assert(session.Begin(ChartHash) == null, "A retired level cannot admit another run.");
  }

  private static async Task RecoverLostResponses()
  {
    var server = new Server
    {
      LoseStart = true,
      LoseAck = true,
      LoseSeal = true,
    };
    using var session = Session(server);
    var attempt = session.Begin(ChartHash);
    attempt.Capture.Write(new RecordedInput(1, 8, RecordInputFlags.Down));
    attempt.Capture.Complete("{}");
    await Wait(() => attempt.Finished);
    TestFixture.Assert(
      attempt.State == "sealed" && server.Runs.Count == 1 && server.Starts >= 3,
      "Lost start, chunk and seal responses must resume one run rather than issuing duplicates."
    );
    TestFixture.Assert(server.Frames.Count == 2, "Reconnection must not duplicate stored evidence frames.");
  }

  private static async Task ApprovalTimeout()
  {
    var server = new Server
    {
      Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
    };
    using var session = Session(server, TimeSpan.FromMilliseconds(80));
    var attempt = session.Begin(ChartHash);
    attempt.Capture.Write(new RecordedInput(1, 7, RecordInputFlags.Down));
    await Wait(() => attempt.Finished);
    TestFixture.Assert(
      attempt.Capture.Failure == "run_start_timeout",
      "Approval has one bounded deadline from first input."
    );
    TestFixture.Assert(server.Frames.IsEmpty, "A timed-out attempt cannot backfill evidence later.");
    server.Gate.SetResult(true);
  }

  private static void QueueAndTerminalBounds()
  {
    var server = new Server();
    using var gate = new ManualResetEventSlim();
    using var session = new LevelSubmissionSession(
      _ => Task.FromResult<IUploadConnection>(new Wire(server)),
      () =>
      {
        gate.Wait();
        return new InstalledLevelContext(42, "file", "hash", "level.adofai");
      },
      "game",
      "mod"
    );
    var attempts = Enumerable.Range(0, 8).Select(_ => session.Begin(ChartHash)).ToArray();
    TestFixture.Assert(
      attempts.All(attempt => attempt != null) && session.Begin(ChartHash) == null,
      "The complete attempt queue must be bounded."
    );
    for (int i = 0; i <= 8192; i++)
      attempts[0].Capture.Write(new RecordedInput(i, 1, RecordInputFlags.Down));
    TestFixture.Assert(
      attempts[0].Capture.Failure == "capture_queue_overflow",
      "Pre-approval capture overflow abandons only that attempt."
    );
    session.Dispose();
    gate.Set();
    session.Completion.GetAwaiter().GetResult();
    var complete = new EvidenceCaptureBuffer();
    complete.Complete("{}");
    complete.Invalidate("late_fail");
    var failed = new EvidenceCaptureBuffer();
    failed.Invalidate("fail");
    failed.Complete("{}");
    TestFixture.Assert(
      complete.Failure == null && failed.CompletionMeta == null,
      "The first local terminal event must win."
    );
    var wrapped = new UploadFrame(0, 0, new byte[] { 1 }).ForRun(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
    TestFixture.Assert(
      Convert.ToHexString(wrapped.Bytes.AsSpan(4, 16)) == "00112233445566778899AABBCCDDEEFF",
      "Wire UUID endianness must match the server."
    );
  }

  private static async Task RetireQueuedAttempts()
  {
    var server = new Server();
    using var gate = new ManualResetEventSlim();
    using var session = new LevelSubmissionSession(
      _ => Task.FromResult<IUploadConnection>(new Wire(server)),
      () =>
      {
        gate.Wait();
        return new InstalledLevelContext(42, "file", "hash", "level.adofai");
      },
      "game",
      "mod"
    );
    var incomplete = session.Begin(ChartHash);
    var completed = session.Begin(ChartHash);
    completed.Capture.Write(new RecordedInput(1, 7, RecordInputFlags.Down));
    completed.Capture.Complete("{}");
    session.Retire();
    gate.Set();
    await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    TestFixture.Assert(
      incomplete.Capture.Failure == "level_changed" && completed.State == "sealed",
      "Changing levels must abandon every queued incomplete run and drain completed evidence."
    );
  }

  private static async Task IdleReconnectAndDeniedStart()
  {
    var server = new Server { LoseHeartbeat = true, DenyStart = true };
    using var session = Session(server);
    await Wait(() => server.Connections >= 2 && session.State == "ready");
    TestFixture.Assert(server.Runs.IsEmpty, "Idle reconnects must not issue runs.");
    var denied = session.Begin(ChartHash);
    denied.Capture.Write(new RecordedInput(1, 7, RecordInputFlags.Down));
    await Wait(() => denied.Finished);
    TestFixture.Assert(
      denied.Capture.Failure == "level_not_eligible" && server.Frames.IsEmpty,
      "A denied start must abandon its capture without sending evidence."
    );
    server.DenyStart = false;
    var next = session.Begin(ChartHash);
    next.Capture.Write(new RecordedInput(2, 8, RecordInputFlags.Down));
    next.Capture.Complete("{}");
    await Wait(() => next.Finished);
    TestFixture.Assert(
      next.State == "sealed" && next.RunId != denied.RunId,
      "A failed start must not disable subsequent independent attempts."
    );
  }

  private static async Task Wait(Func<bool> ready)
  {
    for (int i = 0; i < 1000; i++)
    {
      if (ready())
        return;
      await Task.Delay(10);
    }
    throw new Exception("Level session test timed out.");
  }

  private sealed class Server
  {
    public int Connections,
      Starts,
      Failures;
    public bool LoseStart,
      LoseAck,
      LoseSeal,
      LoseHeartbeat;
    public volatile bool DenyStart;
    public TaskCompletionSource<bool> Gate;
    public ConcurrentDictionary<string, long> Runs = new();
    public ConcurrentDictionary<string, bool> Sealed = new();
    public ConcurrentDictionary<string, byte[]> Frames = new();
  }

  private sealed class Wire : IUploadConnection
  {
    private readonly Server _server;
    private readonly Queue<JObject> _responses = new();
    private bool _lose;
    private bool _waitApproval;

    public Wire(Server server)
    {
      _server = server;
      Interlocked.Increment(ref server.Connections);
    }

    public Task Connect(CancellationToken cancellation) => Task.CompletedTask;

    public Task SendControl(object control, CancellationToken cancellation)
    {
      var value = JObject.FromObject(control);
      string type = (string)value["type"];
      string id = (string)value["run_id"];
      JObject response;
      switch (type)
      {
        case "session_hello":
          response = new JObject { ["type"] = "session_ready", ["protocol_version"] = 2 };
          break;
        case "heartbeat":
          response = new JObject { ["type"] = "heartbeat" };
          if (_server.LoseHeartbeat)
          {
            _server.LoseHeartbeat = false;
            _lose = true;
          }
          break;
        case "run_start":
          Interlocked.Increment(ref _server.Starts);
          if (
            (int?)value["submission_gameplay_hash_version"] != 1
            || (string)value["submission_gameplay_hash_hex"] != new string('1', 64)
          )
          {
            response = new JObject
            {
              ["type"] = "error",
              ["run_id"] = id,
              ["code"] = "submission_chart_gameplay_mismatch",
              ["terminal"] = true,
            };
            break;
          }
          if (_server.DenyStart)
          {
            response = new JObject
            {
              ["type"] = "error",
              ["run_id"] = id,
              ["code"] = "level_not_eligible",
              ["terminal"] = true,
            };
            break;
          }
          _server.Runs.TryAdd(id, -1);
          response = Ack(id, _server.Sealed.ContainsKey(id) ? "sealed" : "ready");
          _waitApproval = true;
          if (_server.LoseStart)
          {
            _server.LoseStart = false;
            _lose = true;
          }
          break;
        case "run_fail":
          Interlocked.Increment(ref _server.Failures);
          response = Ack(id, "failed");
          break;
        case "run_complete":
          _server.Sealed[id] = true;
          response = Ack(id, "sealed");
          if (_server.LoseSeal)
          {
            _server.LoseSeal = false;
            _lose = true;
          }
          break;
        case "run_heartbeat":
          response = Ack(id, "ack");
          break;
        default:
          throw new Exception("Unexpected session control " + type);
      }
      _responses.Enqueue(response);
      return Task.CompletedTask;
    }

    private JObject Ack(string id, string type) =>
      new()
      {
        ["type"] = type,
        ["run_id"] = id,
        ["acknowledged_sequence"] = _server.Runs[id],
        ["max_chunk_bytes"] = 65536,
        ["heartbeat_interval_ms"] = 10000,
        ["chart_verified"] = true,
      };

    public Task SendFrame(UploadFrame frame, CancellationToken cancellation)
    {
      string id = Guid.ParseExact(Convert.ToHexString(frame.Bytes.AsSpan(4, 16)), "N").ToString();
      long seq = BinaryPrimitives.ReadInt64BigEndian(frame.Bytes.AsSpan(28, 8));
      _server.Frames.TryAdd(id + ":" + seq, frame.Bytes.AsSpan(40).ToArray());
      _server.Runs[id] = seq;
      _responses.Enqueue(Ack(id, "ack"));
      if (_server.LoseAck)
      {
        _server.LoseAck = false;
        _lose = true;
      }
      return Task.CompletedTask;
    }

    public async Task<JObject> Receive(CancellationToken cancellation)
    {
      if (_waitApproval && _server.Gate != null)
      {
        await _server.Gate.Task.WaitAsync(cancellation);
        _waitApproval = false;
      }
      if (_lose)
      {
        _lose = false;
        throw new IOException("lost response");
      }
      return _responses.Dequeue();
    }

    public void Dispose() { }
  }
}
