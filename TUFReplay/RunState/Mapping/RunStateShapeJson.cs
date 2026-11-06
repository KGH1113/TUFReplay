using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>
/// Writes a shape as canonical JSON (fixed key order, fields and enum names sorted) and reads it
/// back. The shape id is the SHA-256 of that JSON, so equal shapes share one id.
/// </summary>
internal static class RunStateShapeJson
{
  public static string Write(RunStateShape shape)
  {
    var text = new StringWriter(CultureInfo.InvariantCulture);
    using (var writer = new JsonTextWriter(text) { Formatting = Formatting.None })
      WriteShape(writer, shape);
    return text.ToString();
  }

  public static string IdOf(string shapeJson)
  {
    using SHA256 sha = SHA256.Create();
    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(shapeJson));
    var id = new StringBuilder(hash.Length * 2);
    foreach (byte value in hash)
      id.Append(value.ToString("x2", CultureInfo.InvariantCulture));
    return id.ToString();
  }

  public static RunStateShape Parse(string shapeJson)
  {
    using var reader = new JsonTextReader(new StringReader(shapeJson)) { DateParseHandling = DateParseHandling.None };
    return ReadShape((JObject)JToken.ReadFrom(reader));
  }

  private static void WriteShape(JsonWriter writer, RunStateShape shape)
  {
    writer.WriteStartObject();
    writer.WritePropertyName("kind");
    writer.WriteValue(KindName(shape.Kind));
    if (shape.Nullable)
    {
      writer.WritePropertyName("nullable");
      writer.WriteValue(true);
    }
    switch (shape.Kind)
    {
      case RunStateKind.Enum:
        writer.WritePropertyName("names");
        writer.WriteStartArray();
        foreach (string name in shape.EnumNames)
          writer.WriteValue(name);
        writer.WriteEndArray();
        break;
      case RunStateKind.List:
        writer.WritePropertyName("items");
        WriteShape(writer, shape.Element);
        break;
      case RunStateKind.Map:
        writer.WritePropertyName("values");
        WriteShape(writer, shape.Element);
        break;
      case RunStateKind.Object:
        writer.WritePropertyName("fields");
        writer.WriteStartObject();
        foreach (RunStateField field in shape.Fields)
        {
          writer.WritePropertyName(field.Name);
          WriteShape(writer, field.Shape);
        }
        writer.WriteEndObject();
        break;
    }
    writer.WriteEndObject();
  }

  private static RunStateShape ReadShape(JObject json)
  {
    var shape = new RunStateShape
    {
      Kind = ParseKind((string)json["kind"]),
      Nullable = json["nullable"]?.Value<bool>() == true,
    };
    switch (shape.Kind)
    {
      case RunStateKind.Enum:
        shape.EnumNames = ((JArray)json["names"]).Select(name => (string)name).ToArray();
        break;
      case RunStateKind.List:
        shape.Element = ReadShape((JObject)json["items"]);
        break;
      case RunStateKind.Map:
        shape.Element = ReadShape((JObject)json["values"]);
        break;
      case RunStateKind.Object:
        var fields = new List<RunStateField>();
        foreach (JProperty field in ((JObject)json["fields"]).Properties())
          fields.Add(new RunStateField { Name = field.Name, Shape = ReadShape((JObject)field.Value) });
        shape.Fields = fields.ToArray();
        break;
    }
    return shape;
  }

  private static readonly string[] KindNames =
  {
    "bool",
    "int32",
    "int64",
    "float32",
    "float64",
    "string",
    "enum",
    "datetime",
    "bytes",
    "list",
    "map",
    "object",
  };

  private static string KindName(RunStateKind kind) => KindNames[(int)kind];

  private static RunStateKind ParseKind(string name)
  {
    int index = Array.IndexOf(KindNames, name);
    if (index < 0)
      throw new InvalidDataException("Unknown run state kind: " + name);
    return (RunStateKind)index;
  }
}
