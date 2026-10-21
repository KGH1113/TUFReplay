using System;

namespace TUFReplay.Webcam.Models;

// Coordinates are normalized against the full source, with the origin at the
// top left. Mirroring affects display only, after selecting the source crop.
public readonly struct WebcamCropRect
{
  public const double MinimumSize = 0.05d;

  private WebcamCropRect(double x, double y, double width, double height)
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

  public static WebcamCropRect Get(TUFReplaySetting settings) =>
    settings == null
      ? Clamp(0, 0, 1, 1)
      : Clamp(settings.WebcamCropX, settings.WebcamCropY, settings.WebcamCropWidth, settings.WebcamCropHeight);

  public static WebcamCropRect Snapshot(TUFReplaySetting settings) => Get(settings);

  public static WebcamCropRect Clamp(double x, double y, double width, double height)
  {
    width = FiniteRange(width, 1, MinimumSize, 1);
    height = FiniteRange(height, 1, MinimumSize, 1);
    return new WebcamCropRect(FiniteRange(x, 0, 0, 1 - width), FiniteRange(y, 0, 0, 1 - height), width, height);
  }

  public double AspectRatio(double sourceAspect) =>
    (double.IsNaN(sourceAspect) || double.IsInfinity(sourceAspect) || sourceAspect <= 0 ? 1 : sourceAspect)
    * Width
    / Height;

  public WebcamTextureCoordinates TextureCoordinates(bool mirror, bool flipVertical = false) =>
    new WebcamTextureCoordinates(
      mirror ? X + Width : X,
      flipVertical ? 1 - Y : 1 - Y - Height,
      mirror ? -Width : Width,
      flipVertical ? -Height : Height
    );

  public void ApplyTo(TUFReplaySetting settings)
  {
    settings.WebcamCropX = X;
    settings.WebcamCropY = Y;
    settings.WebcamCropWidth = Width;
    settings.WebcamCropHeight = Height;
  }

  private static double FiniteRange(double value, double fallback, double minimum, double maximum) =>
    double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));
}

public readonly struct WebcamTextureCoordinates
{
  public WebcamTextureCoordinates(double x, double y, double width, double height)
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
