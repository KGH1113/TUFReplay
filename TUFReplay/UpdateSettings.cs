using System.IO;
using Newtonsoft.Json;

namespace TUFReplay;

public sealed class UpdateSettings
{
  public bool ReceiveBetaUpdates { get; set; }

  public static UpdateSettings Load(string path)
  {
    try
    {
      if (!File.Exists(path))
        return new UpdateSettings();

      return JsonConvert.DeserializeObject<UpdateSettings>(File.ReadAllText(path)) ??
             new UpdateSettings();
    }
    catch
    {
      return new UpdateSettings();
    }
  }

  public void Save(string path)
  {
    string temporaryPath = path + ".tmp";
    File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(this, Formatting.Indented));

    if (File.Exists(path))
      File.Delete(path);
    File.Move(temporaryPath, path);
  }
}
