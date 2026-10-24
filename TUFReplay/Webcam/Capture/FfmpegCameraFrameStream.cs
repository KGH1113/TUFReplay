using System;
using System.IO;
using System.Text;

namespace TUFReplay.Webcam.Capture;

// A small streaming Matroska writer for one raw I420 video track. Unlike a
// rawvideo pipe, each frame carries its capture time: dropping queued frames
// cannot shorten the recording or shift it relative to the input replay.
// See https://www.matroska.org/technical/codec_specs.html#v-ms-vfw-fourcc.
internal sealed class FfmpegCameraFrameStream
{
  private readonly Stream _output;
  private readonly int _frameByteCount;
  private readonly long _timestampFrequency;
  private readonly long _frameDurationUs;
  private readonly byte[] _packetHeader = new byte[64];
  private bool _hasFrame;
  private long _lastTimestampTicks;

  public long FirstTimestampTicks { get; private set; }
  public long LastTimestampUs { get; private set; }
  public long DurationUs => _hasFrame ? LastTimestampUs + _frameDurationUs : 0;

  public FfmpegCameraFrameStream(Stream output, int width, int height, int frameRate, long timestampFrequency)
  {
    if (output == null)
      throw new ArgumentNullException(nameof(output));
    if (width < 2 || height < 2 || (width & 1) != 0 || (height & 1) != 0)
      throw new ArgumentOutOfRangeException(nameof(width));
    if (frameRate < 1 || frameRate > 1000)
      throw new ArgumentOutOfRangeException(nameof(frameRate));
    if (timestampFrequency < 1)
      throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
    _output = output;
    _frameByteCount = checked(width * height * 3 / 2);
    _timestampFrequency = timestampFrequency;
    _frameDurationUs = Math.Max(1, 1_000_000 / frameRate);
    WriteHeader(width, height, frameRate);
  }

  // The caller keeps ownership of the frame until this synchronous write ends.
  // The array may be a larger pool bucket; only the logical I420 bytes are sent.
  public void WriteFrame(byte[] pixels, long timestampTicks)
  {
    if (pixels == null || pixels.Length < _frameByteCount)
      throw new ArgumentException("The camera frame is shorter than its resolution requires.", nameof(pixels));
    if (_hasFrame && timestampTicks < _lastTimestampTicks)
      throw new ArgumentOutOfRangeException(nameof(timestampTicks), "Camera timestamps must be monotonic.");
    long first = _hasFrame ? FirstTimestampTicks : timestampTicks;
    long elapsed = checked(timestampTicks - first);
    // Splitting whole seconds avoids overflowing when recording for many hours.
    long timestampUs = checked(
      elapsed / _timestampFrequency * 1_000_000
      + (long)(elapsed % _timestampFrequency * (1_000_000d / _timestampFrequency))
    );
    // One known-size cluster per frame keeps the block's signed relative
    // timestamp zero, including after a long scheduling or camera stall.
    int offset = 0;
    WriteId(_packetHeader, ref offset, 0x1F43B675);
    int timeBytes = UnsignedSize((ulong)timestampUs);
    int blockBytes = checked(_frameByteCount + 4);
    int clusterBytes = checked(1 + 1 + timeBytes + 1 + VintSize((ulong)blockBytes) + blockBytes);
    WriteVint(_packetHeader, ref offset, (ulong)clusterBytes);
    WriteId(_packetHeader, ref offset, 0xE7);
    WriteVint(_packetHeader, ref offset, (ulong)timeBytes);
    WriteUnsigned(_packetHeader, ref offset, (ulong)timestampUs, timeBytes);
    WriteId(_packetHeader, ref offset, 0xA3);
    WriteVint(_packetHeader, ref offset, (ulong)blockBytes);
    _packetHeader[offset++] = 0x81; // Track 1.
    _packetHeader[offset++] = 0;
    _packetHeader[offset++] = 0;
    _packetHeader[offset++] = 0x80; // Keyframe, no lacing.
    _output.Write(_packetHeader, 0, offset);
    _output.Write(pixels, 0, _frameByteCount);
    FirstTimestampTicks = first;
    _lastTimestampTicks = timestampTicks;
    LastTimestampUs = timestampUs;
    _hasFrame = true;
  }

