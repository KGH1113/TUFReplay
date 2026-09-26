using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Validation;

// Independent of activity hash v4 and replay semantic hash v1. The same
// embedded field contract and golden vectors are consumed by the Rust server.
public static class SubmissionGameplayHash
{
  public const int Version = 1;
  private static readonly JObject Contract = LoadContract();
  private static readonly HashSet<string> VisualEvents = new HashSet<string>(
    Contract["visualEvents"].Values<string>(),
    StringComparer.Ordinal
  );

  public static bool IsGameplayEvent(string name) => Contract["events"][name] != null;

  public static bool IsVisualEvent(string name) => VisualEvents.Contains(name);

  internal static JArray EventFields(string name) => (JArray)Contract["events"][name];

  private static JObject LoadContract()
  {
    using Stream stream = typeof(SubmissionGameplayHash).Assembly.GetManifestResourceStream(
      "TUFReplay.Submission.GameplayV1"
    );
    using var reader = new StreamReader(stream);
    return JObject.Parse(reader.ReadToEnd());
  }

  // Accepts decoded runtime settings/angles/events. Kept Unity-free so both
  // implementations can be checked against identical contract fixtures.
  public static byte[] Compute(JObject settings, JArray angles, JArray actions, string legacyPath = null)
  {
    using var stream = new MemoryStream();
    void Int(int value)
    {
      stream.WriteByte((byte)(value >> 24));
      stream.WriteByte((byte)(value >> 16));
      stream.WriteByte((byte)(value >> 8));
      stream.WriteByte((byte)value);
    }
    void Float(float value)
    {
      if (float.IsNaN(value) || float.IsInfinity(value))
        throw new InvalidDataException("Non-finite gameplay number.");
      byte[] bytes = BitConverter.GetBytes(value == 0f ? 0f : value);
      if (BitConverter.IsLittleEndian)
        Array.Reverse(bytes);
      stream.Write(bytes, 0, bytes.Length);
    }
    void Text(string value)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(value);
      Int(bytes.Length);
      stream.Write(bytes, 0, bytes.Length);
    }
    void Fields(JObject source, JToken fields, string eventType = null)
    {
      foreach (JArray field in fields)
      {
        string key = (string)field[0];
        JToken value = source[key] ?? field[2];
        // Inactive SetSpeed alternatives are not gameplay.
        string speedType = EnumText("speedType", source["speedType"] ?? "Bpm");
        if (
          eventType == "SetSpeed"
          && ((key == "beatsPerMinute" && speedType == "Multiplier") || (key == "bpmMultiplier" && speedType == "Bpm"))
        )
          value = field[2];
        switch ((string)field[1])
        {
          case "float":
            Float((float)value);
            break;
          case "int":
            Int(Convert.ToInt32((double)value));
            break;
          case "bool":
            bool flag =
              value.Type == JTokenType.Boolean ? (bool)value
              : (string)value == "Enabled" ? true
              : (string)value == "Disabled" ? false
              : throw new InvalidDataException("Invalid gameplay boolean.");
            stream.WriteByte(flag ? (byte)1 : (byte)0);
            break;
          case "string":
            Text(EnumText(key, value));
            break;
          case "vector":
            if (!(value is JArray vector) || vector.Count != 2)
              throw new InvalidDataException("Invalid gameplay vector.");
            Float((float)vector[0]);
            Float((float)vector[1]);
            break;
          default:
            throw new InvalidDataException("Unknown gameplay field.");
        }
      }
    }
    Text("tuf-submission-gameplay");
    Int(Version);
    Fields(settings, Contract["settings"]);
    // LevelData.Decode retains pathData for legacy sprite charts. Keep that
    // representation distinct from modern angleData without changing v1 hashes.
    Int(legacyPath == null ? angles.Count : -1);
    if (legacyPath != null)
      Text(legacyPath);
    if (legacyPath == null)
      foreach (JToken angle in angles)
      {
        float value = (float)angle;
        if (value != 999f)
        {
          value %= 360f;
          if (value < 0)
            value += 360f;
        }
        Float(value);
      }
    var events = actions
      .Cast<JObject>()
      .Where(action => action["active"]?.Value<bool>() != false)
      .Where(action =>
      {
        string name = (string)action["eventType"];
        if (IsGameplayEvent(name))
          return true;
        if (!IsVisualEvent(name))
          throw new InvalidDataException("Unsupported gameplay event: " + name);
        return false;
      })
      .OrderBy(action => (int)action["floor"])
      .ThenBy(action => (string)action["eventType"], StringComparer.Ordinal)
      .ToList();
    Int(events.Count);
    foreach (JObject action in events)
    {
      string name = (string)action["eventType"];
      Int((int)action["floor"]);
      Text(name);
      Fields(action, Contract["events"][name], name);
    }
    stream.Position = 0;
    using SHA256 sha = SHA256.Create();
    return sha.ComputeHash(stream);
  }

  private static string EnumText(string key, JToken value)
  {
    string text = (string)value ?? throw new InvalidDataException("Missing gameplay string.");
    return (string)Contract["enums"][key]?[text] ?? text;
  }
}
