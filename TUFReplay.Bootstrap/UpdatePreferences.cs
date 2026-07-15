using System.IO;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Bootstrap;

internal sealed class UpdatePreferences
{
  public bool ReceiveBetaUpdates { get; private set; }

  public static UpdatePreferences Load(string path)
  {
    try
    {
      if (!File.Exists(path))
        return new UpdatePreferences();

      JObject root = JObject.Parse(File.ReadAllText(path));
      return new UpdatePreferences
      {
        ReceiveBetaUpdates = root.Value<bool?>(nameof(ReceiveBetaUpdates)) ?? false
      };
    }
    catch
    {
      return new UpdatePreferences();
    }
  }
}
