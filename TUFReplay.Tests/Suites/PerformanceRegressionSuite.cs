using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.Database;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Threading;
using TUFReplay.Unity.ReplayTimeline;
using static TestFixture;

internal static class PerformanceRegressionSuite
{
  public static void RunAll(string root)
  {
    TestBackgroundQueueOrdering();
    TestActivityWritesDoNotWaitForDatabase(root);
    TestRetainedInputSource();
    TestLargeTimelineBuckets();
    TestHashBytesAndAllocations();
    TestChunkedCsvEncoding();
    TestHitContextCsvMatchesStringFormatting();
    TestIndexedInputSeek();
    TestLevelFileVerificationReuse(root);
    TestHarmonyPatchesDoNotRequestBoxedArguments();
    TestGameplayIdleGateDefersWork();
  }

  private static void TestGameplayIdleGateDefersWork()
  {
    using var ran = new ManualResetEventSlim();
    GameplayIdleGate.SetJudging(true);
    try
    {
      GameplayIdleGate.Run(ran.Set);
      Assert(!ran.Wait(TimeSpan.FromMilliseconds(100)), "Deferred work ran while ADOFAI was judging input.");
      Assert(GameplayIdleGate.DeferredCount == 1, "Deferred work was not retained.");
    }
    finally
    {
      GameplayIdleGate.SetJudging(false);
    }
    Assert(ran.Wait(TimeSpan.FromSeconds(5)), "Deferred work did not run after judging stopped.");

    using var immediate = new ManualResetEventSlim();
    GameplayIdleGate.Run(immediate.Set);
    Assert(immediate.Wait(TimeSpan.FromSeconds(5)), "Idle work was held.");

    using var released = new ManualResetEventSlim();
    GameplayIdleGate.SetJudging(true);
    GameplayIdleGate.Run(released.Set);
    GameplayIdleGate.Release();
    Assert(released.Wait(TimeSpan.FromSeconds(5)), "Releasing the gate kept deferred work.");
    Console.WriteLine("PASS optional background work waits for judged gameplay to end");
  }

  // Harmony builds and boxes an object[] on every call when a patch asks for __args.
  // Patched methods such as scrPlayer.Hit run inside ADOFAI's async-input judgment loop.
  private static void TestHarmonyPatchesDoNotRequestBoxedArguments()
  {
    string assemblyPath = typeof(LevelFileVerificationCache).Assembly.Location;
    using FileStream stream = File.OpenRead(assemblyPath);
    using var image = new System.Reflection.PortableExecutable.PEReader(stream);
    System.Reflection.Metadata.MetadataReader metadata =
      System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(image);
    foreach (System.Reflection.Metadata.MethodDefinitionHandle methodHandle in metadata.MethodDefinitions)
    {
      System.Reflection.Metadata.MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
      foreach (System.Reflection.Metadata.ParameterHandle parameterHandle in method.GetParameters())
      {
        System.Reflection.Metadata.Parameter parameter = metadata.GetParameter(parameterHandle);
        Assert(
          !metadata.StringComparer.Equals(parameter.Name, "__args"),
          "A Harmony patch requests __args: " + metadata.GetString(method.Name)
        );
      }
    }
    Console.WriteLine("PASS Harmony patches read arguments without boxing them");
  }