  private void WriteHeader(int width, int height, int frameRate)
  {
    // Only small metadata is allocated here, once per prepared encoder.
    WriteElement(
      _output,
      0x1A45DFA3,
      Elements(
        UIntElement(0x4286, 1),
        UIntElement(0x42F7, 1),
        UIntElement(0x42F2, 4),
        UIntElement(0x42F3, 8),
        StringElement(0x4282, "matroska"),
        UIntElement(0x4287, 4),
        UIntElement(0x4285, 2)
      )
    );
    // A live pipe cannot seek back to fill the segment length.
    byte[] segment = { 0x18, 0x53, 0x80, 0x67, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
    _output.Write(segment, 0, segment.Length);
    WriteElement(
      _output,
      0x1549A966,
      Elements(
        UIntElement(0x2AD7B1, 1000), // Segment ticks are microseconds.
        StringElement(0x4D80, "TUFReplay"),
        StringElement(0x5741, "TUFReplay")
      )
    );
    byte[] bitmap = new byte[40];
    WriteLittleEndian(bitmap, 0, 40);
    WriteLittleEndian(bitmap, 4, width);
    WriteLittleEndian(bitmap, 8, height);
    bitmap[12] = 1; // biPlanes.
    bitmap[14] = 12; // I420 bits per pixel.
    bitmap[16] = (byte)'I';
    bitmap[17] = (byte)'4';
    bitmap[18] = (byte)'2';
    bitmap[19] = (byte)'0';
    WriteLittleEndian(bitmap, 20, _frameByteCount);
    WriteElement(
      _output,
      0x1654AE6B,
      Element(
        0xAE,
        Elements(
          UIntElement(0xD7, 1),
          UIntElement(0x73C5, 1),
          UIntElement(0x83, 1),
          UIntElement(0x9C, 0),
          StringElement(0x86, "V_MS/VFW/FOURCC"),
          Element(0x63A2, bitmap),
          UIntElement(0x23E383, (ulong)(1_000_000_000L / frameRate)),
          Element(0xE0, Elements(UIntElement(0xB0, (ulong)width), UIntElement(0xBA, (ulong)height)))
        )
      )
    );
  }

  private static byte[] UIntElement(uint id, ulong value)
  {
    var bytes = new byte[UnsignedSize(value)];
    int offset = 0;
    WriteUnsigned(bytes, ref offset, value, bytes.Length);
    return Element(id, bytes);
  }

  private static byte[] StringElement(uint id, string value) => Element(id, Encoding.UTF8.GetBytes(value));

  private static byte[] Element(uint id, byte[] content)
  {
    using var stream = new MemoryStream();
    WriteElement(stream, id, content);
    return stream.ToArray();
  }

  private static byte[] Elements(params byte[][] elements)
  {
    using var stream = new MemoryStream();
    foreach (byte[] element in elements)
      stream.Write(element, 0, element.Length);
    return stream.ToArray();
  }

  private static void WriteElement(Stream stream, uint id, byte[] content)
  {
    byte[] header = new byte[12];
    int offset = 0;
    WriteId(header, ref offset, id);
    WriteVint(header, ref offset, (ulong)content.Length);
    stream.Write(header, 0, offset);
    stream.Write(content, 0, content.Length);
  }

  private static int UnsignedSize(ulong value)
  {
    int count = 1;
    while (count < 8 && value >> (count * 8) != 0)
      count++;
    return count;
  }

  private static int VintSize(ulong value)
  {
    int count = 1;
    // All-one payloads mean unknown length, so reserve them at each size.
    while (count < 8 && value >= (1UL << (count * 7)) - 1)
      count++;
    if (value >= (1UL << 56) - 1)
      throw new ArgumentOutOfRangeException(nameof(value));
    return count;
  }

  private static void WriteVint(byte[] target, ref int offset, ulong value)
  {
    int count = VintSize(value);
    WriteUnsigned(target, ref offset, value | (1UL << (count * 7)), count);
  }

  private static void WriteId(byte[] target, ref int offset, uint value) =>
    WriteUnsigned(target, ref offset, value, UnsignedSize(value));

  private static void WriteUnsigned(byte[] target, ref int offset, ulong value, int count)
  {
    for (int index = count - 1; index >= 0; index--)
      target[offset++] = (byte)(value >> (index * 8));
  }

  private static void WriteLittleEndian(byte[] target, int offset, int value)
  {
    for (int index = 0; index < 4; index++)
      target[offset + index] = (byte)(value >> (index * 8));
  }
}
