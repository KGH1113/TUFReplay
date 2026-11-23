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
  // RenderApi 3 (OrbitRender 2.8.0) renders with TUFReplay as the replay host: TUFReplay replays
  // the recording and OrbitRender only captures and encodes it. Older versions replay recordings
  // themselves through ADOFAI's input and stop on keys it cannot take, such as the ' key.
  public const int RequiredApiVersion = 3;
  public const string MinimumVersion = "2.8.0";
  private const string ApiTypeName = "OrbitRender.RenderApi";

  private static readonly object Gate = new object();
  private static Type _apiType;
  private static int _apiVersion;
  private static MethodInfo _startHostedReplay;
  private static MethodInfo _readStatus;
  private static MethodInfo _cancel;
  private static PropertyInfo _isAvailable;
  private static long _nextResolve;
  private static bool _searched;

  /// <summary>Why the installed OrbitRender cannot make a video now, or null when it can.</summary>
  public static OrbitRenderError CheckReady() => Resolve();

  /// <summary>
  /// True when the installed OrbitRender asks the replay host for overlay videos after the last
  /// frame and lays them over the finished video (<c>OverlayVideoVersion</c> 1).
  /// </summary>
  public static bool SupportsOverlayVideos => Resolve() == null && TakesOverlayVideos(_apiType);

  private static bool TakesOverlayVideos(Type type)
  {
    var capability = type?.GetField("OverlayVideoVersion", BindingFlags.Public | BindingFlags.Static);
    return capability?.GetValue(null) is int version && version >= 1;
  }

  /// <summary>
  /// True when the installed OrbitRender takes the bitrate in Kbps (<c>BitrateKbpsVersion</c> 1).
  /// Older versions take whole Mbps only.
  /// </summary>
  public static bool SupportsBitrateKbps => Resolve() == null && TakesBitrateKbps(_apiType);

  private static bool TakesBitrateKbps(Type type)
  {
    var capability = type?.GetField("BitrateKbpsVersion", BindingFlags.Public | BindingFlags.Static);
    return capability?.GetValue(null) is int version && version >= 1;
  }

  /// <summary>Starts a render that OrbitRender captures while <paramref name="host"/> replays the recording.</summary>
  public static OrbitRenderResult StartHostedReplay(string requestJson, object host)
  {
    var error = Resolve();
    return error != null ? OrbitRenderResult.Failure(error) : Invoke(_startHostedReplay, requestJson, host);
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
      // A loaded OrbitRender without the calls TUFReplay needs is an older version, not a missing one.
      if (_apiVersion < RequiredApiVersion || _startHostedReplay == null || _readStatus == null || _cancel == null)
        return new OrbitRenderError
        {
          Code = "renderer_version_unsupported",
          Message =
            "Videos can be made with OrbitRender "
            + MinimumVersion
            + " or later. Update OrbitRender, then restart ADOFAI.",
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
    _apiVersion = ReadApiVersion(type);
    _startHostedReplay = type.GetMethod(
      "StartHostedReplay",
      staticFlags,
      null,
      new[] { typeof(string), typeof(object) },
      null
    );
    _readStatus = type.GetMethod("ReadStatus", staticFlags, null, new[] { typeof(string) }, null);
    _cancel = type.GetMethod("Cancel", staticFlags, null, new[] { typeof(string) }, null);
    _isAvailable = type.GetProperty("IsAvailable", staticFlags);
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
      _apiVersion = 0;
      _startHostedReplay = null;
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

public sealed class OrbitRenderOptions
{
  [JsonProperty("noPreview")]
  public bool? NoPreview { get; set; }

  [JsonProperty("outputDirectory")]
  public string OutputDirectory { get; set; }

  [JsonProperty("targetFps")]
  public int? TargetFps { get; set; }

  [JsonProperty("videoFps")]
  public int? VideoFps { get; set; }

  [JsonProperty("width")]
  public int? Width { get; set; }

  [JsonProperty("height")]
  public int? Height { get; set; }

  [JsonProperty("bitrateMbps")]
  public int? BitrateMbps { get; set; }

  /// <summary>Sent instead of <see cref="BitrateMbps"/> to OrbitRender with <c>BitrateKbpsVersion</c>.</summary>
  [JsonProperty("bitrateKbps")]
  public int? BitrateKbps { get; set; }

  [JsonProperty("videoCodec")]
  public string VideoCodec { get; set; }

  [JsonProperty("bitDepth")]
  public int? BitDepth { get; set; }

  /// <summary>OrbitRender's ProRes profile; OrbitRender before profiles ignores it and uses HQ.</summary>
  [JsonProperty("proResProfile")]
  public string ProResProfile { get; set; }

  [JsonProperty("captureAudio")]
  public bool? CaptureAudio { get; set; }

  [JsonProperty("audioGainDb")]
  public double? AudioGainDb { get; set; }

  [JsonProperty("endDelaySeconds")]
  public double? EndDelaySeconds { get; set; }

  [JsonProperty("bgaMode")]
  public bool? BgaMode { get; set; }

  [JsonProperty("showPlanetRings")]
  public bool? ShowPlanetRings { get; set; }

  [JsonProperty("showSongTitle")]
  public bool? ShowSongTitle { get; set; }

  [JsonProperty("showCountdown")]
  public bool? ShowCountdown { get; set; }

  [JsonProperty("showResultText")]
  public bool? ShowResultText { get; set; }

  [JsonProperty("showHitJudgments")]
  public bool? ShowHitJudgments { get; set; }

  [JsonProperty("showModOverlay")]
  public bool? ShowModOverlay { get; set; }
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
