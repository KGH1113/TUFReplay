using System;

namespace TUFReplay.Webcam.Playback;

public readonly struct WebcamOverlayBounds
{
  public WebcamOverlayBounds(double x, double y, double width, double height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public double X { get; }
  public double Y { get; }
  public double Width { get; }
  public double Height { get; }
}

public static class WebcamOverlayLayout
{
  public static WebcamOverlayBounds Get(
    TUFReplaySetting settings,
    double screenWidth,
    double screenHeight,
    double aspect
  )
  {
    screenWidth = Math.Max(1, screenWidth);
    screenHeight = Math.Max(1, screenHeight);
    double width = Math.Max(1, screenWidth * settings.WebcamOverlayWidth);
    double height = width / ValidAspect(aspect);
    return new WebcamOverlayBounds(
      settings.WebcamOverlayLeft.HasValue
        ? settings.WebcamOverlayLeft.Value * screenWidth
        : (screenWidth - width) * settings.WebcamOverlayX,
      settings.WebcamOverlayTop.HasValue
        ? settings.WebcamOverlayTop.Value * screenHeight
        : (screenHeight - height) * settings.WebcamOverlayY,
      width,
      height
    );
  }

  public static void MoveTo(
    TUFReplaySetting settings,
    double x,
    double y,
    double screenWidth,
    double screenHeight,
    double aspect
  )
  {
    WebcamOverlayBounds bounds = Get(settings, screenWidth, screenHeight, aspect);
    // Store the actual position against the screen dimensions. The legacy anchor
    // fractions cannot represent a moved camera when its height equals the screen.
    settings.WebcamOverlayLeft = x / Math.Max(1, screenWidth);
    settings.WebcamOverlayTop = y / Math.Max(1, screenHeight);
    settings.WebcamOverlayX = Fraction(x, screenWidth - bounds.Width);
    settings.WebcamOverlayY = Fraction(y, screenHeight - bounds.Height);
  }

  public static void Apply(
    TUFReplaySetting settings,
    WebcamOverlayBounds bounds,
    double screenWidth,
    double screenHeight,
    double aspect
  )
  {
    settings.WebcamOverlayWidth = bounds.Width / Math.Max(1, screenWidth);
    MoveTo(settings, bounds.X, bounds.Y, screenWidth, screenHeight, aspect);
  }

  private static double ValidAspect(double aspect) =>
    double.IsNaN(aspect) || double.IsInfinity(aspect) || aspect <= 0 ? 16d / 9 : aspect;

  private static double Fraction(double value, double range) =>
    Math.Abs(range) > 0.001 ? Math.Max(-1, Math.Min(2, value / range)) : 0;
}
