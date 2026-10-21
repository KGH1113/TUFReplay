using Newtonsoft.Json.Linq;
using TUFReplay.Webcam.Diagnostics;

internal static class CameraEngineStatisticsSuite
{
  internal static void RunAll()
  {
    MissingMeasurementsAreNotZeroCost();
    FrameTimingsAreDeduplicatedAcrossWindows();
    InvalidTimingsDoNotPoisonTheWindow();
    SamplingDoesNotAllocate();
    Console.WriteLine("TUFReplay engine render measurement tests passed.");
  }

  private static void MissingMeasurementsAreNotZeroCost()
  {
    var metric = new CameraMetricStatistics();
    JObject missing = JObject.FromObject(metric.DiagnosticState());
    Check((int)missing["samples"] == 0 && missing["mean"].Type == JTokenType.Null, "Missing data reported zero cost.");
    metric.Record(0);
    metric.Record(4);
    JObject measured = JObject.FromObject(metric.DiagnosticState());
    Check((int)measured["samples"] == 2, "A valid zero counter was dropped.");
    Near(2, (double)measured["mean"]);
    Near(0, (double)measured["min"]);
    Near(4, (double)measured["max"]);
    Check(missing["mean"].Type == JTokenType.Null, "A snapshot retained mutable statistics.");
  }

  private static void FrameTimingsAreDeduplicatedAcrossWindows()
  {
    var statistics = new CameraFrameTimingStatistics();
    statistics.Record(100, 8, 4, 3, 0, 7);
    statistics.Record(100, 80, 40, 30, 1, 70);
    statistics.Record(99, 80, 40, 30, 1, 70);
    JObject first = JObject.FromObject(statistics.DiagnosticState());
    Check((int)first["duplicateOrOlderTimings"] == 2, "Repeated or older GPU timings were counted twice.");
    Near(7, (double)first["gpuFrame"]["mean"]);
    Near(0, (double)first["cpuPresentWait"]["mean"]);
    statistics.ResetWindow();
    statistics.Record(100, 80, 40, 30, 1, 70);
    statistics.Record(101, 10, 5, 4, 1, 9);
    JObject second = JObject.FromObject(statistics.DiagnosticState());
    Check((int)second["gpuFrame"]["samples"] == 1, "A window reset re-counted delayed GPU data.");
    Check((int)second["duplicateOrOlderTimings"] == 1, "Duplicate counters leaked between windows.");
    Near(9, (double)second["gpuFrame"]["mean"]);
    Near(7, (double)first["gpuFrame"]["mean"]);
  }

  private static void InvalidTimingsDoNotPoisonTheWindow()
  {
    var statistics = new CameraFrameTimingStatistics();
    statistics.Record(0, 100, 100, 100, 100, 100);
    statistics.Record(1, 0, 0, 0, 0, 0);
    statistics.Record(2, double.NaN, double.PositiveInfinity, -1, 0, double.NegativeInfinity);
    JObject invalid = JObject.FromObject(statistics.DiagnosticState());
    Check((int)invalid["missingTimestamps"] == 1, "Missing timestamps were treated as fresh frames.");
    foreach (string name in new[] { "cpuFrame", "cpuMainThread", "cpuRenderThread", "cpuPresentWait", "gpuFrame" })
      Check(invalid[name]["mean"].Type == JTokenType.Null, "An invalid or unsupported timing became measured data.");
    statistics.Record(3, 10, 5, 4, 0, 0);
    JObject cpuOnly = JObject.FromObject(statistics.DiagnosticState());
    Near(10, (double)cpuOnly["cpuFrame"]["mean"]);
    Check(cpuOnly["gpuFrame"]["mean"].Type == JTokenType.Null, "Unsupported GPU data looked like zero GPU load.");
  }

  private static void SamplingDoesNotAllocate()
  {
    var statistics = new CameraFrameTimingStatistics();
    var metric = new CameraMetricStatistics();
    for (ulong frame = 1; frame < 100; frame++)
    {
      statistics.Record(frame, 10, 5, 4, 0, 7);
      metric.Record(3);
    }
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (ulong frame = 100; frame < 2100; frame++)
    {
      statistics.Record(frame, 10, 5, 4, 0, 7);
      metric.Record(3);
    }
    Check(GC.GetAllocatedBytesForCurrentThread() == before, "Per-frame engine measurement allocated memory.");
  }

  private static void Near(double expected, double actual) =>
    Check(Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, received {actual}.");

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
