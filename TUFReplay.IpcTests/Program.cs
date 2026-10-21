using System.Reflection;
using System.Text;
using AdofaiIpc;
using AdofaiIpc.Contracts;
using AdofaiIpc.Core;
using AdofaiIpc.Server;
using TUFReplay.Activity.Ipc;
using TUFReplay.Shared.Ipc.Generated;

// Headless consumer-contract fixture. Game behavior remains covered by the mod suites/manual game checks.
internal static class Program
{
  private static int _cancelled;
  private static int _waiting;
  private static ModRegistration<ReplayApiApp> _mod;

  private static async Task Main()
  {
    var registry = new IpcRegistry(action => action());
    var runtime = CreateHeadlessRuntime(registry);
    _mod = runtime.RegisterMod(
      ReplayApiContract.Instance,
      "TUFReplay integration fixture",
      new[] { "http://localhost" }
    );
    var implemented = new HashSet<string>
    {
      "ReadHealth",
      "ReadReplayState",
      "StartReplay",
      "ChooseLevelFile",
      "ReadLogicalLevelChart",
      "ExportMicrophoneRecording",
      "ExportCameraRecording",
      "ReadLegacyReplayStatus",
    };
    // Every generated operation is registered so contract readiness catches additions.
    foreach (var field in typeof(ReplayApiMethods).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
      if (implemented.Contains(field.Name))
        continue;
      var arguments = field.FieldType.GetGenericArguments();
      typeof(Program)
        .GetMethod(nameof(RegisterUnused), BindingFlags.NonPublic | BindingFlags.Static)
        .MakeGenericMethod(arguments)
        .Invoke(null, new[] { field.GetValue(null) });
    }
    _mod.Handle(ReplayApiMethods.ReadHealth, (_, _) => Task.FromResult(Health()));
    _mod.Handle(
      ReplayApiMethods.ReadLegacyReplayStatus,
      (_, _) => Task.FromResult(new LegacyReplayStatus { HasLegacyReplays = Volatile.Read(ref _waiting) > 0 })
    );
    _mod.Handle(ReplayApiMethods.ReadReplayState, (_, _) => Task.FromResult(State("idle", null)));
    _mod.Handle(
      ReplayApiMethods.StartReplay,
      async (request, context) =>
      {
        if (!_mod.TryGetApp(request.RunId, out var app))
          throw new IpcCallException("app_not_found", "Choose a connected app.");
        var value = State("playing", request.RunId);
        await app.ReplayStateChangedAsync(new ReplayStateChangedRequest { Value = value }, context.CancellationToken);
        return value;
      }
    );
    _mod.Handle(
      ReplayApiMethods.ChooseLevelFile,
      async (request, context) =>
      {
        Interlocked.Increment(ref _waiting);
        try
        {
          await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
          return new LevelFileResult
          {
            OperationId = "picker",
            RunId = request.RunId,
            Outcome = "cancelled",
          };
        }
        catch (OperationCanceledException)
        {
          Interlocked.Increment(ref _cancelled);
          throw;
        }
        finally
        {
          Interlocked.Decrement(ref _waiting);
        }
      }
    );
    _mod.Handle(
      ReplayApiMethods.ReadLogicalLevelChart,
      (request, _) =>
      {
        var chart = new ActivityChartDto
        {
          LevelSessionId = request.Id,
          FloorCount = 4896,
          LevelText = "{\"comment\":\"" + new string('한', 1_500_000) + "🙂\"}",
        };
        var file = ActivityChartDownloads.Create(chart);
        chart.LevelText = "Do not replace the validated snapshot.";
        return Task.FromResult(
          new Chart
          {
            LevelSessionId = request.Id,
            FloorCount = chart.FloorCount,
            File = file,
          }
        );
      }
    );
    _mod.Handle(
      ReplayApiMethods.ExportMicrophoneRecording,
      (request, _) =>
      {
        byte[] data = Encoding.UTF8.GetBytes(request.RunId);
        return Task.FromResult(IpcFile.FromStream(new MemoryStream(data), data.Length, "recording.txt", "text/plain"));
      }
    );
    _mod.Handle(
      ReplayApiMethods.ExportCameraRecording,
      (_, _) =>
        Task.FromResult(
          IpcFile.FromWriter(
            destination =>
            {
              byte[] chunk = new byte[64 * 1024];
              Array.Fill(chunk, (byte)0x5a);
              for (int i = 0; i < 128; i++)
                destination.Write(chunk, 0, chunk.Length);
            },
            8 * 1024 * 1024,
            "recording.mp4",
            "video/mp4"
          )
        )
    );
    _mod.AppConnected += app => _ = Snapshot(app);
    _mod.MarkReady();
    var server = new IpcServer(registry);
    server.Start();
    Console.WriteLine("IPC_TEST_PORT=" + server.Port);
    Console.Out.Flush();
    // A line of stdin disposes the registration while the server remains alive, for cleanup tests.
    _ = Task.Run(() =>
    {
      string command;
      while ((command = Console.ReadLine()) != null)
      {
        if (command == "disable")
        {
          _mod.Dispose();
          Console.WriteLine("IPC_TEST_DISABLED");
          Console.Out.Flush();
        }
        if (command == "stop")
        {
          _mod.Dispose();
          server.Stop();
          Environment.Exit(0);
        }
      }
    });
    try
    {
      await Task.Delay(Timeout.Infinite);
    }
    finally
    {
      _mod.Dispose();
      server.Stop();
    }
  }

