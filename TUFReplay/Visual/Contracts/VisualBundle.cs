using System.Collections.Generic;
using Newtonsoft.Json;

namespace TUFReplay.Visual.Contracts;

public sealed class VisualViewport
{
  [JsonProperty("width")]
  public int Width { get; set; }

  [JsonProperty("height")]
  public int Height { get; set; }
}

public sealed class VisualAsset
{
  [JsonProperty("path")]
  public string Path { get; set; }

  [JsonProperty("media_type")]
  public string MediaType { get; set; }

  [JsonProperty("data_base64")]
  public string DataBase64 { get; set; }
}

public sealed class VisualBundle
{
  [JsonProperty("schema_version")]
  public int SchemaVersion { get; set; }

  [JsonProperty("kind")]
  public string Kind { get; set; }

  [JsonProperty("source")]
  public string Source { get; set; }

  [JsonProperty("source_version")]
  public string SourceVersion { get; set; }

  [JsonProperty("viewport")]
  public VisualViewport Viewport { get; set; }

  [JsonProperty("files")]
  public Dictionary<string, object> Files { get; set; }

  [JsonProperty("assets")]
  public List<VisualAsset> Assets { get; set; }
}
