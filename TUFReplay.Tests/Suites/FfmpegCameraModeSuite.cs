using System.Globalization;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;

internal static class FfmpegCameraModeSuite
{
  public static void RunAll()
  {
    QualityUsesSupportedRateAndPrefersMjpegAtEqualSize();
    BalancedAvoidsUnnecessaryFourKCapture();
    SmoothAlternativesPrecedeSlowNativeFallbacks();
    FractionalTargetsPrecedeSlowNativeModes();
    SlowFallbacksKeepTheHighestAvailableRate();
    FasterFixedSourceRateRemainsAvailable();
    NativeDefaultIsTriedBeforeSlowExplicitModes();
    AttemptPlanIsBoundedAndUnique();
    EmptyCapabilitiesStillTryNativeDefault();
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
      modes[0].Width == 1280 && Math.Abs(modes[0].FrameRate - 60) < 0.001,
      "Quality preferred a larger 30 fps mode over the available 60 fps mode."
    );
    Check(
      modes[1].Width == 1920 && modes[1].Format == "mjpeg",
      "Quality did not retain the supported 1080p MJPEG fallback ahead of equivalent raw capture."
    );
    Check(Math.Abs(modes[1].FrameRate - 30) < 0.001, "A 30 fps camera mode was forced to 60 fps.");
    string[] arguments = modes[1].InputArguments().ToArray();
    Check(
      arguments.Contains("-vcodec") && !arguments.Contains("-pixel_format"),
      "Compressed mode requested a raw pixel format."
    );
    Check(arguments.Contains("1920x1080"), "Camera input resolution remained implicit.");
  }

  private static void SmoothAlternativesPrecedeSlowNativeFallbacks()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "vcodec=mjpeg min s=1280x720 fps=30 max s=1280x720 fps=30",
        "pixel_format=yuyv422 min s=1280x720 fps=10 max s=1280x720 fps=10",
        "vcodec=mjpeg min s=1920x1080 fps=30 max s=1920x1080 fps=30",
        "pixel_format=yuyv422 min s=1920x1080 fps=5 max s=1920x1080 fps=5",
        "pixel_format=yuyv422 min s=640x480 fps=30 max s=640x480 fps=30",
      },
      WebcamCaptureProfile.ForQuality("balanced")
    );
    Check(modes.Count == 5, "A slow fallback was discarded instead of deferred.");
    Check(modes[0].Width == 1280 && modes[0].Format == "mjpeg", "The native 720p30 mode lost priority.");
    Check(modes[1].Width == 1920 && modes[1].Format == "mjpeg", "Scaled 1080p30 followed native 720p10.");
    Check(
      modes[2].Width == 640 && modes[2].FrameRate >= 29.9,
      "A usable lower-resolution 30 fps mode was hidden by 10 fps capture."
    );
    Check(modes[3].FrameRate == 10 && modes[4].FrameRate == 5, "Slow modes were not retained as final fallbacks.");
  }

  private static void FractionalTargetsPrecedeSlowNativeModes()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "pixel_format=yuyv422 min s=1280x720 fps=10 max s=1280x720 fps=10",
        "vcodec=mjpeg min s=640x480 fps=29.97 max s=640x480 fps=29.97",
      },
      WebcamCaptureProfile.ForQuality("balanced")
    );
    Check(modes[0].Width == 640 && modes[0].FrameRate > 29.9, "29.97 fps was treated as a slow fallback.");

    modes = FfmpegCameraModes.Parse(
      new[]
      {
        "vcodec=mjpeg min s=1920x1080 fps=30 max s=1920x1080 fps=30",
        "vcodec=mjpeg min s=1280x720 fps=59.94 max s=1280x720 fps=59.94",
      },
      WebcamCaptureProfile.ForQuality("quality")
    );
    Check(modes[0].Width == 1280 && modes[0].FrameRate > 59.9, "59.94 fps did not meet the Quality frame-rate target.");
  }

  private static void SlowFallbacksKeepTheHighestAvailableRate()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "pixel_format=yuyv422 min s=1920x1080 fps=5 max s=1920x1080 fps=5",
        "pixel_format=yuyv422 min s=1280x720 fps=10 max s=1280x720 fps=10",
        "pixel_format=yuyv422 min s=640x480 fps=15 max s=640x480 fps=15",
      },
      WebcamCaptureProfile.ForQuality("quality")
    );
    Check(
      modes.Count == 3 && modes[0].Width == 640,
      "Slow fallback selection preferred resolution over usable motion."
    );
    Check(modes[1].FrameRate == 10 && modes[2].FrameRate == 5, "Available slow rates lost their fallback order.");
  }

  private static void FasterFixedSourceRateRemainsAvailable()
  {
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "pixel_format=yuyv422 min s=640x480 fps=10 max s=640x480 fps=10",
        "vcodec=mjpeg min s=1280x720 fps=60 max s=1280x720 fps=60",
      },
      WebcamCaptureProfile.ForQuality("compact")
    );
    Check(
      modes[0].Width == 1280 && Math.Abs(modes[0].FrameRate - 60) < 0.001,
      "A fixed 60 fps source was discarded for a 30 fps recording target."
    );
  }

  private static void NativeDefaultIsTriedBeforeSlowExplicitModes()
  {
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("balanced");
    var modes = FfmpegCameraModes.Parse(
      new[]
      {
        "vcodec=mjpeg min s=1280x720 fps=30 max s=1280x720 fps=30",
        "pixel_format=yuyv422 min s=1280x720 fps=10 max s=1280x720 fps=10",
        "pixel_format=yuyv422 min s=640x480 fps=30 max s=640x480 fps=30",
      },
      profile
    );
    var attempts = FfmpegCameraModes.BuildAttempts(modes, profile);
    Check(attempts.Count == 4, "The retry plan omitted a supported mode or the native default.");
    Check(
      attempts[0].Width == 1280 && attempts[1].Width == 640,
      "Explicit smooth modes were not attempted before native negotiation."
    );
    Check(attempts[2] == null && attempts[3].FrameRate == 10, "10 fps capture hid the native default attempt.");
  }

  private static void AttemptPlanIsBoundedAndUnique()
  {
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality("balanced");
    var modes = Enumerable
      .Range(0, 12)
      .Select(index => new FfmpegCameraMode(640 + index * 2, 480, 30, "mjpeg", true))
      .ToList();
    modes.Insert(0, new FfmpegCameraMode(640, 480, 30, "mjpeg", true));
    modes.Insert(0, null);
    modes.AddRange(
      new[]
      {
        new FfmpegCameraMode(640, 480, 15, "yuyv422", false),
        new FfmpegCameraMode(1280, 720, 10, "yuyv422", false),
        new FfmpegCameraMode(1920, 1080, 5, "yuyv422", false),
        new FfmpegCameraMode(320, 240, 1, "yuyv422", false),
      }
    );
    var attempts = FfmpegCameraModes.BuildAttempts(modes, profile);
    Check(attempts.Count == 12, "The startup retry budget was exceeded or usable fallbacks were crowded out.");
    Check(attempts.Take(8).All(mode => mode != null && mode.FrameRate >= 29.9), "Fast attempts lost priority.");
    Check(attempts[8] == null && attempts.Count(mode => mode == null) == 1, "Native negotiation was duplicated.");
    Check(
      attempts.Take(8).Select(mode => mode.Width).Distinct().Count() == 8,
      "Duplicate capabilities consumed the startup retry budget."
    );
    Check(
      attempts[9].FrameRate > 14.9 && attempts[10].FrameRate == 10 && attempts[11].FrameRate == 5,
      "Bounded slow fallback order was not preserved."
    );
  }

  private static void EmptyCapabilitiesStillTryNativeDefault()
  {
    var attempts = FfmpegCameraModes.BuildAttempts(
      Array.Empty<FfmpegCameraMode>(),
      WebcamCaptureProfile.ForQuality("balanced")
    );
    Check(attempts.Count == 1 && attempts[0] == null, "An empty capability listing disabled native negotiation.");
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
      string[] rate = modes[0].FrameRateArgument.Split(':');
      long numerator = long.Parse(rate[0], CultureInfo.InvariantCulture);
      long denominator = long.Parse(rate[1], CultureInfo.InvariantCulture);
      Check(
        modes[0].IntervalTicks == 166833 && 10_000_000L * denominator / numerator == modes[0].IntervalTicks,
        "DirectShow interval was not reconstructed invariantly."
      );
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
