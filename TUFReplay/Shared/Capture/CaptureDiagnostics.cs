using System;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Shared.Capture;

internal static class CaptureDiagnostics
{
  internal static JObject CreateRecord(string eventName, object data = null, Exception exception = null)
  {
    var record = new JObject
    {
      ["utc"] = DateTime.UtcNow.ToString("O"),
      ["threadId"] = Thread.CurrentThread.ManagedThreadId,
      ["event"] = eventName,
      ["data"] = data == null ? null : JToken.FromObject(data),
    };
    if (exception != null)
      record["exception"] = new JObject
      {
        ["type"] = exception.GetType().FullName,
        ["hResult"] = exception.HResult,
        ["detail"] = exception.ToString(),
      };
    return record;
  }

  // Diagnostics must never turn a recoverable camera failure into another failure.
  // Call from capture workers; no per-frame serialization or disk I/O belongs in Unity Update.
  internal static void Record(string eventName, object data = null, Exception exception = null)
  {
    try
    {
      Main.Instance?.Log("[Camera/Diagnostics] " + CreateRecord(eventName, data, exception).ToString(Formatting.None));
    }
    catch { }
  }
}
