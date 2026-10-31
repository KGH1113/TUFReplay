using System;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Replay.Export;

// OrbitRender is an independent mod. TUFReplay binds to it through reflection
// and JSON only: no assembly reference, no shared types, no exceptions across
// the boundary. Every call must run on Unity's main thread.
public static class OrbitRenderClient
{
  public const int RequiredApiVersion = 2;
  private const string ApiTypeName = "OrbitRender.RenderApi";

  private static readonly object Gate = new object();
  private static Type _apiType;
  private static MethodInfo _startReplay;
  private static MethodInfo _readStatus;
  private static MethodInfo _cancel;
  private static PropertyInfo _isAvailable;
  private static long _nextResolve;
  private static bool _searched;

  public static OrbitRenderResult StartReplay(string manifestPath, string optionsJson)
  {
    var error = Resolve();
    return error != null ? OrbitRenderResult.Failure(error) : Invoke(_startReplay, manifestPath, optionsJson ?? "{}");
  }

  public static OrbitRenderResult ReadStatus(string jobId)
  {
    var error = Resolve();
    return error != null ? OrbitRenderResult.Failure(error) : Invoke(_readStatus, jobId);
  }

  public static OrbitRenderResult Cancel(string jobId)
  {
    var error = Resolve();
    return error != null ? OrbitRenderResult.Failure(error) : Invoke(_cancel, jobId);
  }

  private static OrbitRenderError Resolve()
  {
    lock (Gate)
    {
      if (_apiType == null)
      {
        long now = Stopwatch.GetTimestamp();
        if (_searched && now < _nextResolve)
          return Missing();
        _apiType = FindApiType();
        _searched = true;
        if (_apiType == null)
        {
          _nextResolve = now + Stopwatch.Frequency;
          return Missing();
        }
        Bind(_apiType);
      }
      if (ReadApiVersion(_apiType) < RequiredApiVersion)
        return new OrbitRenderError
        {
          Code = "renderer_version_unsupported",
          Message = "OrbitRender 2.7.0 or later is required. Update it in UnityModManager and restart ADOFAI.",
        };
      if (_isAvailable != null && !(bool)(_isAvailable.GetValue(null) ?? false))
        return new OrbitRenderError
        {
          Code = "renderer_unavailable",
          Message = "OrbitRender is disabled. Enable it in UnityModManager and try again.",
        };
      return null;
    }
  }

