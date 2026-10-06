using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Webcam.Models;

namespace TUFReplay;

public sealed class TUFReplaySetting
{
  public const int CurrentMicrophoneOffsetConventionVersion = 1;
  public const int MinMicrophoneOffsetMs = -500;
  public const int MaxMicrophoneOffsetMs = 500;
  public const int MinMicrophoneVolumeDb = -20;
  public const int MaxMicrophoneVolumeDb = 30;

  public bool AutoRecord { get; set; } = true;
  public bool MicrophoneEnabled { get; set; } = true;
  public string MicrophoneDeviceId { get; set; }
  public int MicrophoneOffsetMs { get; set; }
  public int MicrophoneOffsetConventionVersion { get; set; } = CurrentMicrophoneOffsetConventionVersion;
  public int MicrophoneVolumeDb { get; set; }

  public bool WebcamEnabled { get; set; }
  public string WebcamDeviceId { get; set; }
  public string WebcamQuality { get; set; } = "compact";
  public int WebcamOffsetMs { get; set; }
  public int WebcamStorageLimitMb { get; set; } = 512;
  public int WebcamRetentionDays { get; set; } = 7;
  public bool WebcamPlaybackVisible { get; set; } = true;
  public bool WebcamLiveVisible { get; set; }
  public bool WebcamMirror { get; set; }
  public double WebcamOverlayX { get; set; } = 1d;
  public double WebcamOverlayY { get; set; } = 1d;
  public double WebcamOverlayWidth { get; set; } = 0.22d;
  public double? WebcamOverlayLeft { get; set; }
  public double? WebcamOverlayTop { get; set; }
  public double WebcamCropX { get; set; }
  public double WebcamCropY { get; set; }
  public double WebcamCropWidth { get; set; } = 1d;
  public double WebcamCropHeight { get; set; } = 1d;

  public static TUFReplaySetting Load(string path)
  {
    if (!File.Exists(path))
      return new TUFReplaySetting();

    JObject root = JObject.Parse(File.ReadAllText(path));
    JToken settings = root["Setting"] ?? root;
    TUFReplaySetting result = settings.ToObject<TUFReplaySetting>() ?? new TUFReplaySetting();
    int offsetConventionVersion = (int?)settings["MicrophoneOffsetConventionVersion"] ?? 0;
    if (offsetConventionVersion < CurrentMicrophoneOffsetConventionVersion)
    {
      result.MicrophoneOffsetMs = -result.MicrophoneOffsetMs;
      result.MicrophoneOffsetConventionVersion = CurrentMicrophoneOffsetConventionVersion;
    }
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
    WebcamOffsetMs = Math.Max(-1000, Math.Min(1000, WebcamOffsetMs));
    WebcamStorageLimitMb = Math.Max(64, Math.Min(8192, WebcamStorageLimitMb));
    WebcamRetentionDays = Math.Max(1, Math.Min(30, WebcamRetentionDays));
    WebcamQuality = WebcamQuality == "balanced" || WebcamQuality == "quality" ? WebcamQuality : "compact";
    WebcamOverlayX = NormalizeFraction(WebcamOverlayX, 1d, -1d, 2d);
    WebcamOverlayY = NormalizeFraction(WebcamOverlayY, 1d, -1d, 2d);
    WebcamOverlayWidth = NormalizeFraction(WebcamOverlayWidth, 0.22d, 0.1d, 0.5d);
    WebcamOverlayLeft = NormalizePosition(WebcamOverlayLeft);
    WebcamOverlayTop = NormalizePosition(WebcamOverlayTop);
    WebcamCropRect.Get(this).ApplyTo(this);
  }

  private static double? NormalizePosition(double? value) =>
    value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)) ? null : value;

  private static double NormalizeFraction(double value, double fallback, double min, double max) =>
    double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));

  private static int LegacyVolumePercentToDb(double volumePercent)
  {
    if (volumePercent <= 0d)
      return MinMicrophoneVolumeDb;
    return (int)Math.Round(20d * Math.Log10(volumePercent / 100d));
  }
}
