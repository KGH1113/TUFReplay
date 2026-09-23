using System;
using System.Net.Http;
using AdofaiIpc.Core;
using Newtonsoft.Json.Linq;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Submission.Api;

namespace TUFReplay.Submission.Ipc;

public static class SubmissionIpcHandlers
{
  public static object BeginLogin(IpcRequest request)
  {
    try
    {
      var session = FeatureRegistry.Submission.Authentication.Session;
      if (session == null)
        return Error("oauth_not_configured");
      return new { authorizationUrl = session.Begin().GetAwaiter().GetResult() };
    }
    catch (Exception)
    {
      return Error("oauth_login_failed");
    }
  }

  public static object CompleteLogin(IpcRequest request)
  {
    try
    {
      var session = FeatureRegistry.Submission.Authentication.Session;
      if (session == null)
        return Error("oauth_not_configured");
      session
        .Complete(IpcParams.OptionalString(request, "code"), IpcParams.OptionalString(request, "state"))
        .GetAwaiter()
        .GetResult();
      return new { completed = true };
    }
    catch (Exception)
    {
      return Error("oauth_login_failed");
    }
  }

  public static object Disconnect(IpcRequest request)
  {
    try
    {
      FeatureRegistry.Submission.Authentication.Session?.Logout().GetAwaiter().GetResult();
      return new { disconnected = true };
    }
    catch (Exception)
    {
      return Error("oauth_revocation_pending");
    }
  }

  public static object Status(IpcRequest request) => FeatureRegistry.Submission.Status();

  public static object SetDisabled(IpcRequest request)
  {
    if (!IpcParams.TryBool(request, "disabled", out var disabled))
      return Error("invalid_setting");
    FeatureRegistry.Submission.SetDisabled(disabled);
    return FeatureRegistry.Submission.Status();
  }

  public static object List(IpcRequest request)
  {
    string before = IpcParams.OptionalString(request, "before");
    if (before != null && (!long.TryParse(before, out long cursor) || cursor <= 0))
      return Error("invalid_cursor");
    return Send(HttpMethod.Get, "api/v1/runs" + (before == null ? "" : "?before=" + Uri.EscapeDataString(before)));
  }

  public static object Get(IpcRequest request) => RunRequest(request, HttpMethod.Get, "");

  public static object Submit(IpcRequest request) =>
    FeatureRegistry.Submission.CanSubmit
      ? TryPresentation(request, out JObject body, out object error)
        ? RunRequest(request, HttpMethod.Post, "/submit", body)
        : error
      : Error("submission_not_authorized");

  public static object Remove(IpcRequest request) => RunRequest(request, HttpMethod.Delete, "");

  private static object RunRequest(IpcRequest request, HttpMethod method, string suffix)
  {
    return RunRequest(request, method, suffix, null);
  }

  private static object RunRequest(IpcRequest request, HttpMethod method, string suffix, JObject body)
  {
    if (!Guid.TryParse(IpcParams.OptionalString(request, "runId"), out var id))
      return Error("invalid_run_id");
    return Send(method, "api/v1/runs/" + id + suffix, body);
  }

  private static object Send(HttpMethod method, string path, JObject body = null)
  {
    try
    {
      var feature = FeatureRegistry.Submission;
      return feature.Records.Send(feature.Account, method, path, body).GetAwaiter().GetResult();
    }
    catch (SubmissionRequestException exception)
    {
      return Error(exception.Code, exception.Message);
    }
    catch (Exception)
    {
      return Error("submission_request_failed");
    }
  }

  private static bool TryPresentation(IpcRequest request, out JObject body, out object error)
  {
    body = null;
    error = null;
    if (!(request?.Params is JObject parameters) || !parameters.TryGetValue("presentation", out JToken value))
      return true;
    if (value.Type == JTokenType.Null)
    {
      body = new JObject { ["presentation"] = JValue.CreateNull() };
      return true;
    }
    if (!(value is JObject presentation))
    {
      error = Error("visual_bundle_invalid", "The presentation selection is invalid.");
      return false;
    }
    foreach (JProperty property in presentation.Properties())
    {
      if (property.Name != "keyviewer_id" && property.Name != "overlay_id")
      {
        error = Error("visual_bundle_invalid", "The presentation selection is invalid.");
        return false;
      }
      if (property.Value.Type != JTokenType.Null && property.Value.Type != JTokenType.String)
      {
        error = Error("visual_bundle_invalid", "The presentation selection is invalid.");
        return false;
      }
      if (property.Value.Type == JTokenType.String && property.Value.Value<string>().Length > 128)
      {
        error = Error("visual_bundle_invalid", "The presentation selection is invalid.");
        return false;
      }
    }
    body = new JObject { ["presentation"] = presentation.DeepClone() };
    return true;
  }

  private static object Error(string code) =>
    IpcDomainError.Create(code, "Unable to complete the auto submission request.");

  private static object Error(string code, string message) => IpcDomainError.Create(code, message);
}
