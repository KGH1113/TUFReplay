using Newtonsoft.Json;

namespace TUFReplay.Visual.Contracts;

public sealed class VisualPreset
{
  [JsonProperty("id")]
  public string Id { get; set; }

  [JsonProperty("name")]
  public string Name { get; set; }

  [JsonProperty("kind")]
  public string Kind { get; set; }

  [JsonProperty("source")]
  public string Source { get; set; }

  [JsonProperty("source_version")]
  public string SourceVersion { get; set; }

  [JsonProperty("created_at")]
  public string CreatedAt { get; set; }
}

public sealed class VisualSelection
{
  [JsonProperty("keyviewer_id")]
  public string KeyviewerId { get; set; }

  [JsonProperty("overlay_id")]
  public string OverlayId { get; set; }
}
