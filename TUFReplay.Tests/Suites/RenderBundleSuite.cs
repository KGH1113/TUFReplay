using System.Globalization;
using TUFReplay.Replay.Export;
using TUFReplay.Replay.Models;
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
    catch (InvalidDataException)
    {
      rejected = true;
    }
    Assert(rejected, "Invalid replay data was silently exported.");
  }
}
