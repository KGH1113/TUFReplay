using Newtonsoft.Json;

namespace TUFReplay.Visual.Importing;

public sealed class VisualAssetRequirement
{
  [JsonProperty("reference")]
  public string Reference { get; set; }

  [JsonProperty("kind")]
  public string Kind { get; set; }
}