  private static void TestLevelFileVerificationReuse(string root)
  {
    string path = Path.Combine(root, "verified-chart.adofai");
    File.WriteAllText(path, "{\"angleData\":[0,90]}");
    int verifications = 0;
    int reads = 0;
    var cache = new LevelFileVerificationCache(
      (string file, out string source, out byte[] hash) =>
      {
        verifications++;
        source = File.ReadAllText(file);
        hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return true;
      },
      file =>
      {
        reads++;
        return File.ReadAllText(file);
      },
      capacity: 4
    );

    Assert(cache.TryRead(path, out string first, out byte[] firstHash), "The first chart read failed.");
    Assert(cache.TryRead(path, out string second, out byte[] secondHash), "The cached chart read failed.");
    Assert(verifications == 1 && reads == 1, "An unchanged chart was fully verified again.");
    Assert(first == second && firstHash.SequenceEqual(secondHash), "The cached chart read changed its result.");
    secondHash[0] ^= 0xff;
    Assert(cache.TryRead(path, out _, out byte[] thirdHash), "The cached chart read failed after hash mutation.");
    Assert(thirdHash.SequenceEqual(firstHash), "A caller could mutate the cached hash.");

    File.WriteAllText(path, "{\"angleData\":[0,180]}");
    File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
    Assert(cache.TryRead(path, out string changed, out _), "The modified chart read failed.");
    Assert(verifications == 2 && changed.Contains("180"), "A modified chart reused the old verification.");

    File.Delete(path);
    Assert(!cache.TryRead(path, out _, out _) && verifications == 2, "A deleted chart was served.");
    Console.WriteLine("PASS unchanged level files reuse their verified gameplay hash");
  }

  private static void TestBackgroundQueueOrdering()
  {
    using var entered = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    int caller = Environment.CurrentManagedThreadId;
    int worker = caller;
    var order = new List<int>();
    int failures = 0;
    var queue = new SerialBackgroundQueue(_ => failures++);
    queue.Enqueue(() =>
    {
      worker = Environment.CurrentManagedThreadId;
      entered.Set();
      if (!release.Wait(TimeSpan.FromSeconds(5)))
        throw new TimeoutException();
      order.Add(1);
    });
    try
    {
      Assert(entered.Wait(TimeSpan.FromSeconds(5)), "Background writer did not start.");
      queue.Enqueue(() => throw new IOException("intentional failure"));
      queue.Enqueue(() => order.Add(2));
      Assert(worker != caller && !queue.Completion.IsCompleted, "Persistence ran inline or waited on its caller.");
    }
    finally
    {
      release.Set();
    }
    queue.Completion.GetAwaiter().GetResult();
    Assert(order.SequenceEqual(new[] { 1, 2 }) && failures == 1, "A failed write reordered or stopped later writes.");
  }

  private static void TestActivityWritesDoNotWaitForDatabase(string root)
  {
    PropertyInfo pathProperty = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    string previousPath = Database.DbPath;
    pathProperty.SetValue(null, Path.Combine(root, "async-activity.sqlite"));
    var tracker = new RecordingActivityTracker();
    try
    {
      using (SqliteConnection setup = Database.OpenConnection())
        ActivitySchema.Ensure(setup);
      string chart = Path.Combine(root, "async-level.adofai");
      File.WriteAllText(chart, "{\"settings\":{\"song\":\"Song\",\"artist\":\"Artist\",\"author\":\"Author\"}}");
      var payload = new RecordedRunPayload
      {
        StartedAtUtc = DateTime.UtcNow.ToString("O"),
        GameplayHash = new byte[32],
        GameplayHashVersion = GameplayChartHash.Version,
      };
      payload.Inputs.Add(new RecordedInput(100, 32, RecordInputFlags.Down));
      RunRecord first;
      using (SqliteConnection locked = Database.OpenConnection())
      using (SqliteTransaction transaction = locked.BeginTransaction())
      {
        var elapsed = Stopwatch.StartNew();
        Assert(
          tracker.OpenLevel(chart, null, 10, payload.GameplayHash, payload.GameplayHashVersion),
          "Level draft failed."
        );
        first = tracker.CreateRunDraft(payload, 0, 10);
        first.LastTile = 10;
        first.Result = "cleared";
        tracker.SaveRun(first, payload.CompletedSnapshot(), null, null);
        RunRecord second = tracker.CreateRunDraft(payload, 0, 10);
        second.LastTile = 1;
        second.Result = "failed";
        tracker.SaveRun(second, payload.CompletedSnapshot(), null, null);
        Assert(second.RunIndex == 1, "A queued retry reused the previous run index.");
        tracker.StopAppSession();
        Assert(elapsed.Elapsed < TimeSpan.FromSeconds(1), "Game-side recording waited for a locked database.");
        transaction.Rollback();
      }
      Assert(
        tracker.PendingWrites.Wait(TimeSpan.FromSeconds(10)),
        "Activity writer did not finish after the database unlocked."
      );
      StoredReplayRun stored = RunRepository.GetReplayRun(first.Id);
      Assert(
        stored != null && Encoding.UTF8.GetString(stored.InputCsv) == "100,32,1,-1,0\n",
        "Queued replay bytes changed."
      );
      using SqliteConnection verify = Database.OpenConnection();
      using SqliteCommand command = verify.CreateCommand();
      command.CommandText = "SELECT count(*) FROM runs";
      Assert(Convert.ToInt32(command.ExecuteScalar()) == 2, "Closing the session lost a pending run.");
      command.CommandText = "SELECT count(*) FROM level_sessions WHERE closed_at_utc IS NOT NULL";
      Assert(Convert.ToInt32(command.ExecuteScalar()) == 1, "Queued session close overtook its runs.");
    }
    finally
    {
      tracker.PendingWrites.GetAwaiter().GetResult();
      pathProperty.SetValue(null, previousPath);
    }
  }

