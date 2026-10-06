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
    Assert(source.ByteLength > 2 * 1024 * 1024, "The regression fixture does not exceed the control-message limit.");
    Assert(source.ByteLength == Encoding.UTF8.GetByteCount(text), "The chart ticket length is not a UTF-8 byte count.");
    chart.LevelText = "A later file revision must not replace the validated snapshot.";
    using var output = new MemoryStream();
    source.WriteTo(output);
    Assert(output.CanWrite, "The chart writer disposed the destination HTTP stream.");
    Assert(output.Length == source.ByteLength, "The chart writer did not write the declared number of bytes.");
    Assert(
      Encoding.UTF8.GetString(output.ToArray()) == text,
      "The chart text changed, was escaped, or acquired a BOM."
    );
    Console.WriteLine("PASS large chart UTF-8 streaming, exact byte count and validated snapshot ownership");
  }
}
