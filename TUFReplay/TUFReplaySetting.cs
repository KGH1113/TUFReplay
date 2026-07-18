using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay;

public sealed class TUFReplaySetting
{
  public const int MinMicrophoneOffsetMs = -500;
  public const int MaxMicrophoneOffsetMs = 500;
  public const int MinMicrophoneVolumeDb = -20;
  public const int MaxMicrophoneVolumeDb = 20;

  public bool AutoRecord { get; set; } = true;
  public string MicrophoneDeviceId { get; set; }
  public int MicrophoneOffsetMs { get; set; }
  public int MicrophoneVolumeDb { get; set; }

  public static TUFReplaySetting Load(string path)
  {
    if (!File.Exists(path))
      return new TUFReplaySetting();

    JObject root = JObject.Parse(File.ReadAllText(path));
    JToken settings = root["Setting"] ?? root;
    TUFReplaySetting result = settings.ToObject<TUFReplaySetting>() ?? new TUFReplaySetting();
    JToken legacyVolumePercent = settings["MicrophoneVolumePercent"];
    if (settings["MicrophoneVolumeDb"] == null && legacyVolumePercent != null)
      result.MicrophoneVolumeDb = LegacyVolumePercentToDb(legacyVolumePercent.Value<double>());
    result.Normalize();
    return result;
  }

  public void Save(string path)
  {
    Normalize();
    File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
  }

  public void Normalize()
  {
    MicrophoneOffsetMs = Math.Max(MinMicrophoneOffsetMs, Math.Min(MaxMicrophoneOffsetMs, MicrophoneOffsetMs));
    MicrophoneVolumeDb = Math.Max(MinMicrophoneVolumeDb, Math.Min(MaxMicrophoneVolumeDb, MicrophoneVolumeDb));
  }

  private static int LegacyVolumePercentToDb(double volumePercent)
  {
    if (volumePercent <= 0d)
      return MinMicrophoneVolumeDb;
    return (int)Math.Round(20d * Math.Log10(volumePercent / 100d));
  }
}