  private static Type FindApiType()
  {
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      Type type = null;
      try
      {
        type = assembly.GetType(ApiTypeName, false);
      }
      catch (Exception)
      {
        // A half-loaded or dynamic assembly must not break render startup.
      }
      if (type != null)
        return type;
    }
    return null;
  }

  private static void Bind(Type type)
  {
    const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.Static;
    _startReplay = type.GetMethod("StartReplay", staticFlags, null, new[] { typeof(string), typeof(string) }, null);
    _readStatus = type.GetMethod("ReadStatus", staticFlags, null, new[] { typeof(string) }, null);
    _cancel = type.GetMethod("Cancel", staticFlags, null, new[] { typeof(string) }, null);
    _isAvailable = type.GetProperty("IsAvailable", staticFlags);
    if (_startReplay == null || _readStatus == null || _cancel == null)
      _apiType = null;
  }

  private static int ReadApiVersion(Type type)
  {
    const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.Static;
    var field = type.GetField("ApiVersion", staticFlags);
    if (field != null && field.GetValue(null) is int fieldVersion)
      return fieldVersion;
    var property = type.GetProperty("ApiVersion", staticFlags);
    return property?.GetValue(null) is int propertyVersion ? propertyVersion : 0;
  }

  private static OrbitRenderResult Invoke(MethodInfo method, params object[] arguments)
  {
    if (method == null)
      return OrbitRenderResult.Failure(Missing());
    try
    {
      return Parse(method.Invoke(null, arguments) as string);
    }
    catch (TargetInvocationException exception)
    {
      var inner = exception.InnerException ?? exception;
      return OrbitRenderResult.Failure(
        new OrbitRenderError
        {
          Code = "render_internal_error",
          Message = string.IsNullOrEmpty(inner.Message) ? "OrbitRender could not complete the request." : inner.Message,
        }
      );
    }
    catch (Exception exception)
    {
      ResetBinding();
      return OrbitRenderResult.Failure(
        new OrbitRenderError
        {
          Code = "renderer_unavailable",
          Message = "OrbitRender could not be reached: " + exception.Message,
        }
      );
    }
  }

  private static void ResetBinding()
  {
    lock (Gate)
    {
      _apiType = null;
      _startReplay = null;
      _readStatus = null;
      _cancel = null;
      _isAvailable = null;
      _searched = false;
    }
  }

  private static OrbitRenderError Missing() =>
    new OrbitRenderError
    {
      Code = "renderer_missing",
      Message = "Install and enable OrbitRender, then restart ADOFAI.",
    };

  private static OrbitRenderResult Parse(string json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return OrbitRenderResult.Failure(
        new OrbitRenderError { Code = "renderer_invalid_response", Message = "OrbitRender returned an empty response." }
      );
    JObject root;
    try
    {
      root = JObject.Parse(json);
    }
    catch (JsonException exception)
    {
      return OrbitRenderResult.Failure(
        new OrbitRenderError
        {
          Code = "renderer_invalid_response",
          Message = "OrbitRender returned an unreadable response: " + exception.Message,
        }
      );
    }
    if (root.Value<bool?>("ok") != true)
    {
      var error = root["error"] as JObject;
      return OrbitRenderResult.Failure(
        new OrbitRenderError
        {
          Code = error?.Value<string>("code") ?? "render_failed",
          Message = error?.Value<string>("message") ?? "OrbitRender could not complete the request.",
          Field = error?.Value<string>("field"),
          Line = error?.Value<int?>("line"),
          File = error?.Value<string>("file"),
        }
      );
    }
    if (!(root["job"] is JObject job))
      return OrbitRenderResult.Failure(
        new OrbitRenderError { Code = "renderer_invalid_response", Message = "OrbitRender returned no render job." }
      );
    var details = job["errorDetails"] as JObject;
    return OrbitRenderResult.Success(
      new OrbitRenderJob
      {
        Id = job.Value<string>("id") ?? string.Empty,
        State = (job.Value<string>("state") ?? string.Empty).ToLowerInvariant(),
        Progress = job.Value<double?>("progress") ?? 0,
        CapturedFrames = job.Value<long?>("capturedFrames") ?? 0,
        TotalFrames = job.Value<long?>("totalFrames") ?? 0,
        OutputPath = job.Value<string>("outputPath"),
        ErrorCode = job.Value<string>("errorCode"),
        ErrorMessage = job.Value<string>("errorMessage"),
        WaitingForGameFocus = job.Value<bool?>("waitingForGameFocus") ?? false,
        Warnings = job["warnings"]?.ToObject<string[]>() ?? Array.Empty<string>(),
        ErrorDetails =
          details == null
            ? null
            : new OrbitRenderError
            {
              Code = details.Value<string>("code"),
              Message = details.Value<string>("message"),
              Field = details.Value<string>("field"),
              Line = details.Value<int?>("line"),
              File = details.Value<string>("file"),
            },
      }
    );
  }
}

public sealed class OrbitRenderResult
{
  public bool Ok { get; set; }
  public OrbitRenderJob Job { get; set; }
  public OrbitRenderError Error { get; set; }

  public static OrbitRenderResult Success(OrbitRenderJob job) => new OrbitRenderResult { Ok = true, Job = job };

  public static OrbitRenderResult Failure(OrbitRenderError error) =>
    new OrbitRenderResult { Ok = false, Error = error };

  public string ErrorCode => Error?.Code ?? "render_failed";

  public string ErrorMessage => Error?.Message ?? "OrbitRender could not complete the request.";
}

public sealed class OrbitRenderJob
{
  public string Id { get; set; } = string.Empty;
  public string State { get; set; } = string.Empty;
  public double Progress { get; set; }
  public long CapturedFrames { get; set; }
  public long TotalFrames { get; set; }
  public string OutputPath { get; set; }
  public string ErrorCode { get; set; }
  public string ErrorMessage { get; set; }
  public OrbitRenderError ErrorDetails { get; set; }
  public bool WaitingForGameFocus { get; set; }
  public string[] Warnings { get; set; } = Array.Empty<string>();
}

public sealed class OrbitRenderError
{
  public string Code { get; set; }
  public string Message { get; set; }
  public string Field { get; set; }
  public int? Line { get; set; }
  public string File { get; set; }
}
