using System;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Webcam.Models;

public sealed class WebcamSettingsPatch
{
  private readonly JObject _values;

  private WebcamSettingsPatch(JObject values)
  {
    _values = values;
  }

  public bool ChangesCapture =>
    _values["enabled"] != null
    || _values.ContainsKey("deviceId")
    || _values["quality"] != null
    || _values["storageLimitMb"] != null
    || _values["retentionDays"] != null;
  public bool RearmsCapture =>
    _values["enabled"] != null || _values.ContainsKey("deviceId") || _values["quality"] != null;

  public static bool TryParse(JObject values, out WebcamSettingsPatch patch)
  {
    patch = null;
    if (values == null || !values.HasValues)
      return false;
    foreach (JProperty property in values.Properties())
    {
      JToken token = property.Value;
      switch (property.Name)
      {
        case "enabled":
        case "playbackVisible":
        case "liveVisible":
        case "mirror":
          if (token.Type != JTokenType.Boolean)
            return false;
          break;
        case "deviceId":
          if (token.Type != JTokenType.Null && (token.Type != JTokenType.String || token.Value<string>().Length > 4096))
            return false;
          break;
        case "quality":
          if (
            token.Type != JTokenType.String
            || (
              token.Value<string>() != "compact"
              && token.Value<string>() != "balanced"
              && token.Value<string>() != "quality"
            )
          )
            return false;
          break;
        case "offsetMs":
          if (!ValidInteger(token, -1000, 1000))
            return false;
          break;
        case "storageLimitMb":
          if (!ValidInteger(token, 64, 8192))
            return false;
          break;
        case "retentionDays":
          if (!ValidInteger(token, 1, 30))
            return false;
          break;
        case "crop":
          if (!ValidCrop(token))
            return false;
          break;
        case "overlayX":
        case "overlayY":
        case "overlayWidth":
          if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
            return false;
          double number = token.Value<double>();
          double min = property.Name == "overlayWidth" ? 0.1 : -1;
          double max = property.Name == "overlayWidth" ? 0.5 : 2;
          if (double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max)
            return false;
          break;
        default:
          return false;
      }
    }
    patch = new WebcamSettingsPatch((JObject)values.DeepClone());
    return true;
  }

  public void ApplyTo(TUFReplaySetting settings)
  {
    if (_values["enabled"] != null)
      settings.WebcamEnabled = _values["enabled"].Value<bool>();
    if (_values.ContainsKey("deviceId"))
      settings.WebcamDeviceId = NullableText(_values["deviceId"]);
    if (_values["quality"] != null)
      settings.WebcamQuality = _values["quality"].Value<string>();
    if (_values["offsetMs"] != null)
      settings.WebcamOffsetMs = _values["offsetMs"].Value<int>();
    if (_values["storageLimitMb"] != null)
      settings.WebcamStorageLimitMb = _values["storageLimitMb"].Value<int>();
    if (_values["retentionDays"] != null)
      settings.WebcamRetentionDays = _values["retentionDays"].Value<int>();
    if (_values["playbackVisible"] != null)
      settings.WebcamPlaybackVisible = _values["playbackVisible"].Value<bool>();
    if (_values["liveVisible"] != null)
      settings.WebcamLiveVisible = _values["liveVisible"].Value<bool>();
    if (_values["mirror"] != null)
      settings.WebcamMirror = _values["mirror"].Value<bool>();
    if (_values["overlayX"] != null)
      settings.WebcamOverlayX = _values["overlayX"].Value<double>();
    if (_values["overlayY"] != null)
      settings.WebcamOverlayY = _values["overlayY"].Value<double>();
    if (_values["overlayX"] != null || _values["overlayY"] != null)
    {
      settings.WebcamOverlayLeft = null;
      settings.WebcamOverlayTop = null;
    }
    if (_values["overlayWidth"] != null)
      settings.WebcamOverlayWidth = _values["overlayWidth"].Value<double>();
    if (_values["crop"] is JObject crop)
      WebcamCropRect
        .Clamp(
          crop["x"].Value<double>(),
          crop["y"].Value<double>(),
          crop["width"].Value<double>(),
          crop["height"].Value<double>()
        )
        .ApplyTo(settings);
  }

  private static string NullableText(JToken token) =>
    token.Type == JTokenType.Null || string.IsNullOrWhiteSpace(token.Value<string>())
      ? null
      : token.Value<string>().Trim();

  private static bool ValidInteger(JToken token, int min, int max) =>
    token.Type == JTokenType.Integer && token.Value<long>() >= min && token.Value<long>() <= max;

  private static bool ValidCrop(JToken token)
  {
    if (!(token is JObject crop) || crop.Count != 4)
      return false;
    if (
      !ValidNumber(crop["x"], 0, 1)
      || !ValidNumber(crop["y"], 0, 1)
      || !ValidNumber(crop["width"], WebcamCropRect.MinimumSize, 1)
      || !ValidNumber(crop["height"], WebcamCropRect.MinimumSize, 1)
    )
      return false;
    return crop["x"].Value<double>() + crop["width"].Value<double>() <= 1 + 1e-9
      && crop["y"].Value<double>() + crop["height"].Value<double>() <= 1 + 1e-9;
  }

  private static bool ValidNumber(JToken token, double min, double max)
  {
    if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
      return false;
    double number = token.Value<double>();
    return !double.IsNaN(number) && !double.IsInfinity(number) && number >= min && number <= max;
  }
}
