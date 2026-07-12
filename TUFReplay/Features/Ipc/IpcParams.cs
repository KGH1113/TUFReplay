using AdofaiIpc.Core;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Features.Ipc;

public static class IpcParams
{
  public static string OptionalString(IpcRequest request, string name)
  {
    JToken token = GetToken(request, name);
    if (token == null || token.Type == JTokenType.Null) return null;
    return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
  }

  public static int? OptionalInt(IpcRequest request, string name)
  {
    JToken token = GetToken(request, name);
    if (token == null || token.Type == JTokenType.Null) return null;

    if (token.Type == JTokenType.Integer) return token.Value<int>();

    return int.TryParse(token.ToString(), out int value) ? value : null;
  }

  private static JToken GetToken(IpcRequest request, string name)
  {
    return request?.Params is JObject obj ? obj[name] : null;
  }
}
