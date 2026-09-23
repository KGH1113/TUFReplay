using Newtonsoft.Json;

namespace TUFReplay.Visual.Importing;

public sealed class VisualAssetUpload
{
  [JsonProperty("reference")]
  public string Reference { get; set; }

  [JsonProperty("data_base64")]
  public string DataBase64 { get; set; }
}
