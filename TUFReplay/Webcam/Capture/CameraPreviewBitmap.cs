using System;
using System.Buffers.Binary;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

// The shared preview is bottom-up RGBA. BMP preserves that orientation without
// a Unity texture or image encoder. Recording pixels remain untouched.
public static class CameraPreviewBitmap
{
  private const int HeaderSize = 54;

  public static byte[] Encode(byte[] rgba, CameraFrameSize source, out CameraFrameSize target)
  {
    byte[] bytes = null;
    EncodeInto(rgba, source, ref bytes, out target);
    return bytes;
  }

  // Reuses the destination while the preview size is unchanged; the companion polls
  // several times per second and a fresh ~0.7 MB array per frame feeds GC pauses.
  public static void EncodeInto(byte[] rgba, CameraFrameSize source, ref byte[] destination, out CameraFrameSize target)
  {
    if (rgba == null || rgba.Length != source.ByteCount)
      throw new ArgumentException("Invalid preview pixels.");
    target = CameraFrameSize.Fit(source.Width, source.Height, 640, 480);
    int stride = (target.Width * 3 + 3) & ~3;
    int imageSize = stride * target.Height;
    int length = HeaderSize + imageSize;
    if (destination == null || destination.Length != length)
      destination = new byte[length];

    byte[] bytes = destination;
    Span<byte> header = bytes.AsSpan(0, HeaderSize);
    header.Clear();
    header[0] = (byte)'B';
    header[1] = (byte)'M';
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(2), length);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(10), HeaderSize);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(14), 40);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(18), target.Width);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(22), target.Height);
    BinaryPrimitives.WriteInt16LittleEndian(header.Slice(26), 1);
    BinaryPrimitives.WriteInt16LittleEndian(header.Slice(28), 24);
    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(34), imageSize);

    int padding = stride - target.Width * 3;
    for (int y = 0; y < target.Height; y++)
    {
      int row = HeaderSize + y * stride;
      for (int x = 0; x < target.Width; x++)
      {
        int sourceIndex = ((y * source.Height / target.Height) * source.Width + x * source.Width / target.Width) * 4;
        int offset = row + x * 3;
        bytes[offset] = rgba[sourceIndex + 2];
        bytes[offset + 1] = rgba[sourceIndex + 1];
        bytes[offset + 2] = rgba[sourceIndex];
      }
      if (padding != 0)
        Array.Clear(bytes, row + target.Width * 3, padding);
    }
  }
}
