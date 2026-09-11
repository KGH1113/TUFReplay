using System;
using System.Buffers.Binary;

namespace TUFReplay.Submission.Protocol;

public sealed class UploadFrame
{
  public long Sequence { get; }
  public byte[] Bytes { get; }

  public UploadFrame(byte kind, long sequence, byte[] payload)
  {
    if (kind > 5 || sequence < 0 || payload == null || payload.Length == 0 || payload.Length > 65536)
      throw new ArgumentException("Invalid upload frame.");
    Sequence = sequence;
    Bytes = new byte[20 + payload.Length];
    Bytes[0] = (byte)'T'; Bytes[1] = (byte)'U'; Bytes[2] = (byte)'F'; Bytes[3] = (byte)'R';
    Bytes[4] = 1; Bytes[5] = kind;
    BinaryPrimitives.WriteInt64BigEndian(Bytes.AsSpan(8, 8), sequence);
    BinaryPrimitives.WriteInt32BigEndian(Bytes.AsSpan(16, 4), payload.Length);
    payload.CopyTo(Bytes, 20);
  }
}
