using System;

namespace TUFReplay.Webcam.Capture;

public sealed class CameraPreviewPixels
{
  private readonly struct Sample
  {
    public Sample(int first, int[] weights)
    {
      First = first;
      Weights = weights;
    }

    public int First { get; }
    public int[] Weights { get; }
  }

  private readonly int _width;
  private readonly int _height;
  private readonly int _targetWidth;
  private readonly int _targetHeight;
  private readonly Sample[] _lumaX;
  private readonly Sample[] _lumaY;
  private readonly Sample[] _chromaX;
  private readonly Sample[] _chromaY;

  // Area averaging prevents fine detail from aliasing when downscaled. Chroma
  // upsampling is bilinear. Coefficients are reused, with no per-frame allocation.
  // Conversion runs on the capture worker; the recording receives untouched YUV.
  public CameraPreviewPixels(int width, int height, int targetWidth, int targetHeight)
  {
    if (
      (width & 1) != 0
      || (height & 1) != 0
      || width < 2
      || height < 2
      || targetWidth < 1
      || targetHeight < 1
      || targetWidth > width
      || targetHeight > height
    )
      throw new ArgumentException("Invalid camera frame dimensions.");
    _width = width;
    _height = height;
    _targetWidth = targetWidth;
    _targetHeight = targetHeight;
    _lumaX = BuildAxis(width, targetWidth);
    _lumaY = BuildAxis(height, targetHeight);
    _chromaX = BuildAxis(width / 2, targetWidth);
    _chromaY = BuildAxis(height / 2, targetHeight);
  }

  public static void FromYuv420(
    byte[] source,
    int width,
    int height,
    byte[] target,
    int targetWidth,
    int targetHeight
  ) => new CameraPreviewPixels(width, height, targetWidth, targetHeight).Convert(source, target);

  public void Convert(byte[] source, byte[] target)
  {
    int lumaSize = _width * _height;
    if (
      source == null
      || source.Length != lumaSize * 3 / 2
      || target == null
      || target.Length != _targetWidth * _targetHeight * 4
    )
      throw new ArgumentException("Invalid camera frame dimensions.");
    for (int y = 0; y < _targetHeight; y++)
    {
      for (int x = 0; x < _targetWidth; x++)
      {
        int c = Read(source, 0, _width, _lumaX[x], _lumaY[y]) - 16;
        int d = Read(source, lumaSize, _width / 2, _chromaX[x], _chromaY[y]) - 128;
        int e = Read(source, lumaSize + lumaSize / 4, _width / 2, _chromaX[x], _chromaY[y]) - 128;
        int destination = ((_targetHeight - 1 - y) * _targetWidth + x) * 4;
        target[destination] = Clamp((298 * c + 409 * e + 128) >> 8);
        target[destination + 1] = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
        target[destination + 2] = Clamp((298 * c + 516 * d + 128) >> 8);
        target[destination + 3] = 255;
      }
    }
  }

  private static int Read(byte[] source, int offset, int stride, Sample x, Sample y)
  {
    if (x.Weights.Length == 1 && y.Weights.Length == 1)
      return source[offset + y.First * stride + x.First];
    int value = 0;
    for (int row = 0; row < y.Weights.Length; row++)
    {
      int rowValue = 0;
      int start = offset + (y.First + row) * stride + x.First;
      for (int column = 0; column < x.Weights.Length; column++)
        rowValue += source[start + column] * x.Weights[column];
      value += rowValue * y.Weights[row];
    }
    return (value + 32768) >> 16;
  }

  private static Sample[] BuildAxis(int sourceSize, int targetSize)
  {
    var samples = new Sample[targetSize];
    double scale = (double)sourceSize / targetSize;
    for (int index = 0; index < targetSize; index++)
    {
      if (scale < 1)
      {
        double position = Math.Max(0, Math.Min(sourceSize - 1, (index + 0.5) * scale - 0.5));
        int first = (int)position;
        int nextWeight = (int)Math.Round((position - first) * 256);
        samples[index] = new Sample(
          first,
          first == sourceSize - 1 ? new[] { 256 } : new[] { 256 - nextWeight, nextWeight }
        );
      }
      else
      {
        double start = index * scale;
        double end = (index + 1) * scale;
        int first = (int)start;
        int last = Math.Min(sourceSize - 1, (int)Math.Ceiling(end) - 1);
        var weights = new int[last - first + 1];
        int total = 0;
        for (int tap = 0; tap < weights.Length - 1; tap++)
        {
          double overlap = Math.Min(end, first + tap + 1) - Math.Max(start, first + tap);
          weights[tap] = (int)Math.Round(overlap / scale * 256);
          total += weights[tap];
        }
        weights[weights.Length - 1] = 256 - total;
        samples[index] = new Sample(first, weights);
      }
    }
    return samples;
  }

  private static byte Clamp(int value) => (byte)Math.Max(0, Math.Min(255, value));
}
