using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using TUFReplay.Replay.Export;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using static TestFixture;

internal static class RenderBundleSuite
{
  internal static void RunAll()
  {
    using var windows = new StringWriter();
    RenderBundleCsv.WriteInputs(
      windows,
      new[]
      {
        new RecordedInput(0, 0x41, RecordInputFlags.Down),
        new RecordedInput(0, 0x41, 0),
        new RecordedInput(1, 0x0D, RecordInputFlags.Down | RecordInputFlags.ExtendedKey),
      },
      "windows",
      1
    );
    Assert(
      windows.ToString().Replace("\r", "") == "timeUs,key,down,sequence\n0,A,1,0\n0,A,0,1\n1,KeypadEnter,1,2\n",
      "Neutral key export lost a short pulse or native keypad identity."
    );
    using var countdown = new StringWriter();
    RenderBundleCsv.WriteInputs(
      countdown,
      new[]
      {
        new RecordedInput(-2_000_000, 0x41, RecordInputFlags.Down),
        new RecordedInput(-2_000_000, 0x41, 0),
        new RecordedInput(-1, 0x42, RecordInputFlags.Down),
        new RecordedInput(0, 0x42, 0),
      },
      "windows",
      1
    );
    Assert(
      countdown.ToString().Replace("\r", "")
        == "timeUs,key,down,sequence\n-2000000,A,1,0\n-2000000,A,0,1\n-1,B,1,2\n0,B,0,3\n",
      "Countdown input times or short pulse sequence changed during export."
    );
    var parsedCountdown = ReplayInputParser.Parse(Encoding.UTF8.GetBytes("-2000000,65,1,65,0\n-2000000,65,0,65,0\n"));
    Assert(
      parsedCountdown.Count == 2 && parsedCountdown[0].TimeUs == -2_000_000 && !parsedCountdown[1].Down,
      "Native countdown timestamps or tied short tap changed during parsing."
    );
    using var earlyHit = new StringWriter();
    RenderBundleCsv.WriteHits(
      earlyHit,
      new[] { new ReplayHitContext(0, 1, 0, false, false, false, 1, 1, false, false, 0, 0, -1) },
      1,
      _ => "Perfect"
    );
    Assert(earlyHit.ToString().Contains("-1,0,1,0"), "A signed pre-origin accepted hit was rejected.");
    var metadata = new ReplayMetadata
    {
      gameplayStartSongPosition = -1,
      effectivePitch = 1,
      gameInputOffsetMs = 0,
      terminalTimeUs = 1,
      judgmentSystem = "ModernClassic",
    };
    RenderBundleValidation.ValidateMetadata(metadata);
    metadata.effectivePitch = null;
    Error(() => RenderBundleValidation.ValidateMetadata(metadata), "render_metadata_field_missing", "effectivePitch");
    metadata.effectivePitch = float.NaN;
    Error(() => RenderBundleValidation.ValidateMetadata(metadata), "render_metadata_field_invalid", "effectivePitch");
    Error(
      () =>
        RenderBundleCsv.WriteInputs(
          new StringWriter(),
          new[] { new RecordedInput(-1, 0x41, 0), new RecordedInput(-2, 0x41, 0) },
          "windows",
          1
        ),
      "render_timeline_out_of_order",
      "timeUs",
      3,
      "inputs.csv"
    );
    Error(
      () => RenderBundleCsv.WriteInputs(new StringWriter(), new[] { new RecordedInput(2, 0x41, 0) }, "windows", 1),
      "render_event_after_terminal",
      "timeUs",
      2,
      "inputs.csv"
    );
    using var specialKeys = new StringWriter();
    RenderBundleCsv.WriteInputs(
      specialKeys,
      new[]
      {
        new RecordedInput(0, 0x90, RecordInputFlags.Down),
        new RecordedInput(1, 0x2C, RecordInputFlags.Down | RecordInputFlags.ExtendedKey),
      },
      "windows",
      1
    );
    Assert(
      specialKeys.ToString().Replace("\r", "") == "timeUs,key,down,sequence\n0,Numlock,1,0\n1,Print,1,1\n",
      "Windows lock and print keys did not use Unity KeyCode aliases."
    );
    using var mac = new StringWriter();
    RenderBundleCsv.WriteInputs(
      mac,
      new[] { new RecordedInput(0, 0x24, RecordInputFlags.Down), new RecordedInput(1, 0x37, RecordInputFlags.Down) },
      "macos",
      1
    );
    Assert(
      mac.ToString().Contains("0,Return,1,0") && mac.ToString().Contains("1,LeftCommand,1,1"),
      "macOS native keys used host platform codes."
    );
    Throws(() =>
      RenderBundleCsv.WriteInputs(new StringWriter(), new[] { new RecordedInput(0, 65535, 0) }, "windows", 1)
    );
    Throws(() =>
      RenderBundleCsv.WriteInputs(new StringWriter(), new[] { new RecordedInput(2, 0x41, 0) }, "windows", 1)
    );
    Throws(() =>
      RenderBundleCsv.WriteInputs(
        new StringWriter(),
        new[] { new RecordedInput(1, 0x41, 0), new RecordedInput(0, 0x41, 0) },
        "windows",
        1
      )
    );
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
      using var hits = new StringWriter();
      RenderBundleCsv.WriteHits(hits, new[] { Hit(1.5), Hit(2.5) }, 1, _ => "Perfect");
      Assert(
        hits.ToString().Contains("0,1,1.5,0,0,0,0,2,3,0,0,0,Perfect"),
        "Hit export lost invariant decimals or resolved margin."
      );
      Assert(
        hits.ToString().IndexOf("1.5", StringComparison.Ordinal)
          < hits.ToString().IndexOf("2.5", StringComparison.Ordinal),
        "Equal-time hit ordering changed."
      );
      Throws(() => RenderBundleCsv.WriteHits(new StringWriter(), new[] { Hit(1) }, 1, _ => null));
      Throws(() => RenderBundleCsv.WriteHits(new StringWriter(), new[] { Hit(double.NaN) }, 1, _ => "Perfect"));
    }
    finally
    {
      CultureInfo.CurrentCulture = previousCulture;
    }
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    bool cancelled = false;
    try
    {
      RenderBundleCsv.WriteInputs(
        new StringWriter(),
        new[] { new RecordedInput(0, 0x41, 0) },
        "windows",
        1,
        cancellation.Token
      );
    }
    catch (OperationCanceledException)
    {
      cancelled = true;
    }
    Assert(cancelled, "Bundle export ignored cancellation.");
    TestPrematureTerminalRecovery();
    TestRecordedTerminalFixture();
    Console.WriteLine("TUFReplay neutral render bundle tests passed.");
  }

  private static void TestPrematureTerminalRecovery()
  {
    // Reproduce the 23:56 payload's committed zero boundary, valid later hits
    // and final Escape transition. Recovery changes the export boundary only.
    var metadata = new ReplayMetadata
    {
      startedAtUtc = "2026-10-05T14:56:56.3889140Z",
      endedAtUtc = "2026-10-05T14:56:56.4034060Z",
      terminalTimeUs = 0L,
      inputFormat = RecordedRunPayload.NativeInputFormatV2,
      inputTimeBase = ReplayInputTimeBases.Hybrid,
      inputInvalidAnchors = 229,
      inputDiscontinuities = 2,
      inputLastDiscontinuity = "fail",
    };
    RecordedInput[] inputs =
    {
      new(0L, 14, RecordInputFlags.Down),
      new(0L, 14, 0),
      new(483_193L, 121, RecordInputFlags.Down),
      new(584_667L, 121, 0),
      new(9_665_839L, 53, RecordInputFlags.Down),
    };
    ReplayHitContext[] hits =
    {
      new(0, 0, 0, false, false, false, 0, 0, false, false, 0, 4, 0L),
      new(57, 0, 0, false, false, false, 0, 0, false, false, 0, 7, 8_832_425L),
    };
    long terminal = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "aborted", inputs, hits, out bool recovered);
    Assert(recovered && terminal == 9_665_839L, "The observed pre-start boundary defect could not be exported.");
    Assert(
      metadata.terminalTimeUs == 0L && inputs[0].TimeUs == 0L,
      "Recovery modified the original recording timestamps."
    );
    using var output = new StringWriter();
    RenderBundleCsv.WriteInputs(output, inputs, "macos", terminal);
    Assert(output.ToString().Contains("9665839,Escape,1,4"), "Terminal recovery lost the recorded stop input.");
    Assert(
      RenderBundleValidation.RecoveredTerminalWarning.Contains("cannot be restored"),
      "Recovery hid the unrecoverable pre-start key timing."
    );

    metadata.terminalTimeUs = 400_000L;
    terminal = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "aborted", inputs, hits, out recovered);
    Assert(!recovered && terminal == 400_000L, "Ordinary late events silently extended a nonzero terminal.");
    Error(
      () => RenderBundleCsv.WriteInputs(new StringWriter(), inputs, "macos", terminal),
      "render_event_after_terminal",
      "timeUs",
      4,
      "inputs.csv"
    );
    metadata.terminalTimeUs = 0L;
    _ = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "failed", inputs, hits, out recovered);
    Assert(!recovered, "A failed recording without the observed aborted signature was normalized.");
    metadata.endedAtUtc = "2026-10-05T14:56:57.4034060Z";
    _ = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "aborted", inputs, hits, out recovered);
    Assert(!recovered, "A terminal committed during gameplay was treated as a pre-start defect.");
    metadata.endedAtUtc = "2026-10-05T14:56:56.4034060Z";
    metadata.inputDiscontinuities = 0L;
    _ = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "aborted", inputs, hits, out recovered);
    Assert(!recovered, "An unrelated zero terminal was normalized without the capture signature.");
    metadata.inputDiscontinuities = 2L;
    inputs[inputs.Length - 1] = new RecordedInput(50_000_000L, 53, RecordInputFlags.Down);
    _ = RenderBundleValidation.ResolveTerminalTimeUs(metadata, "aborted", inputs, hits, out recovered);
    Assert(!recovered, "Arbitrary input long after the final hit extended the export.");
  }

  private static void TestRecordedTerminalFixture()
  {
    string database = Environment.GetEnvironmentVariable("TUFREPLAY_RENDER_BUNDLE_FIXTURE_DB");
    if (string.IsNullOrWhiteSpace(database))
      return;
    string runId = Environment.GetEnvironmentVariable("TUFREPLAY_RENDER_BUNDLE_FIXTURE_RUN");
    string gameAssembly = Environment.GetEnvironmentVariable("TUFREPLAY_RENDER_BUNDLE_FIXTURE_GAME_DLL");
    Assert(
      !string.IsNullOrWhiteSpace(runId) && File.Exists(gameAssembly),
      "Recorded fixture needs its run and game judgment enum."
    );
    var connectionString = new SqliteConnectionStringBuilder
    {
      DataSource = database,
      Mode = SqliteOpenMode.ReadOnly,
      Pooling = false,
    };
    using var connection = new SqliteConnection(connectionString.ToString());
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT r.result, a.metadata_json, a.input_csv, a.hit_context_csv FROM runs r JOIN replay_artifacts a ON a.run_id=r.id WHERE r.id=@run";
    command.Parameters.AddWithValue("@run", runId);
    using var reader = command.ExecuteReader();
    Assert(reader.Read(), "The requested recorded terminal fixture is missing.");
    var metadata = JsonConvert.DeserializeObject<ReplayMetadata>(reader.GetString(1));
    var inputs = ReplayInputParser.Parse((byte[])reader[2]);
    var hits = ReplayHitContextParser.Parse((byte[])reader[3]);
    RenderBundleValidation.ValidateMetadata(metadata);
    long terminal = RenderBundleValidation.ResolveTerminalTimeUs(
      metadata,
      reader.GetString(0),
      inputs,
      hits,
      out bool recovered
    );
    Assert(
      recovered && terminal == Math.Max(inputs[^1].TimeUs, hits[^1].TimeUs),
      "The actual recorded terminal defect did not match safe recovery."
    );
    using var inputCsv = new StringWriter();
    RenderBundleCsv.WriteInputs(inputCsv, inputs, metadata.inputNativePlatform, terminal);
    Type marginType = Assembly.LoadFrom(gameAssembly).GetType("HitMargin", throwOnError: true);
    using var hitCsv = new StringWriter();
    RenderBundleCsv.WriteHits(hitCsv, hits, terminal, value => Enum.GetName(marginType, value));
    Assert(
      inputCsv.ToString().Split('\n').Length == inputs.Count + 2,
      "Fixture export dropped recorded key transitions."
    );
    Assert(hitCsv.ToString().Split('\n').Length == hits.Count + 2, "Fixture export dropped accepted judgments.");
    Assert(metadata.terminalTimeUs == 0L, "Fixture recovery changed the stored metadata.");
    Console.WriteLine(
      "Read-only recorded terminal fixture passed. terminalTimeUs="
        + terminal
        + ", inputs="
        + inputs.Count
        + ", hits="
        + hits.Count
        + ", warnings=1"
    );
  }

  private static ReplayHitContext Hit(double angle) =>
    new(1, angle, 0, false, false, false, 2, 3, false, false, 0, 0, 0);

  private static void Throws(Action action)
  {
    bool rejected = false;
    try
    {
      action();
    }
    catch (RenderBundleValidationException)
    {
      rejected = true;
    }
    Assert(rejected, "Invalid replay data was silently exported.");
  }

  private static void Error(Action action, string code, string field, int? line = null, string file = null)
  {
    try
    {
      action();
      throw new Exception("Invalid data was accepted.");
    }
    catch (RenderBundleValidationException exception)
    {
      Assert(exception.Code == code && exception.Field == field, "Export lost the exact validation reason or field.");
      if (line.HasValue)
        Assert(
          exception.Line == line && exception.File == file,
          "Export error row did not include the neutral CSV header."
        );
    }
  }
}
