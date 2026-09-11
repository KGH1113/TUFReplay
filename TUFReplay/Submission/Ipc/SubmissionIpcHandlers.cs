using System;
using System.Net.Http;
using AdofaiIpc.Core;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Submission.Ipc;

public static class SubmissionIpcHandlers
{
  public static object BeginLogin(IpcRequest request)
  {
    try
    {
      var session = FeatureRegistry.Submission.Authentication.Session;
      if (session == null) return Error("oauth_not_configured");
      return new { authorizationUrl = session.Begin().GetAwaiter().GetResult() };
    }
    catch (Exception) { return Error("oauth_login_failed"); }
  }

  public static object CompleteLogin(IpcRequest request)
  {
    try
    {
      var session = FeatureRegistry.Submission.Authentication.Session;
      if (session == null) return Error("oauth_not_configured");
      session.Complete(IpcParams.OptionalString(request, "code"), IpcParams.OptionalString(request, "state"))
        .GetAwaiter().GetResult();
      return new { completed = true };
    }
    catch (Exception) { return Error("oauth_login_failed"); }
  }

  public static object Disconnect(IpcRequest request)
  {
    try
    {
      FeatureRegistry.Submission.Authentication.Session?.Logout().GetAwaiter().GetResult();
      return new { disconnected = true };
    }
    catch (Exception) { return Error("oauth_revocation_pending"); }
  }

  public static object Status(IpcRequest request) => FeatureRegistry.Submission.Status();

  public static object SetDisabled(IpcRequest request)
  {
    if (!IpcParams.TryBool(request, "disabled", out var disabled)) return Error("invalid_setting");
    FeatureRegistry.Submission.SetDisabled(disabled);
    return FeatureRegistry.Submission.Status();
  }

  public static object List(IpcRequest request)
  {
    string before = IpcParams.OptionalString(request, "before");
    if (before != null && (!long.TryParse(before, out long cursor) || cursor <= 0)) return Error("invalid_cursor");
    return Send(HttpMethod.Get, "api/v1/runs" + (before == null ? "" : "?before=" + Uri.EscapeDataString(before)));
  }
  public static object Get(IpcRequest request) => RunRequest(request, HttpMethod.Get, "");
  public static object Submit(IpcRequest request) => RunRequest(request, HttpMethod.Post, "/submit");
  public static object Remove(IpcRequest request) => RunRequest(request, HttpMethod.Delete, "");

  private static object RunRequest(IpcRequest request, HttpMethod method, string suffix)
  {
    if (!Guid.TryParse(IpcParams.OptionalString(request, "runId"), out var id)) return Error("invalid_run_id");
    return Send(method, "api/v1/runs/" + id + suffix);
  }
  private static object Send(HttpMethod method, string path)
  {
    try
    {
      var feature = FeatureRegistry.Submission;
      return feature.Records.Send(feature.Account, method, path).GetAwaiter().GetResult();
    }
    catch (Exception) { return Error("submission_request_failed"); }
  }
  private static object Error(string code) => IpcDomainError.Create(code, "Unable to complete the auto submission request.");
}