  private static void TestRetainedInputSource()
  {
    FieldInfo sourceField = typeof(RecordInputTracker).GetField(
      "EventSource",
      BindingFlags.NonPublic | BindingFlags.Static
    );
    var previous = (INativeInputEventSource)sourceField.GetValue(null);
    var source = new FakeInputSource();
    sourceField.SetValue(null, source);
    try
    {
      RecordInputTracker.Reset();
      RecordInputTracker.PrepareSource();
      for (int i = 0; i < 5; i++)
      {
        RecordInputTracker.StartCapture();
        RecordInputTracker.StopCapture(null);
        RecordInputTracker.Reset();
      }
      Assert(
        source.Starts == 1 && source.Stops == 0 && source.IsRunning,
        "Retry restarted or joined the native input source."
      );
    }
    finally
    {
      RecordInputTracker.Reset();
      sourceField.SetValue(null, previous);
      source.Stop();
    }
  }

  private static void TestLargeTimelineBuckets()
  {
    var markers = new ReplayJudgmentMarker[100_000];
    for (int i = 0; i < markers.Length; i++)
      markers[i] = new ReplayJudgmentMarker { normalizedTime = 0.5f, judgment = ReplayJudgmentKind.Perfect };
    markers[0].judgment = ReplayJudgmentKind.Miss;
    var columns = new int[ReplayJudgmentMarkerBuckets.MaximumColumns];
    ReplayJudgmentMarkerBuckets.Fill(markers, int.MaxValue, columns);
    Assert(
      columns.Count(value => value >= 0) == 1 && columns[columns.Length / 2] == (int)ReplayJudgmentKind.Miss,
      "Dense timeline markers hid the most severe judgment."
    );
    ReplayJudgmentMarkerBuckets.Fill(markers, 1 << (int)ReplayJudgmentKind.Perfect, columns);
    Assert(
      columns[columns.Length / 2] == (int)ReplayJudgmentKind.Perfect,
      "Bucket aggregation ignored judgment filters."
    );
    Assert(columns.Length * 4 < 65000, "A dense timeline can exceed Unity's vertex limit.");
  }

  private static void TestHashBytesAndAllocations()
  {
    using (var writer = new GameplayChartHashCanonicalWriter())
    {
      writer.WriteAngles(new[] { 0f, 1f, -1f });
      byte[] legacyBytes = { 0, 0, 0, 3, 0, 0, 0, 0, 63, 128, 0, 0, 191, 128, 0, 0 };
      Assert(writer.ComputeSha256Hash().SequenceEqual(SHA256.HashData(legacyBytes)), "Gameplay hash identity changed.");
    }
    var angles = new float[100_000];
    using var large = new GameplayChartHashCanonicalWriter();
    long before = GC.GetAllocatedBytesForCurrentThread();
    large.WriteAngles(angles);
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert(allocated < angles.Length * 12L, "Chart hashing still allocates a temporary array per angle.");
  }

