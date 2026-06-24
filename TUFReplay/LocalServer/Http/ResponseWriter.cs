using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace TUFReplay.LocalServer.Http;

public static class ResponseWriter
{
  public static void Write(HttpListenerContext context, object result)
  {
    ApplyCorsHeaders(context);

    if (context.Request.HttpMethod == "OPTIONS")
    {
      WriteNoContent(context.Response);
      return;
    }

    ServerResponse response = Normalize(result);
    WriteResponse(context.Response, response);
  }

  public static void WriteError(HttpListenerContext context, Exception error)
  {
    ApplyCorsHeaders(context);

    WriteResponse(
      context.Response, 
      ServerResponse.InternalServerError(new { error = "internal_error" })
    );
  }

  private static ServerResponse Normalize(object result)
  {
    if (result == null) return ServerResponse.NoContent();
    if (result is ServerResponse response) return response;
    return ServerResponse.Ok(result);
  }

  private static void WriteResponse(HttpListenerResponse response, ServerResponse result)
  {
    response.StatusCode = result.StatusCode;

    if (result.Body == null)
    {
      response.OutputStream.Close();
      return;
    }

    string json = JsonConvert.SerializeObject(result.Body);
    byte[] bytes = Encoding.UTF8.GetBytes(json);

    response.ContentType = result.ContentType ?? "application/json; charset=utf-8";
    response.ContentLength64 = bytes.Length;
    response.OutputStream.Write(bytes, 0, bytes.Length);
    response.OutputStream.Close();
  }

  private static void WriteNoContent(HttpListenerResponse response)
  {
    response.StatusCode = 204;
    response.OutputStream.Close();
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
      return uri.Scheme == "http" && (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1");
    }
    catch
    {
      return false;
    }
  }
}
