using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using TUFReplay.Webcam.Capture;
using static TestFixture;

internal static class FfmpegCameraFrameRateSuite
{
  private static readonly long[] CameraIntervals =
  {
    10000,
    16666,
    16667,
    41666,
    41667,
    83333,
    100000,
    166666,
    166667,
    166833,
    333333,
    333334,
    333667,
    400000,
    666667,
    1000000,
    2000000,
  };

  public static void RunAll()
  {
    CameraIntervalsSurviveIntegerConversion();
    RatesRemainInvariantAndRejectUnrepresentableInputs();
    ActualFfmpegParserPreservesCameraIntervals();
    Console.WriteLine("TUFReplay camera frame-rate negotiation tests passed.");
  }

  private static void CameraIntervalsSurviveIntegerConversion()
  {
    foreach (long interval in CameraIntervals)
      CheckInterval(interval);
    // Exercise intervals between standard rates, including both sides of the
    // integer boundaries that the old 10000000/interval representation lost.
    for (long interval = 10000; interval <= 1000000; interval += 997)
      CheckInterval(interval);
    CheckInterval(FfmpegCameraFrameRate.TicksPerSecond * FfmpegCameraFrameRate.MaximumComponent);
  }

  private static FfmpegCameraFrameRate CheckInterval(long interval)
  {
    Assert(FfmpegCameraFrameRate.TryFromInterval(interval, out var rate), "Supported interval was lost: " + interval);
    string[] parts = rate.Argument.Split(':');
    Assert(parts.Length == 2, "Camera rate did not use FFmpeg's integer ratio syntax.");
    long numerator = long.Parse(parts[0], CultureInfo.InvariantCulture);
    long denominator = long.Parse(parts[1], CultureInfo.InvariantCulture);
    Assert(
      numerator > 0 && numerator <= 1_001_000 && denominator > 0 && denominator <= 1_001_000,
      "Camera rate exceeded FFmpeg's actual parser component bound."
    );
    Assert(
      10_000_000L * denominator / numerator == interval,
      "Camera rate changed its DirectShow interval: " + interval
    );
    Assert(rate.IntervalTicks == interval, "Camera rate diagnostics lost the requested interval.");
    return rate;
  }

  private static void RatesRemainInvariantAndRejectUnrepresentableInputs()
  {
    CultureInfo previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
      Assert(FfmpegCameraFrameRate.TryCreate(30, out var thirty), "30 fps could not be represented.");
      Assert(thirty.IntervalTicks == 333333, "30 fps no longer selected the native 333333-tick interval.");
      Assert(
        FfmpegCameraFrameRate.TryCreate(60000d / 1001, out var fractional),
        "Fractional fps could not be represented."
      );
      Assert(fractional.IntervalTicks == 166833, "Fractional fps was rounded to 60.");
      Assert(!fractional.Argument.Contains(','), "Current culture changed the FFmpeg rate argument.");
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }

    foreach (double invalid in new[] { 0d, -30d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 1e-100 })
      Assert(!FfmpegCameraFrameRate.TryCreate(invalid, out _), "Invalid fps reached FFmpeg: " + invalid);
    foreach (long invalid in new[] { 0L, -1L, long.MaxValue, 1L, 10_000_001L })
      Assert(
        !FfmpegCameraFrameRate.TryFromInterval(invalid, out _),
        "Unrepresentable interval was silently changed: " + invalid
      );
  }

  private static void ActualFfmpegParserPreservesCameraIntervals()
  {
    string executable = FindFfmpeg();
    if (executable == null)
    {
      Console.WriteLine("SKIP camera frame-rate parser integration: FFmpeg is not installed.");
      return;
    }

    foreach (long interval in CameraIntervals)
    {
      FfmpegCameraFrameRate rate = CheckInterval(interval);
      // rawvideo uses the same av_parse_video_rate entry point as DirectShow.
      // Copying a tiny frame exposes the parsed time base without a camera,
      // encoder, ffprobe dependency, or reproduction of FFmpeg's parser in C#.
      using Process process = FfmpegCameraProcess.Create(
        executable,
        new[]
        {
          "-v",
          "error",
          "-f",
          "rawvideo",
          "-pixel_format",
          "gray",
          "-video_size",
          "2x2",
          "-framerate",
          rate.Argument,
          "-i",
          "pipe:0",
          "-frames:v",
          "1",
          "-c:v",
          "copy",
          "-f",
          "framemd5",
          "pipe:1",
        }
      );
      Assert(process.Start(), "FFmpeg frame-rate parser test did not start.");
      Task<string> output = process.StandardOutput.ReadToEndAsync();
      Task<string> error = process.StandardError.ReadToEndAsync();
      process.StandardInput.BaseStream.Write(new byte[4], 0, 4);
      process.StandardInput.Close();
      if (!process.WaitForExit(10000))
      {
        process.Kill();
        throw new InvalidOperationException("FFmpeg frame-rate parser test timed out.");
      }
      Task.WaitAll(output, error);
      Assert(process.ExitCode == 0, "FFmpeg rejected camera rate " + rate.Argument + ": " + error.Result);
      Match timeBase = Regex.Match(output.Result, @"(?m)^#tb 0: (\d+)/(\d+)");
      Assert(timeBase.Success, "FFmpeg did not expose the parsed camera time base.");
      long numerator = long.Parse(timeBase.Groups[1].Value, CultureInfo.InvariantCulture);
      long denominator = long.Parse(timeBase.Groups[2].Value, CultureInfo.InvariantCulture);
      long parsedInterval = 10_000_000L * numerator / denominator;
      Assert(parsedInterval == interval, "Actual FFmpeg parser changed " + interval + " to " + parsedInterval + ".");
    }
  }

  private static string FindFfmpeg()
  {
    string configured = Environment.GetEnvironmentVariable("TUFREPLAY_TEST_FFMPEG");
    if (!string.IsNullOrEmpty(configured))
      return File.Exists(configured) ? configured : null;
    return (Environment.GetEnvironmentVariable("PATH") ?? "")
      .Split(Path.PathSeparator)
      .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg"))
      .FirstOrDefault(File.Exists);
  }
}
