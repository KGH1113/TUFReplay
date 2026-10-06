using System;

namespace TUFReplay.Webcam.Models;

public readonly struct CameraFrameSize
{
  public CameraFrameSize(int width, int height)
  {
    Width = width;
    Height = height;
  }

  public int Width { get; }
  public int Height { get; }
  public int ByteCount => Width * Height * 4;
  public double Aspect => (double)Width / Height;

  // H.264/YUV420 dimensions must be even. Scale uniformly and never upscale.
  public static CameraFrameSize Fit(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
  {
    if (sourceWidth < 2 || sourceHeight < 2 || maxWidth < 2 || maxHeight < 2)
      throw new ArgumentOutOfRangeException(nameof(sourceWidth));
    double scale = Math.Min(1, Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight));
    return new CameraFrameSize(
      Math.Max(2, (int)(sourceWidth * scale) / 2 * 2),
      Math.Max(2, (int)(sourceHeight * scale) / 2 * 2)
    );
  }
}
