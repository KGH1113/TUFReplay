using System;
using System.IO;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

// The shared preview is bottom-up RGBA. BMP preserves that orientation without
// a Unity texture or image encoder. Recording pixels remain untouched.
public static class CameraPreviewBitmap
{
  public static byte[] Encode(byte[] rgba, CameraFrameSize source, out CameraFrameSize target)
  {
    if (rgba == null || rgba.Length != source.ByteCount)
      throw new ArgumentException("Invalid preview pixels.");
    target = CameraFrameSize.Fit(source.Width, source.Height, 640, 480);
    int stride = (target.Width * 3 + 3) & ~3;
    var bytes = new byte[54 + stride * target.Height];
    using (var writer = new BinaryWriter(new MemoryStream(bytes)))
    {
      writer.Write((byte)'B');
      writer.Write((byte)'M');
      writer.Write(bytes.Length);
      writer.Write(0);
      writer.Write(54);
      writer.Write(40);
      writer.Write(target.Width);
      writer.Write(target.Height);
      writer.Write((short)1);
      writer.Write((short)24);
      writer.Write(0);
      writer.Write(stride * target.Height);
      writer.Write(new byte[16]);
    }
    for (int y = 0; y < target.Height; y++)
    for (int x = 0; x < target.Width; x++)
    {
      int sourceIndex = ((y * source.Height / target.Height) * source.Width + x * source.Width / target.Width) * 4;
      int destination = 54 + y * stride + x * 3;
      bytes[destination] = rgba[sourceIndex + 2];
      bytes[destination + 1] = rgba[sourceIndex + 1];
      bytes[destination + 2] = rgba[sourceIndex];
    }
    return bytes;
  }
}
