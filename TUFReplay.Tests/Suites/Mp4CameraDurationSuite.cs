using System.Buffers.Binary;
using System.Text;
using TUFReplay.Webcam.Capture;
using static TestFixture;

internal static class Mp4CameraDurationSuite
{
  public static void RunAll(string root)
  {
    string path = Path.Combine(root, "camera-duration-fixture.mp4");
    File.WriteAllBytes(
      path,
      Atom("moov", Join(Track("soun", Header(0, 48000, 48000)), Track("vide", Header(0, 1000000, 799999))))
    );
    Assert(
      Mp4CameraDuration.ReadUs(path) == 799999,
      "MP4 duration reader selected the audio track or rounded video timing."
    );

    const ulong largeDuration = 90_000_000_000_000;
    File.WriteAllBytes(path, Atom("moov", Track("vide", Header(1, 1000000, largeDuration), true), true));
    Assert(
      Mp4CameraDuration.ReadUs(path) == (long)largeDuration,
      "Version 1 MP4 duration overflowed before scaling or extended atom headers failed."
    );
    File.WriteAllBytes(path, Atom("moov", Track("vide", Header(0, 90000, 90001))));
    Assert(Mp4CameraDuration.ReadUs(path) == 1000011, "MP4 time-scale conversion lost its fractional remainder.");

    Reject(path, new byte[] { 0, 0, 0 }, "truncated atom header");
    Reject(path, Join(BigEndian(7u), Encoding.ASCII.GetBytes("moov")), "undersized atom");
    Reject(path, Join(BigEndian(1u), Encoding.ASCII.GetBytes("moov")), "truncated extended header");
    Reject(
      path,
      Join(BigEndian(1u), Encoding.ASCII.GetBytes("moov"), BigEndian(ulong.MaxValue)),
      "overflowing extended length"
    );
    Reject(path, Atom("moov", Join(BigEndian(200u), Encoding.ASCII.GetBytes("trak"))), "child escaping its parent");
    Reject(path, Atom("moov", Track("vide", Header(0, 0, 1))), "zero time scale");
    Reject(path, Atom("moov", Track("vide", Header(0, 1000000, uint.MaxValue))), "unknown version 0 duration");
    Reject(path, Atom("moov", Track("vide", Header(1, 1000000, ulong.MaxValue))), "unknown version 1 duration");
    Reject(path, Atom("moov", Track("vide", Header(1, 1, ulong.MaxValue - 1))), "unrepresentable microsecond duration");
    byte[] unknownVersion = Header(0, 1000000, 1);
    unknownVersion[0] = 2;
    Reject(path, Atom("moov", Track("vide", unknownVersion)), "unknown duration version");
    Reject(path, Atom("moov", Track("vide", new byte[4])), "truncated duration payload");
    Reject(path, Atom("moov", Track("soun", Header(0, 48000, 48000))), "no video track");
    byte[] video = Track("vide", Header(0, 1000000, 1));
    Reject(path, Atom("moov", Join(video, video)), "ambiguous video tracks");
    Reject(
      path,
      Join(Enumerable.Repeat(Atom("free", Array.Empty<byte>()), 10001).ToArray()),
      "unbounded atom traversal"
    );
    SkipsLargeMediaPayload(path);
  }

  private static void SkipsLargeMediaPayload(string path)
  {
    const uint mediaSize = 64 * 1024 * 1024;
    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
    {
      stream.Write(BigEndian(mediaSize));
      stream.Write(Encoding.ASCII.GetBytes("mdat"));
      stream.Position = mediaSize;
      stream.Write(Atom("moov", Track("vide", Header(0, 1000000, 1234567))));
    }
    long before = GC.GetAllocatedBytesForCurrentThread();
    Assert(Mp4CameraDuration.ReadUs(path) == 1234567, "MP4 reader could not seek past a large media atom.");
    Assert(
      GC.GetAllocatedBytesForCurrentThread() - before < 1024 * 1024,
      "MP4 duration reader allocated media-sized buffers."
    );
  }

  private static void Reject(string path, byte[] bytes, string reason)
  {
    File.WriteAllBytes(path, bytes);
    try
    {
      Mp4CameraDuration.ReadUs(path);
    }
    catch (InvalidDataException)
    {
      return;
    }
    throw new Exception("MP4 duration reader accepted " + reason + ".");
  }

  private static byte[] Track(string handlerType, byte[] durationHeader, bool extended = false)
  {
    byte[] handler = new byte[12];
    Encoding.ASCII.GetBytes(handlerType).CopyTo(handler, 8);
    return Atom("trak", Atom("mdia", Join(Atom("mdhd", durationHeader), Atom("hdlr", handler)), extended), extended);
  }

  private static byte[] Header(byte version, uint scale, ulong duration)
  {
    byte[] bytes = new byte[version == 0 ? 24 : 36];
    bytes[0] = version;
    if (version == 0)
    {
      BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12), scale);
      BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)duration);
    }
    else
    {
      BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), scale);
      BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(24), duration);
    }
    return bytes;
  }

  private static byte[] Atom(string type, byte[] content, bool extended = false) =>
    extended
      ? Join(BigEndian(1u), Encoding.ASCII.GetBytes(type), BigEndian((ulong)content.Length + 16), content)
      : Join(BigEndian((uint)content.Length + 8), Encoding.ASCII.GetBytes(type), content);

  private static byte[] BigEndian(uint value)
  {
    byte[] bytes = new byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
    return bytes;
  }

  private static byte[] BigEndian(ulong value)
  {
    byte[] bytes = new byte[8];
    BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
    return bytes;
  }

  private static byte[] Join(params byte[][] values)
  {
    using var stream = new MemoryStream();
    foreach (byte[] value in values)
      stream.Write(value);
    return stream.ToArray();
  }
}
