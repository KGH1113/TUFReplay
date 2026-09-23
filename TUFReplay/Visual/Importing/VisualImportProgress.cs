using Newtonsoft.Json;

namespace TUFReplay.Visual.Importing;

public sealed class VisualImportProgress
{
  [JsonProperty("stage")]
  public string Stage { get; }

  [JsonProperty("asset_name")]
  public string AssetName { get; }

  [JsonProperty("completed_assets")]
  public int CompletedAssets { get; }

  public VisualImportProgress(string stage, string assetName = null, int completedAssets = 0)
  {
    Stage = stage;
    AssetName = assetName;
    CompletedAssets = completedAssets;
  }
}
