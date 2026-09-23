using System;
using System.Buffers.Binary;

namespace TUFReplay.Submission.Protocol;

public sealed class UploadFrame
{
  public long Sequence { get; }
  public byte[] Bytes { get; }

  private UploadFrame(long sequence, byte[] bytes)
  {
    Sequence = sequence;
    Bytes = bytes;
  }

  public UploadFrame ForRun(Guid id)
  {
    var bytes = new byte[20 + Bytes.Length];
    bytes[0] = (byte)'T';
    bytes[1] = (byte)'U';
    bytes[2] = (byte)'F';
    bytes[3] = (byte)'2';
    // Guid.ToByteArray uses mixed endianness; the wire UUID is RFC 4122/network order.
    byte[] guid = id.ToByteArray();
    int[] order = { 3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15 };
    for (int i = 0; i < 16; i++)
      bytes[4 + i] = guid[order[i]];
    Buffer.BlockCopy(Bytes, 0, bytes, 20, Bytes.Length);
    return new UploadFrame(Sequence, bytes);
  }

  public UploadFrame(byte kind, long sequence, byte[] payload)
  {
    if (kind > 5 || sequence < 0 || payload == null || payload.Length == 0 || payload.Length > 65536)
      throw new ArgumentException("Invalid upload frame.");
    Sequence = sequence;
    Bytes = new byte[20 + payload.Length];
    Bytes[0] = (byte)'T';
    Bytes[1] = (byte)'U';
    Bytes[2] = (byte)'F';
    Bytes[3] = (byte)'R';
    Bytes[4] = 1;
    Bytes[5] = kind;
    BinaryPrimitives.WriteInt64BigEndian(Bytes.AsSpan(8, 8), sequence);
    BinaryPrimitives.WriteInt32BigEndian(Bytes.AsSpan(16, 4), payload.Length);
    payload.CopyTo(Bytes, 20);
  }
}
