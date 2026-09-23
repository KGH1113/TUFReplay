using System.Collections.Generic;
using Newtonsoft.Json;

namespace TUFReplay.Visual.Contracts;

public sealed class VisualSourceInfo
{
  [JsonProperty("source")]
  public string Source { get; set; }

  [JsonProperty("version")]
  public string Version { get; set; }

  [JsonProperty("available")]
  public bool Available { get; set; }

  [JsonProperty("kinds")]
  public List<string> Kinds { get; set; }
}
