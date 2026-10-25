using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TUFReplay.Replay.Models;

// Writes CSV rows straight into pooled 64 KiB UTF-8 chunks, so serializing a long run
// allocates only its final exact-length byte[]. Numbers go through TryFormat on the
// stack, the same invariant formatting ToString(...) uses, so the bytes are unchanged.
internal sealed class Utf8CsvWriter : IDisposable
{
  private const int ChunkSize = 64 * 1024;
  private const int MaxFormattedLength = 64;
  private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

  private readonly List<byte[]> _completed = new List<byte[]>();
  private byte[] _current;
  private int _position;
  private long _completedLength;

  public Utf8CsvWriter()
  {
    _current = ArrayPool<byte>.Shared.Rent(ChunkSize);
  }

  public long Length => _completedLength + _position;

  public Utf8CsvWriter Append(char value)
  {
    if (value > 0x7f)
      return AppendText(value.ToString());
    EnsureSpace();
    _current[_position++] = (byte)value;
    return this;
  }

  public Utf8CsvWriter Append(int value)
  {
    Span<char> chars = stackalloc char[MaxFormattedLength];
    return value.TryFormat(chars, out int written, default, Invariant)
      ? AppendFormatted(chars.Slice(0, written))
      : AppendText(value.ToString(Invariant));
  }

  public Utf8CsvWriter Append(long value)
  {
    Span<char> chars = stackalloc char[MaxFormattedLength];
    return value.TryFormat(chars, out int written, default, Invariant)
      ? AppendFormatted(chars.Slice(0, written))
      : AppendText(value.ToString(Invariant));
  }

  public Utf8CsvWriter Append(ulong value)
  {
    Span<char> chars = stackalloc char[MaxFormattedLength];
    return value.TryFormat(chars, out int written, default, Invariant)
      ? AppendFormatted(chars.Slice(0, written))
      : AppendText(value.ToString(Invariant));
  }

  public Utf8CsvWriter AppendRoundTrip(double value)
  {
    Span<char> chars = stackalloc char[MaxFormattedLength];
    return value.TryFormat(chars, out int written, "R", Invariant)
      ? AppendFormatted(chars.Slice(0, written))
      : AppendText(value.ToString("R", Invariant));
  }

  public Utf8CsvWriter AppendRoundTrip(float value)
  {
    Span<char> chars = stackalloc char[MaxFormattedLength];
    return value.TryFormat(chars, out int written, "R", Invariant)
      ? AppendFormatted(chars.Slice(0, written))
      : AppendText(value.ToString("R", Invariant));
  }

  public byte[] ToArray()
  {
    long length = Length;
    if (length > int.MaxValue)
      throw new InvalidOperationException("The CSV exceeds the maximum array length.");

    var result = new byte[length];
    int offset = 0;
    foreach (byte[] chunk in _completed)
    {
      Buffer.BlockCopy(chunk, 0, result, offset, ChunkSize);
      offset += ChunkSize;
    }
    Buffer.BlockCopy(_current, 0, result, offset, _position);
    return result;
  }

  public void Dispose()
  {
    foreach (byte[] chunk in _completed)
      ArrayPool<byte>.Shared.Return(chunk);
    _completed.Clear();
    if (_current != null)
      ArrayPool<byte>.Shared.Return(_current);
    _current = null;
    _position = 0;
    _completedLength = 0;
  }

  private Utf8CsvWriter AppendFormatted(ReadOnlySpan<char> chars)
  {
    // Invariant number formatting is ASCII; keep exact UTF-8 for anything else.
    for (int i = 0; i < chars.Length; i++)
    {
      if (chars[i] > 0x7f)
        return AppendText(chars.ToString());
    }

    for (int i = 0; i < chars.Length; i++)
    {
      EnsureSpace();
      _current[_position++] = (byte)chars[i];
    }
    return this;
  }

  private Utf8CsvWriter AppendText(string text)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(text);
    for (int i = 0; i < bytes.Length; i++)
    {
      EnsureSpace();
      _current[_position++] = bytes[i];
    }
    return this;
  }

  private void EnsureSpace()
  {
    if (_current == null)
      throw new ObjectDisposedException(nameof(Utf8CsvWriter));
    if (_position < ChunkSize)
      return;

    _completed.Add(_current);
    _completedLength += ChunkSize;
    _current = ArrayPool<byte>.Shared.Rent(ChunkSize);
    _position = 0;
  }
}
