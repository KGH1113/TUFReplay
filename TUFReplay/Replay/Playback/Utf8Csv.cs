using System;
using System.Buffers.Text;

namespace TUFReplay.Replay.Playback;

internal static class Utf8Csv
{
  public static int CountNonEmptyLines(ReadOnlySpan<byte> payload)
  {
    int count = 0;
    bool hasContent = false;

    for (int i = 0; i < payload.Length; i++)
    {
      if (payload[i] == (byte)'\n' || payload[i] == (byte)'\r')
      {
        if (hasContent)
        {
          count++;
          hasContent = false;
        }
      }
      else
      {
        hasContent = true;
      }
    }

    return hasContent ? count + 1 : count;
  }

  public static bool TryReadNonEmptyLine(ReadOnlySpan<byte> payload, ref int offset, out ReadOnlySpan<byte> line)
  {
    while (offset < payload.Length && IsLineBreak(payload[offset]))
      offset++;

    if (offset >= payload.Length)
    {
      line = default;
      return false;
    }

    int start = offset;
    while (offset < payload.Length && !IsLineBreak(payload[offset]))
      offset++;

    line = payload.Slice(start, offset - start);
    return true;
  }

  public static bool TrySplit(ReadOnlySpan<byte> line, Span<Range> fields)
  {
    int fieldIndex = 0;
    int fieldStart = 0;

    for (int i = 0; i < line.Length; i++)
    {
      if (line[i] != (byte)',')
        continue;
      if (fieldIndex >= fields.Length - 1)
        return false;

      fields[fieldIndex++] = fieldStart..i;
      fieldStart = i + 1;
    }

    if (fieldIndex != fields.Length - 1)
      return false;

    fields[fieldIndex] = fieldStart..line.Length;
    return true;
  }

  public static bool TrySplit(ReadOnlySpan<char> line, Span<Range> fields)
  {
    int fieldIndex = 0;
    int fieldStart = 0;

    for (int i = 0; i < line.Length; i++)
    {
      if (line[i] != ',')
        continue;
      if (fieldIndex >= fields.Length - 1)
        return false;

      fields[fieldIndex++] = fieldStart..i;
      fieldStart = i + 1;
    }

    if (fieldIndex != fields.Length - 1)
      return false;

    fields[fieldIndex] = fieldStart..line.Length;
    return true;
  }

  public static bool TryParseInt32(ReadOnlySpan<byte> value, out int result)
  {
    value = TrimWhitespace(value);
    return Utf8Parser.TryParse(value, out result, out int consumed) && consumed == value.Length;
  }

  public static bool TryParseInt64(ReadOnlySpan<byte> value, out long result)
  {
    value = TrimWhitespace(value);
    return Utf8Parser.TryParse(value, out result, out int consumed) && consumed == value.Length;
  }

  public static bool TryParseUInt16(ReadOnlySpan<byte> value, out ushort result)
  {
    value = TrimWhitespace(value);
    return Utf8Parser.TryParse(value, out result, out int consumed) && consumed == value.Length;
  }

  public static bool TryParseSingle(ReadOnlySpan<byte> value, out float result)
  {
    value = TrimWhitespace(value);
    return Utf8Parser.TryParse(value, out result, out int consumed) && consumed == value.Length;
  }

  public static bool TryParseDouble(ReadOnlySpan<byte> value, out double result)
  {
    value = TrimWhitespace(value);
    return Utf8Parser.TryParse(value, out result, out int consumed) && consumed == value.Length;
  }

  public static bool TryParseBoolean(ReadOnlySpan<byte> value, out bool result)
  {
    value = TrimWhitespace(value);

    if (value.Length == 1 && value[0] == (byte)'1')
    {
      result = true;
      return true;
    }

    if (value.Length == 1 && value[0] == (byte)'0')
    {
      result = false;
      return true;
    }

    if (EqualsAsciiIgnoreCase(value, "true"u8))
    {
      result = true;
      return true;
    }

    if (EqualsAsciiIgnoreCase(value, "false"u8))
    {
      result = false;
      return true;
    }

    result = false;
    return false;
  }

  private static bool IsLineBreak(byte value)
  {
    return value == (byte)'\n' || value == (byte)'\r';
  }

  private static ReadOnlySpan<byte> TrimWhitespace(ReadOnlySpan<byte> value)
  {
    int start = 0;
    while (start < value.Length && IsAsciiWhitespace(value[start]))
      start++;

    int end = value.Length;
    while (end > start && IsAsciiWhitespace(value[end - 1]))
      end--;

    return value.Slice(start, end - start);
  }

  private static bool IsAsciiWhitespace(byte value)
  {
    return value == (byte)' ' || (value >= (byte)'\t' && value <= (byte)'\r');
  }

  private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> expectedLowercase)
  {
    if (value.Length != expectedLowercase.Length)
      return false;

    for (int i = 0; i < value.Length; i++)
    {
      byte current = value[i];
      if (current >= (byte)'A' && current <= (byte)'Z')
        current = (byte)(current + ((byte)'a' - (byte)'A'));
      if (current != expectedLowercase[i])
        return false;
    }

    return true;
  }
}
