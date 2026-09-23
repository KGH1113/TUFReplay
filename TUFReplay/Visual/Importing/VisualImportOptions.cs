using System;
using System.Collections.Generic;

namespace TUFReplay.Visual.Importing;

/// <summary>Per-request asset bindings. Never reads a path supplied by the browser.</summary>
public sealed class VisualImportOptions
{
  public bool Inspect { get; set; }
  public Action<VisualImportProgress> Progress { get; set; }
  public IReadOnlyList<VisualAssetUpload> Uploads { get; set; } = Array.Empty<VisualAssetUpload>();
  public List<VisualAssetRequirement> MissingAssets { get; } = new List<VisualAssetRequirement>();

  internal VisualAssetUpload Find(string reference)
  {
    VisualAssetUpload found = null;
    foreach (VisualAssetUpload upload in Uploads)
    {
      if (upload == null || string.IsNullOrWhiteSpace(upload.Reference) || upload.Reference.Length > 1024)
        throw new VisualImportException("visual_bundle_invalid", "An uploaded asset reference is invalid.");
      if (!string.Equals(upload.Reference, reference, StringComparison.Ordinal))
        continue;
      if (found != null)
        throw new VisualImportException("visual_bundle_invalid", "An asset reference has more than one uploaded file.");
      found = upload;
    }
    return found;
  }

  internal void Missing(string reference, string kind)
  {
    foreach (VisualAssetRequirement item in MissingAssets)
      if (item.Reference == reference)
        return;
    MissingAssets.Add(new VisualAssetRequirement { Reference = reference, Kind = kind });
  }

  internal void EnsureComplete()
  {
    if (!Inspect && MissingAssets.Count > 0)
      throw new VisualImportException(
        "visual_asset_missing",
        "Upload the missing images or fonts before registering this preset."
      );
  }
}
