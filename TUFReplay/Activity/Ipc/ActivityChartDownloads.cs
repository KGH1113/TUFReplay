using System.IO;
using System.Text;
using AdofaiIpc;

namespace TUFReplay.Activity.Ipc;

public static class ActivityChartDownloads
{
  private static readonly Encoding Utf8 = new UTF8Encoding(false);

  public static IpcDownloadSource Create(ActivityChartDto chart)
  {
    // Keep the validated snapshot, even if the file changes before the ticket is consumed.
    // Encode on the HTTP worker in bounded chunks without a second whole-file byte array.
    string levelText = chart.LevelText;
    return new IpcDownloadSource(
      destination =>
      {
        using var writer = new StreamWriter(destination, Utf8, 64 * 1024, leaveOpen: true);
        writer.Write(levelText);
      },
      Utf8.GetByteCount(levelText),
      "tufreplay-chart.adofai",
      "application/json; charset=utf-8"
    );
  }
}