  // The pooled UTF-8 writer must keep the exact bytes of the previous
  // StringBuilder + ToString("R") serialization, across 64 KiB chunk boundaries.
  private static void TestHitContextCsvMatchesStringFormatting()
  {
    double[] doubles =
    {
      0d,
      -0d,
      1d / 3d,
      -Math.PI,
      double.Epsilon,
      double.MaxValue,
      double.MinValue,
      1e-300,
      123456789.123456789,
      double.NaN,
      double.PositiveInfinity,
      double.NegativeInfinity,
    };
    float[] floats = { 0f, -0f, 1f / 3f, float.Epsilon, float.MaxValue, float.MinValue, float.NaN, 0.1f };
    var payload = new RecordedRunPayload();
    var random = new Random(1234);
    for (int i = 0; i < 4_000; i++)
    {
      payload.HitContexts.Add(
        new RecordedHitContext
        {
          CurrentFloorID = i % 7 == 0 ? int.MinValue + i : i,
          CurrAngle = i < doubles.Length ? doubles[i] : random.NextDouble() * 1e6 - 5e5,
          OverloadCounter = i < floats.Length ? floats[i] : (float)random.NextDouble(),
          NoFailHit = i % 2 == 0,
          IsAuto = i % 3 == 0,
          NextFloorAuto = i % 5 == 0,
          CachedAngle = doubles[i % doubles.Length],
          TargetExitAngle = random.NextDouble() * Math.PI,
          MidspinInfiniteMargin = i % 11 == 0,
          RDCAuto = i % 13 == 0,
          CurFreeRoamSection = -i,
          ResolvedHitMargin = i % 4 == 0 ? null : i % 9,
          TimeUs = i % 8 == 1 ? null : long.MaxValue - i,
        }
      );
      payload.Inputs.Add(
        new RecordedInput(long.MinValue + i, i, (RecordInputFlags)(ushort)(i * 977), -i, ulong.MaxValue - (ulong)i)
      );
    }

    byte[] hits = payload.ToHitContextCsvBytes();
    byte[] inputs = payload.ToInputCsvBytes();
    Assert(hits.Length > 3 * 64 * 1024, "The hit CSV fixture does not cross several chunks.");
    Assert(hits.SequenceEqual(ReferenceHitCsv(payload)), "Hit-context CSV bytes changed.");
    Assert(inputs.SequenceEqual(ReferenceInputCsv(payload)), "Input CSV bytes changed.");
    Console.WriteLine("PASS pooled replay CSV writer keeps the previous bytes");
  }

  private static byte[] ReferenceInputCsv(RecordedRunPayload payload)
  {
    var builder = new StringBuilder();
    foreach (RecordedInput input in payload.Inputs)
    {
      builder
        .Append(input.TimeUs)
        .Append(',')
        .Append(input.Key)
        .Append(',')
        .Append((ushort)input.Flags)
        .Append(',')
        .Append(input.NativeCode)
        .Append(',')
        .Append(input.NativeFlags)
        .Append('\n');
    }
    return new UTF8Encoding(false).GetBytes(builder.ToString());
  }

  private static byte[] ReferenceHitCsv(RecordedRunPayload payload)
  {
    var invariant = System.Globalization.CultureInfo.InvariantCulture;
    var builder = new StringBuilder();
    foreach (RecordedHitContext hit in payload.HitContexts)
    {
      builder
        .Append(hit.CurrentFloorID)
        .Append(',')
        .Append(hit.CurrAngle.ToString("R", invariant))
        .Append(',')
        .Append(hit.OverloadCounter.ToString("R", invariant))
        .Append(',')
        .Append(hit.NoFailHit ? '1' : '0')
        .Append(',')
        .Append(hit.IsAuto ? '1' : '0')
        .Append(',')
        .Append(hit.NextFloorAuto ? '1' : '0')
        .Append(',')
        .Append(hit.CachedAngle.ToString("R", invariant))
        .Append(',')
        .Append(hit.TargetExitAngle.ToString("R", invariant))
        .Append(',')
        .Append(hit.MidspinInfiniteMargin ? '1' : '0')
        .Append(',')
        .Append(hit.RDCAuto ? '1' : '0')
        .Append(',')
        .Append(hit.CurFreeRoamSection);
      if (hit.ResolvedHitMargin.HasValue)
      {
        builder.Append(',').Append(hit.ResolvedHitMargin.Value);
        if (hit.TimeUs.HasValue)
          builder.Append(',').Append(hit.TimeUs.Value);
      }
      builder.Append('\n');
    }
    return new UTF8Encoding(false).GetBytes(builder.ToString());
  }

