using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay;

public sealed class TUFReplaySetting
{
  public bool AutoRecord { get; set; } = true;

  public static TUFReplaySetting Load(string path)
  {
    if (!File.Exists(path))
      return new TUFReplaySetting();

    JObject root = JObject.Parse(File.ReadAllText(path));
    JToken settings = root["Setting"] ?? root;
    return settings.ToObject<TUFReplaySetting>() ?? new TUFReplaySetting();
  }

  public void Save(string path)
  {
    File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
  }
}
