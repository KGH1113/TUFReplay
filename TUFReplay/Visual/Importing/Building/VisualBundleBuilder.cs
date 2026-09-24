using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Assets;

namespace TUFReplay.Visual.Importing.Building;

internal static class VisualBundleBuilder
{
  public static VisualBundle CreateBundle(
    VisualKind kind,
    VisualSource source,
    string sourceVersion,
    VisualViewport viewport,
    IDictionary<string, JToken> files,
    VisualAssetCollector assets
  )
  {
    assets.EnsureComplete();
    var result = new VisualBundle
    {
      SchemaVersion = VisualSchema.Version,
      Kind = VisualKindNames.ToWire(kind),
      Source = VisualSourceDefinitions.Get(source).WireName,
      SourceVersion = sourceVersion,
      Viewport = NormalizeViewport(viewport),
      Files = new Dictionary<string, object>(),
      Assets = new List<VisualAsset>(),
    };
    foreach (KeyValuePair<string, JToken> file in files)
      result.Files.Add(file.Key, file.Value);
    foreach (VisualAsset asset in assets.Assets)
      result.Assets.Add(asset);
    // Binary assets have separate per-file and aggregate limits and are uploaded separately.
    string serialized = JsonConvert.SerializeObject(result.Files, Formatting.None);
    if (Encoding.UTF8.GetByteCount(serialized) > VisualImportLimits.MaxBundleBytes)
      throw new VisualImportException("visual_payload_too_large", "The visual preset exceeds the permitted size.");
    return result;
  }

  public static VisualViewport ViewportFrom(JToken token)
  {
    if (token is JObject obj)
    {
      int? width = Number(obj["width"] ?? obj["Width"] ?? obj["canvasWidth"]);
      int? height = Number(obj["height"] ?? obj["Height"] ?? obj["canvasHeight"]);
      JToken viewport =
        obj["viewport"] ?? obj["Viewport"] ?? obj["Setting"] ?? obj["setting"] ?? obj["settings"] ?? obj["Settings"];
      if (viewport is JObject nested)
      {
        width = width ?? Number(nested["width"] ?? nested["Width"]);
        height = height ?? Number(nested["height"] ?? nested["Height"]);
      }
      if (
        width.HasValue
        && height.HasValue
        && width.Value > 0
        && height.Value > 0
        && width.Value <= 16384
        && height.Value <= 16384
      )
        return new VisualViewport { Width = width.Value, Height = height.Value };
    }
    return new VisualViewport
    {
      Width = VisualSchema.DefaultViewportWidth,
      Height = VisualSchema.DefaultViewportHeight,
    };
  }

  private static VisualViewport NormalizeViewport(VisualViewport viewport)
  {
    if (
      viewport == null
      || viewport.Width <= 0
      || viewport.Height <= 0
      || viewport.Width > 16384
      || viewport.Height > 16384
    )
      return new VisualViewport
      {
        Width = VisualSchema.DefaultViewportWidth,
        Height = VisualSchema.DefaultViewportHeight,
      };
    return viewport;
  }

  private static int? Number(JToken token)
  {
    if (token == null || token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
      return null;
    double value = token.Value<double>();
    return double.IsNaN(value) || double.IsInfinity(value) ? (int?)null : (int)value;
  }
}
