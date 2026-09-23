using System;
using System.Collections.Generic;
using AdofaiIpc.Core;
using Newtonsoft.Json;
using TUFReplay.Shared.Ipc;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Ipc;

internal static class VisualIpcRequestParser
{
  public static bool TryKind(IpcRequest request, out VisualKind kind) =>
    VisualKindNames.TryParse(IpcParams.OptionalString(request, "kind"), out kind);

  public static bool TrySource(IpcRequest request, out VisualSource source) =>
    VisualSourceDefinitions.TryParse(IpcParams.OptionalString(request, "source"), out source);

  public static IReadOnlyList<VisualAssetUpload> ReadUploads(IpcRequest request)
  {
    string json = IpcParams.OptionalString(request, "assetsJson");
    if (string.IsNullOrWhiteSpace(json))
      return Array.Empty<VisualAssetUpload>();
    if (json.Length > VisualImportLimits.MaxRequestBytes)
      throw new VisualImportException("visual_payload_too_large", "The uploaded assets are too large.");
    try
    {
      var uploads = JsonConvert.DeserializeObject<List<VisualAssetUpload>>(json);
      if (uploads == null || uploads.Count > VisualImportLimits.MaxAssets)
        throw new VisualImportException("visual_payload_too_large", "Too many uploaded assets.");
      return uploads;
    }
    catch (JsonException exception)
    {
      throw new VisualImportException("visual_bundle_invalid", "The uploaded assets are invalid.", exception);
    }
  }
}