  private static IIpcRuntime CreateHeadlessRuntime(IpcRegistry registry)
  {
    // RuntimeEntry.Start requires Unity's game-thread context. Only this test bootstrap
    // reflects the bundled implementation constructor; calls and registration use the public API.
    var type = typeof(IpcRegistry).Assembly.GetType("AdofaiIpc.Runtime.RoutedRuntime");
    var constructor = type?.GetConstructor(
      BindingFlags.Instance | BindingFlags.NonPublic,
      null,
      new[] { typeof(IpcRegistry), typeof(RuntimeInfo), typeof(Action<Action>), typeof(Func<bool>) },
      null
    );
    if (constructor == null)
      throw new InvalidOperationException(
        "The bundled IPC headless test bootstrap changed. Update the fixture constructor signature."
      );
    return (IIpcRuntime)
      constructor.Invoke(
        new object[]
        {
          registry,
          new RuntimeInfo("2.1.0", 1, 3, "commands", "game-thread", "snapshots", "downloads", "typed-files"),
          (Action<Action>)(action => action()),
          (Func<bool>)(() => false),
        }
      );
  }

  private static void RegisterUnused<TRequest, TResult>(IpcMethod<TRequest, TResult> method) =>
    _mod.Handle(
      method,
      (_, _) => Task.FromException<TResult>(new IpcCallException("fixture_unused", "This operation requires the game."))
    );

  private static Health Health() =>
    new Health
    {
      Ok = true,
      Mod = "TUFReplay",
      ModVersion = "fixture",
      ProtocolVersion = 11,
      ServerVersion = Volatile.Read(ref _cancelled),
      BuildFlavor = "standard",
      AutoSubmissionProtocolVersion = 0,
      ReplayEngineId = "fixture",
      ReplayFormatVersion = 1,
    };

  private static ReplayState State(string state, string runId) =>
    new ReplayState
    {
      OperationId = null,
      RunId = runId,
      State = state,
      ErrorCode = null,
      Message = null,
    };

  private static async Task Snapshot(ReplayApiApp app)
  {
    try
    {
      await app.HealthSnapshotAsync(new HealthSnapshotRequest { Value = Health() }, app.CancellationToken);
      await app.ReplayStateChangedAsync(
        new ReplayStateChangedRequest { Value = State("idle", null) },
        app.CancellationToken
      );
    }
    catch (OperationCanceledException) { }
    catch (IpcCallException error) when (error.Code == "unavailable") { }
    catch (Exception error)
    {
      Console.Error.WriteLine(error);
    }
  }
}
