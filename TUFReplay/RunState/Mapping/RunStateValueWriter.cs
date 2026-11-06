using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>
/// Writes a state value as JSON following its <see cref="RunStateType"/>, so only values of the
/// declared shape reach the database. Floats must be finite, enums named members, and nested
/// objects exactly their declared type.
/// </summary>
internal static class RunStateValueWriter
{
  /// <summary>The largest saved value, in UTF-8 bytes.</summary>
  public const int MaxBytes = 64 * 1024;

  public static string Write(RunStateType type, object value)
  {
    var text = new LimitedStringWriter(MaxBytes);
    try
    {
      using (var writer = new JsonTextWriter(text) { Formatting = Formatting.None, CloseOutput = false })
        WriteValue(writer, type, value, RunStateTypeNames.Of(type.ClrType));
    }
    catch (LimitExceededException)
    {
      throw TooLarge(type, null);
    }
    string json = text.ToString();
    int bytes = Encoding.UTF8.GetByteCount(json);
    if (bytes > MaxBytes)
      throw TooLarge(type, bytes);
    return json;
  }

  private static RunStateValueException TooLarge(RunStateType type, int? bytes)
  {
    return new RunStateValueException(
      RunStateTypeNames.Of(type.ClrType)
        + " is "
        + (bytes.HasValue ? (bytes.Value / 1024 + 1) + " KiB" : "over " + MaxBytes / 1024 + " KiB")
        + "; a run state can hold up to "
        + MaxBytes / 1024
        + " KiB. Save less, such as totals instead of every event."
    );
  }

  private static void WriteValue(JsonWriter writer, RunStateType type, object value, string path)
  {
    if (value == null)
    {
      writer.WriteNull();
      return;
    }
    if (type.Underlying != null)
    {
      WriteValue(writer, type.Underlying, value, path);
      return;
    }

    switch (type.Shape.Kind)
    {
      case RunStateKind.Boolean:
        writer.WriteValue((bool)value);
        return;
      case RunStateKind.Int32:
        writer.WriteValue((int)value);
        return;
      case RunStateKind.Int64:
        writer.WriteValue((long)value);
        return;
      case RunStateKind.Float32:
        float single = (float)value;
        if (float.IsNaN(single) || float.IsInfinity(single))
          throw NotFinite(path, single);
        writer.WriteValue(single);
        return;
      case RunStateKind.Float64:
        double number = (double)value;
        if (double.IsNaN(number) || double.IsInfinity(number))
          throw NotFinite(path, number);
        writer.WriteValue(number);
        return;
      case RunStateKind.String:
        writer.WriteValue((string)value);
        return;
      case RunStateKind.Enum:
        string name = Enum.GetName(type.ClrType, value);
        if (name == null)
          throw new RunStateValueException(
            path
              + " holds "
              + Convert.ToInt64(value, CultureInfo.InvariantCulture)
              + ", which is not a named member of "
              + RunStateTypeNames.Of(type.ClrType)
              + ". Enums are saved by member name."
          );
        writer.WriteValue(name);
        return;
      case RunStateKind.DateTime:
        var time = (DateTime)value;
        DateTime utc =
          time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        writer.WriteValue(utc.ToString("o", CultureInfo.InvariantCulture));
        return;
      case RunStateKind.Bytes:
        writer.WriteValue(Convert.ToBase64String((byte[])value));
        return;
      case RunStateKind.List:
        writer.WriteStartArray();
        int index = 0;
        foreach (object item in (IList)value)
          WriteValue(writer, type.Element, item, path + "[" + index++ + "]");
        writer.WriteEndArray();
        return;
      case RunStateKind.Map:
        writer.WriteStartObject();
        foreach (DictionaryEntry entry in (IDictionary)value)
        {
          var key = (string)entry.Key;
          writer.WritePropertyName(key);
          WriteValue(writer, type.Element, entry.Value, path + "[\"" + key + "\"]");
        }
        writer.WriteEndObject();
        return;
      case RunStateKind.Object:
        if (value.GetType() != type.ClrType)
          throw new RunStateValueException(
            path
              + " holds a "
              + RunStateTypeNames.Of(value.GetType())
              + " but is declared as "
              + RunStateTypeNames.Of(type.ClrType)
              + ". Run states save the declared type only, so declare the exact type."
          );
        writer.WriteStartObject();
        foreach (RunStateMember member in type.Members)
        {
          writer.WritePropertyName(member.Name);
          WriteValue(writer, member.Type, member.GetValue(value), path + "." + member.Name);
        }
        writer.WriteEndObject();
        return;
    }
  }

  private static RunStateValueException NotFinite(string path, double value)
  {
    return new RunStateValueException(
      path
        + " is "
        + value.ToString(CultureInfo.InvariantCulture)
        + ". Run states save finite numbers only; use a nullable number for a missing value."
    );
  }

  private sealed class LimitExceededException : Exception { }

  /// <summary>Stops writing once the text can no longer fit, so a huge value never builds a huge string.</summary>
  private sealed class LimitedStringWriter : StringWriter
  {
    private readonly int _maxChars;

    public LimitedStringWriter(int maxBytes)
      : base(CultureInfo.InvariantCulture)
    {
      // UTF-8 needs at least one byte per UTF-16 char.
      _maxChars = maxBytes;
    }

    public override void Write(char value)
    {
      Reserve(1);
      base.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
      Reserve(count);
      base.Write(buffer, index, count);
    }

    public override void Write(string value)
    {
      Reserve(value?.Length ?? 0);
      base.Write(value);
    }

    private void Reserve(int count)
    {
      if (GetStringBuilder().Length + count > _maxChars)
        throw new LimitExceededException();
    }
  }
}
