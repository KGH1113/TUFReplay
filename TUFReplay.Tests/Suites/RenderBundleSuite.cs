using System.Globalization;
using System.Text;
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
    Console.WriteLine("TUFReplay neutral render bundle tests passed.");
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
