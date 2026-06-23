using System;
using System.Net;
using Newtonsoft.Json;
using TUFReplay.Replay;
using TUFReplay.Shared;

namespace TUFReplay.LocalServer;

public static class LocalApi
{
  public static void Handle(HttpListenerContext context)
  {
    ApplyCorsHeaders(context);

    string method = context.Request.HttpMethod;
    string path = context.Request.Url.AbsolutePath.TrimEnd('/');
    if (path.Length == 0) path = "/";

    if (method == "OPTIONS")
    {
      context.Response.StatusCode = 204;
      context.Response.OutputStream.Close();
      return;
    }

    if (method == "GET" && path == "/api/health")
    {
      LocalServer.WriteJson(context.Response, 200, new
      {
        ok = true,
        mod = "TUFReplay",
        serverVersion = 1
      });
      return;
    }

    if (method == "GET" && path == "/api/records")
    {
      LocalServer.WriteJson(context.Response, 200, PlayRecordRepository.ListSummaries());
      return;
    }

    if (method == "POST" && (path == "/api/replay/stop" || path == "/api/replay/clear"))
    {
      ReplayService.StopActiveReplay("local api " + path);
      LocalServer.WriteJson(context.Response, 200, new { ok = true });
      return;
    }

    if (method == "GET" && TryGetRecordId(path, out string getId))
    {
      PlayRecordMetadata record = PlayRecordRepository.GetMetadata(getId);
      if (record == null)
      {
        LocalServer.WriteJson(context.Response, 404, new { error = "record_not_found" });
        return;
      }

      LocalServer.WriteJson(context.Response, 200, new
      {
        record.Id,
        record.TufLevelId,
        record.ClearedAtUtc,
        record.StartedAtUtc,
        record.EndedAtUtc,
        record.InputCount,
        record.Submitted,
        record.InputCsvBytes,
        record.HasMicRecord,
        record.MicRecordBytes,
        meta = ParseMeta(record.MetaJson)
      });
      return;
    }

    if (method == "DELETE" && TryGetRecordId(path, out string deleteId))
    {
      PlayRecordRepository.Delete(deleteId);
      LocalServer.WriteJson(context.Response, 200, new { ok = true });
      return;
    }

    if (method == "POST" && TryGetRecordAction(path, out string id, out string action))
    {
      if (action == "open")
      {
        OpenRecordResult result = ReplayService.OpenRecord(id);

        if (!result.Ok)
        {
          LocalServer.WriteJson(context.Response, 400, new { error = result.Error });
          return;
        }

        LocalServer.WriteJson(context.Response, 200, new { ok = true, status = result.Status, recordId = id, tufLevelId = result.TufLevelId });
        return;
      }
    }

    LocalServer.WriteJson(context.Response, 404, new { error = "not_found" });
  }

  private static bool TryGetRecordId(string path, out string id)
  {
    id = null;

    const string prefix = "/api/records/";
    if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;

    string rest = path.Substring(prefix.Length);
    if (rest.Length == 0 || rest.Contains("/")) return false;

    id = WebUtility.UrlDecode(rest);
    return true;
  }

  private static bool TryGetRecordAction(string path, out string id, out string action)
  {
    id = null;
    action = null;

    const string prefix = "/api/records/";
    if (!path.StartsWith(prefix, StringComparison.Ordinal)) return false;

    string rest = path.Substring(prefix.Length);
    string[] parts = rest.Split('/');

    if (parts.Length != 2) return false;
    if (string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1])) return false;

    id = WebUtility.UrlDecode(parts[0]);
    action = WebUtility.UrlDecode(parts[1]);
    return true;
  }

  private static object ParseMeta(string metaJson)
  {
    if (string.IsNullOrEmpty(metaJson)) return null;

    try
    {
      return JsonConvert.DeserializeObject(metaJson);
    }
    catch
    {
      return metaJson;
    }
  }

  private static void ApplyCorsHeaders(HttpListenerContext context)
  {
    string origin = context.Request.Headers["Origin"];
    if (IsLoopbackOrigin(origin))
    {
      context.Response.Headers["Access-Control-Allow-Origin"] = origin;
      context.Response.Headers["Vary"] = "Origin";
    }

    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, DELETE, OPTIONS";
    context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
  }

  private static bool IsLoopbackOrigin(string origin)
  {
    if (string.IsNullOrEmpty(origin)) return false;

    try
    {
      Uri uri = new Uri(origin);
      return uri.Scheme == "http" &&
             (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1");
    }
    catch
    {
      return false;
    }
  }
}
