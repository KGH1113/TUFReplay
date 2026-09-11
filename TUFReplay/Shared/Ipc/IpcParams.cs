using System.Collections.Generic;
using AdofaiIpc.Core;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Shared.Ipc;

public static class IpcParams
{
  public static bool TryRequiredString(IpcRequest request, string name, out string value)
  {
    value = null;
    JToken token = GetToken(request, name);
    if (token == null || token.Type != JTokenType.String)
      return false;

    string parsed = token.Value<string>();
    if (string.IsNullOrWhiteSpace(parsed))
      return false;

    value = parsed.Trim();
    return true;
  }

  public static bool TryRequiredStringArray(IpcRequest request, string name, out List<string> values)
  {
    values = null;
    JToken token = GetToken(request, name);
    if (!(token is JArray array) || array.Count == 0)
      return false;

    var parsed = new List<string>(array.Count);
    foreach (JToken item in array)
    {
      if (item.Type != JTokenType.String || string.IsNullOrWhiteSpace(item.Value<string>()))
        return false;
      string value = item.Value<string>().Trim();
      if (!parsed.Contains(value))
        parsed.Add(value);
    }
    values = parsed;
    return values.Count > 0;
  }

  public static string OptionalString(IpcRequest request, string name)
  {
    JToken token = GetToken(request, name);
    if (token == null || token.Type == JTokenType.Null)
      return null;
    return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
  }

  public static bool TryNullableString(IpcRequest request, string name, out string value)
  {
    value = null;
    if (!(request?.Params is JObject obj) || !obj.TryGetValue(name, out JToken token))
      return false;

    if (token == null || token.Type == JTokenType.Null)
      return true;
    if (token.Type != JTokenType.String)
      return false;

    string parsed = token.Value<string>();
    value = string.IsNullOrWhiteSpace(parsed) ? null : parsed.Trim();
    return true;
  }

  public static int? OptionalInt(IpcRequest request, string name)
  {
    JToken token = GetToken(request, name);
    if (token == null || token.Type == JTokenType.Null)
      return null;

    if (token.Type == JTokenType.Integer)
      return token.Value<int>();

    return int.TryParse(token.ToString(), out int value) ? value : null;
  }

  public static bool TryBool(IpcRequest request, string name, out bool value)
  {
    value = false;
    JToken token = GetToken(request, name);
    if (token == null || token.Type != JTokenType.Boolean)
      return false;

    value = token.Value<bool>();
    return true;
  }

  private static JToken GetToken(IpcRequest request, string name)
  {
    return request?.Params is JObject obj ? obj[name] : null;
  }
}
