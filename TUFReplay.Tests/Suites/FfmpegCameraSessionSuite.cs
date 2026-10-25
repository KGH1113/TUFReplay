using System.Globalization;
using TUFReplay.Webcam.Capture;
using static TestFixture;

internal static class FfmpegCameraSessionSuite
{
  public static void RunAll()
  {
    ReadsNegotiatedInputRateFromShowinfo();
    RejectsUnavailableOrMalformedInputRates();
    Console.WriteLine("TUFReplay camera input frame-rate diagnostics tests passed.");
  }

  private static void ReadsNegotiatedInputRateFromShowinfo()
  {
    CultureInfo previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
      Assert(
        FfmpegCameraSession.TryParseInputFrameRate(
          "[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: 10/1",
          out double slowRate
        )
          && slowRate == 10,
        "A device-default 10 fps stream was not recognized."
      );
      Assert(
        FfmpegCameraSession.TryParseInputFrameRate(
          "[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: 30000/1001",
          out double fractionalRate
        )
          && Math.Abs(fractionalRate - 30000d / 1001) < 0.000001,
        "The negotiated NTSC frame rate changed with the system culture."
      );
      Assert(
        FfmpegCameraSession.TryParseInputFrameRate(
          "[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: 60000/1001",
          out double highRate
        )
          && highRate > 59
          && highRate < 60,
        "The negotiated source rate was limited before recording-profile selection."
      );
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  private static void RejectsUnavailableOrMalformedInputRates()
  {
    foreach (
      string invalid in new[] { "0/0", "0/1", "30/0", "-30/1", "30/-1", "NaN/1", "30/1x", new string('9', 40) + "/1" }
    )
      Assert(
        !FfmpegCameraSession.TryParseInputFrameRate(
          "[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: " + invalid,
          out double frameRate
        )
          && frameRate == 0,
        "An invalid or unavailable frame rate became a camera-speed warning: " + invalid
      );
    Assert(
      !FfmpegCameraSession.TryParseInputFrameRate("config out time_base: 1/1000000, frame_rate: 30/1", out _),
      "An output rate was mistaken for the camera's negotiated input rate."
    );
    Assert(!FfmpegCameraSession.TryParseInputFrameRate(null, out _), "An absent showinfo line supplied a frame rate.");
  }
}
