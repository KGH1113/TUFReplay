using System;
using AdofaiIpc.Core;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Ipc;

public static class VisualIpcHandlers
{
  public static object List(IpcRequest request) =>
    Execute(() => FeatureRegistry.Visuals?.ListAsync().GetAwaiter().GetResult());

  public static object Remove(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return Error("visual_preset_not_found", "The visual preset was not found.");
    return Execute(() => FeatureRegistry.Visuals?.RemoveAsync(id).GetAwaiter().GetResult());
  }

  public static object Sources(IpcRequest request)
  {
    if (FeatureRegistry.Visuals == null)
      return Error("visual_source_unsupported", "Visual preset import is unavailable in this build.");
    return FeatureRegistry.Visuals.Sources();
  }

  public static object Import(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "name", out string name))
      return Error("visual_name_required", "Enter a name for the visual preset.");
    if (!VisualIpcRequestParser.TryKind(request, out VisualKind kind))
      return Error("visual_bundle_invalid", "The visual preset kind is invalid.");
    if (!VisualIpcRequestParser.TrySource(request, out VisualSource source))
      return Error("visual_source_unsupported", "The visual preset source is unsupported.");
    string presetJson = IpcParams.OptionalString(request, "presetJson");
    return Execute(() =>
      FeatureRegistry
        .Visuals?.ImportAsync(name, kind, source, presetJson, uploads: VisualIpcRequestParser.ReadUploads(request))
        .GetAwaiter()
        .GetResult()
    );
  }

  public static object Inspect(IpcRequest request)
  {
    if (
      !VisualIpcRequestParser.TryKind(request, out VisualKind kind)
      || !VisualIpcRequestParser.TrySource(request, out VisualSource source)
    )
      return Error("visual_source_unsupported", "Select a supported visual source.");
    return Execute(() =>
      FeatureRegistry.Visuals?.Inspect(
        kind,
        source,
        IpcParams.OptionalString(request, "presetJson"),
        VisualIpcRequestParser.ReadUploads(request)
      )
    );
  }

  public static object StartRegistration(IpcRequest request)
  {
    if (
      !IpcParams.TryRequiredString(request, "name", out string name)
      || !VisualIpcRequestParser.TryKind(request, out VisualKind kind)
      || !VisualIpcRequestParser.TrySource(request, out VisualSource source)
    )
      return Error("visual_bundle_invalid", "Select a visual source and enter a preset name.");
    return Execute(() =>
      FeatureRegistry.Visuals?.StartRegistration(
        IpcParams.OptionalString(request, "operationId"),
        name,
        kind,
        source,
        IpcParams.OptionalString(request, "presetJson"),
        VisualIpcRequestParser.ReadUploads(request)
      )
    );
  }

  public static object RegistrationStatus(IpcRequest request) =>
    Execute(() => FeatureRegistry.Visuals?.RegistrationStatus(IpcParams.OptionalString(request, "operationId")));

  private static object Execute(Func<object> operation)
  {
    try
    {
      object result = operation();
      return result ?? Error("visual_source_unsupported", "Visual preset import is unavailable in this build.");
    }
    catch (VisualImportException exception)
    {
      return Error(exception.Code, exception.Message);
    }
    catch (SubmissionRequestException exception)
    {
      return Error(exception.Code, exception.Message);
    }
    catch (Exception)
    {
      return Error("visual_request_failed", "Unable to complete the visual preset request.");
    }
  }

  private static object Error(string code, string message) => IpcDomainError.Create(code, message);
}