  private static void TestChunkedCsvEncoding()
  {
    var payload = new RecordedRunPayload();
    for (int i = 0; i < 10_000; i++)
      payload.Inputs.Add(new RecordedInput(i, 32, RecordInputFlags.Down));
    string expected = string.Concat(Enumerable.Range(0, 10_000).Select(i => i + ",32,1,-1,0\n"));
    Assert(
      payload.ToInputCsvBytes().SequenceEqual(Encoding.UTF8.GetBytes(expected)),
      "CSV chunk boundaries changed the recording."
    );
    RecordedRunPayload snapshot = payload.CompletedSnapshot();
    payload.EndedAtUtc = "changed";
    Assert(
      snapshot.EndedAtUtc == null && ReferenceEquals(snapshot.Inputs, payload.Inputs),
      "Snapshot copied a long input buffer or mutable scalar metadata."
    );
  }

  private static void TestIndexedInputSeek()
  {
    var inputs = new List<RecordedInput>();
    for (int i = 0; i < 100_000; i++)
      inputs.Add(
        new RecordedInput(
          i / 4,
          32 + i % 8,
          RecordInputFlags.Async | ((i / 8) % 2 == 0 ? RecordInputFlags.Down : 0),
          i % 8,
          (ulong)i
        )
      );
    var scheduler = new ReplayInputScheduler(inputs);
    foreach (long time in new long[] { -1, 0, 511, 512, 24_999, 9_999, 1, 25_000 })
    {
      var expected = new List<NativeInputKey>();
      int count = 0;
      foreach (RecordedInput input in inputs)
      {
        if (input.TimeUs > time)
          break;
        var key = new NativeInputKey(input.Key, input.ExtendedKey, input.NativeCode, input.NativeFlags);
        if (input.Down)
        {
          if (!expected.Contains(key))
            expected.Add(key);
        }
        else
          expected.Remove(key);
        count++;
      }
      List<NativeInputKey> actual = scheduler.SeekToNativeState(time);
      Assert(
        actual.SequenceEqual(expected) && scheduler.NextIndex == count,
        "Indexed seek changed held-key state or chord order."
      );
      for (int i = 0; i < actual.Count; i++)
        Assert(
          actual[i].NativeCode == expected[i].NativeCode && actual[i].NativeFlags == expected[i].NativeFlags,
          "Seek lost native key metadata."
        );
      Assert(
        scheduler.LastSeekScannedEvents < ReplayInputScheduler.SeekCheckpointInterval,
        "Seek still scans the whole recording."
      );
    }
  }

  private sealed class FakeInputSource : INativeInputEventSource
  {
    public int Starts;
    public int Stops;
    public string Name => "test";
    public bool IsRunning { get; private set; }
    public bool UsesExtendedKeyState => false;
    public IReadOnlyList<int> SnapshotKeyCodes => Array.Empty<int>();

    public void Start(Action<NativeInputTransition> onTransition)
    {
      Starts++;
      IsRunning = true;
    }

    public void Stop()
    {
      Stops++;
      IsRunning = false;
    }

    public void RefreshPhysicalState() { }

    public bool TryGetPhysicalKeyState(int keyCode, out bool isDown)
    {
      isDown = false;
      return true;
    }

    public long ConsumeDroppedEvents() => 0;

    public NativeInputSourceDiagnostics GetDiagnostics() => default;
  }
}
