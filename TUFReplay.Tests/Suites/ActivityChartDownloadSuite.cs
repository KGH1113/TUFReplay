using System.Text;
using TUFReplay.Activity.Ipc;
using static TestFixture;

internal static class ActivityChartDownloadSuite
{
  internal static void RunAll()
  {
    string text = "{\"comment\":\"" + new string('한', 1_500_000) + "🙂\"}";
    var chart = new ActivityChartDto
    {
      LevelSessionId = "large-chart",
      LevelText = text,
      FloorCount = 4896,
    };
    using var source = ActivityChartDownloads.Create(chart);
    Assert(source.Size > 2 * 1024 * 1024, "The regression fixture does not exceed the control-message limit.");
    Assert(source.Size == Encoding.UTF8.GetByteCount(text), "The chart file length is not a UTF-8 byte count.");
    Assert(source.Name == "tufreplay-chart.adofai", "The chart file name changed.");
    Assert(source.ContentType == "application/json; charset=utf-8", "The chart content type changed.");
    // The real-runtime IPC suite consumes this opaque file source and checks snapshot ownership.
    Console.WriteLine("PASS large chart typed-file metadata and exact UTF-8 byte count");
  }
}
