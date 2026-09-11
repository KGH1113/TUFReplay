using System.Buffers.Binary;
using Newtonsoft.Json.Linq;
using TUFReplay.Replay.Models;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Protocol;
using TUFReplay.Submission.Transport;

internal static class SubmissionUploadSuite
{
  public static void RunAll()
  {
    var capture = new EvidenceCaptureBuffer();
    capture.Write(new RecordedInput(10, 4, RecordInputFlags.Down));
    capture.Write(new RecordedHitContext {CurrentFloorID = 1, TimeUs = 10, ResolvedHitMargin = 4});
    capture.Complete("{\"version\":1}");
    var server = new FakeServer();
    var now = TimeSpan.Zero;
    server.BeforeDrop = () => now += TimeSpan.FromMinutes(2);
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    new EvidenceUploader(() => new FakeConnection(server), capture, new UploadRecoveryWindow(() => now))
      .Run(deadline.Token).GetAwaiter().GetResult();
    Require(server.Connections == 3, "data ACK loss and seal response loss both reconnect");
    Require(server.Frames.Count == 3, "each input, hit, and metadata chunk is preserved once");
    Require(server.Sealed && server.InputCount == 1 && server.HitCount == 1, "completion retains exact record counts");
    var lateCapture = new EvidenceCaptureBuffer();
    lateCapture.Complete("{\"version\":1}");
    var lateServer = new FakeServer { BeforeReconnect = () => now += TimeSpan.FromSeconds(31) };
    bool expired = false;
    try
    {
      new EvidenceUploader(() => new FakeConnection(lateServer), lateCapture, new UploadRecoveryWindow(() => now))
        .Run(deadline.Token).GetAwaiter().GetResult();
    }
    catch (UploadRejectedException error) when (error.Message == "upload_connection_expired") { expired = true; }
    Require(expired && !lateServer.Sealed, "a successful handshake after 30 seconds cannot complete the run");
    Console.WriteLine("Submission lost-ACK and lost-seal recovery tests passed.");
  }

  private static void Require(bool condition, string message)
  { if (!condition) throw new InvalidOperationException(message); }

  private sealed class FakeServer
  {
    public int Connections;
    public readonly SortedDictionary<long, byte[]> Frames = new();
    public bool LoseAck = true;
    public bool LoseSeal = true;
    public bool Sealed;
    public Action BeforeDrop;
    public Action BeforeReconnect;
    public long InputCount;
    public long HitCount;
    public long Ack => Frames.Count - 1;
  }

  private sealed class FakeConnection(FakeServer server) : IUploadConnection
  {
    private JObject _response;
    private bool _drop;
    public Task Connect(CancellationToken cancellation)
    {
      server.Connections++;
      if (server.Connections > 1) server.BeforeReconnect?.Invoke();
      return Task.CompletedTask;
    }
    public Task SendControl(object control, CancellationToken cancellation)
    {
      var message = JObject.FromObject(control);
      if ((string)message["type"] == "complete")
      {
        Require((long?)message["final_sequence"] == server.Ack, "seal follows acknowledged data");
        server.Sealed = true;
        server.InputCount = (long)message["input_count"]!;
        server.HitCount = (long)message["hit_context_count"]!;
        if (server.LoseSeal) { server.LoseSeal = false; _drop = true; }
      }
      _response = new JObject { ["type"] = server.Sealed ? "sealed" : "ready",
        ["acknowledged_sequence"] = server.Ack, ["max_chunk_bytes"] = 65536, ["heartbeat_interval_ms"] = 10000 };
      return Task.CompletedTask;
    }
    public Task SendFrame(UploadFrame frame, CancellationToken cancellation)
    {
      long sequence = BinaryPrimitives.ReadInt64BigEndian(frame.Bytes.AsSpan(8, 8));
      if (server.Frames.TryGetValue(sequence, out var existing))
        Require(existing.SequenceEqual(frame.Bytes), "duplicate sequence preserves exact bytes");
      else server.Frames.Add(sequence, frame.Bytes.ToArray());
      _response = new JObject { ["type"] = "ack", ["acknowledged_sequence"] = server.Ack };
      if (server.LoseAck) { server.LoseAck = false; _drop = true; }
      return Task.CompletedTask;
    }
    public Task<JObject> Receive(CancellationToken cancellation)
    {
      if (_drop) { _drop = false; server.BeforeDrop?.Invoke(); throw new IOException("Synthetic response loss"); }
      return Task.FromResult(_response ?? throw new InvalidOperationException("Nothing sent"));
    }
    public void Dispose() { }
  }
}
