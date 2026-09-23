using System;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Importing.Json;

internal static class VisualJsonSanitizer
{
  public static JToken Sanitize(JToken token)
  {
    int nodes = 0;
    return Sanitize(token, 0, ref nodes);
  }

  public static bool IsExecutableProperty(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return false;
    string lower = name.ToLowerInvariant();
    return lower.Contains("javascript")
      || lower == "js"
      || lower == "customjs"
      || lower == "usecustomjs"
      || lower == "custom_js"
      || lower == "use_custom_js"
      || lower.EndsWith("js", StringComparison.Ordinal)
      || lower.Contains("plugin")
      || lower.Contains("sound")
      || lower.Contains("audio")
      || lower.Contains("script")
      || lower == "onclick"
      || lower.Contains("eventhandler");
  }

  private static JToken Sanitize(JToken token, int depth, ref int nodes)
  {
    if (++nodes > VisualImportLimits.MaxJsonNodes || depth > VisualImportLimits.MaxJsonDepth)
      throw new VisualImportException("visual_payload_too_large", "The visual preset JSON is too complex.");

    if (token is JObject obj)
    {
      var copy = new JObject();
      foreach (JProperty property in obj.Properties())
      {
        if (IsExecutableProperty(property.Name))
          continue;
        copy.Add(property.Name, Sanitize(property.Value, depth + 1, ref nodes));
      }
      return copy;
    }

    if (token is JArray array)
    {
      var copy = new JArray();
      foreach (JToken item in array)
        copy.Add(Sanitize(item, depth + 1, ref nodes));
      return copy;
    }

    return token.DeepClone();
  }
}
