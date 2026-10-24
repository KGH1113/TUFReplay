using System.Globalization;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;

internal static class FfmpegCameraModeSuite
{
  public static void RunAll()
  {
    QualityUsesSupportedRateAndPrefersMjpegAtEqualSize();
    BalancedAvoidsUnnecessaryFourKCapture();
    RangeEndpointsPreserveAspectWithoutInventingModes();
    FractionalRatesSurviveNonEnglishCulture();
    MalformedAndDuplicateCapabilitiesAreIgnored();
    Console.WriteLine("TUFReplay DirectShow camera mode selection tests passed.");
  }

  private static void QualityUsesSupportedRateAndPrefersMjpegAtEqualSize()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "[dshow @ 0] pixel_format=yuyv422 min s=1920x1080 fps=5 max s=1920x1080 fps=30",
        "[dshow @ 0] vcodec=mjpeg min s=1920x1080 fps=5 max s=1920x1080 fps=30",
        "[dshow @ 0] vcodec=mjpeg min s=1280x720 fps=5 max s=1280x720 fps=60",
      },
      WebcamCaptureProfile.ForQuality("quality")
    );
    Check(modes.Count == 3, "Distinct DirectShow modes were lost.");
    Check(
      modes[0].Width == 1920 && modes[0].Format == "mjpeg",
      "Quality did not select the supported 1080p MJPEG mode."
    );
    Check(Math.Abs(modes[0].FrameRate - 30) < 0.001, "A 30 fps camera was forced to 60 fps.");
    string[] arguments = modes[0].InputArguments().ToArray();
    Check(
      arguments.Contains("-vcodec") && !arguments.Contains("-pixel_format"),
      "Compressed mode requested a raw pixel format."
    );
    Check(arguments.Contains("1920x1080"), "Camera input resolution remained implicit.");
  }

  private static void BalancedAvoidsUnnecessaryFourKCapture()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "vcodec=mjpeg min s=3840x2160 fps=30 max s=3840x2160 fps=60",
        "vcodec=mjpeg min s=1920x1080 fps=30 max s=1920x1080 fps=30",
        "pixel_format=yuyv422 min s=1280x720 fps=15 max s=1280x720 fps=30",
      },
      WebcamCaptureProfile.ForQuality("balanced")
    );
    Check(
      modes[0].Width == 1280 && modes[0].Height == 720,
      "720p recording unnecessarily captured a larger native image."
    );
    Check(modes[0].InputArguments().Contains("-pixel_format"), "Raw DirectShow format was not requested explicitly.");
  }

  private static void RangeEndpointsPreserveAspectWithoutInventingModes()
  {
    var modes = FfmpegCameraModes.Parse(
      new[] { "pixel_format=nv12 min s=640x480 fps=15 max s=1600x1200 fps=30" },
      WebcamCaptureProfile.ForQuality("balanced")
    );
    Check(modes.Count == 2, "Capability endpoints were not retained.");
    Check(
      modes[0].Width == 1600 && modes[0].Height == 1200,
      "The selector invented an unadvertised intermediate resolution."
    );
    CameraFrameSize output = CameraFrameSize.Fit(modes[0].Width, modes[0].Height, 1280, 720);
    Check(output.Width == 960 && output.Height == 720, "4:3 camera mode was stretched to 16:9.");
  }

  private static void FractionalRatesSurviveNonEnglishCulture()
  {
    CultureInfo previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
      var modes = FfmpegCameraModes.Parse(
        new[]
        {
          "vcodec=mjpeg min s=1920x1080 fps=30000/1001 max s=1920x1080 fps=60000/1001",
          "vcodec=mjpeg min s=640x480 fps=2.997e1 max s=640x480 fps=2.997e1",
        },
        WebcamCaptureProfile.ForQuality("quality")
      );
      Check(Math.Abs(modes[0].FrameRate - 60000d / 1001) < 0.001, "Fractional camera fps was rounded to 60.");
      Check(modes[0].FrameRateArgument == "10000000/166833", "DirectShow interval was not reconstructed invariantly.");
      Check(modes.Count == 2 && Math.Abs(modes[1].FrameRate - 29.97) < 0.001, "Scientific decimal fps was rejected.");
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  private static void MalformedAndDuplicateCapabilitiesAreIgnored()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        null,
        "DirectShow video device options",
        "unknown compression type 0x100 min s=640x480 fps=30 max s=640x480 fps=30",
        "vcodec=mjpeg min s=0x1080 fps=30 max s=1920x1080 fps=30",
        "vcodec=mjpeg min s=1920x1080 fps=1/0 max s=1920x1080 fps=30",
        "vcodec=mjpeg min s=1920x1080 fps=60 max s=1920x1080 fps=30",
        "vcodec=mjpeg min s=9999999999999x1080 fps=30 max s=1920x1080 fps=30",
        "vcodec=mjpeg min s=640x480 fps=30 max s=640x480 fps=30",
        "vcodec=mjpeg min s=640x480 fps=30 max s=640x480 fps=30",
      },
      WebcamCaptureProfile.ForQuality("quality")
    );
    Check(
      modes.Count == 1 && modes[0].Width == 640,
      "Malformed or duplicated capability reached camera initialization."
    );
  }

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
