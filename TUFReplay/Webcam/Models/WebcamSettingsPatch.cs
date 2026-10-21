using System;
using AdofaiIpc;
using TUFReplay.Shared.Ipc.Generated;

namespace TUFReplay.Webcam.Models;

public sealed class WebcamSettingsPatch
{
  private readonly WebcamSettings _values;

  private WebcamSettingsPatch(WebcamSettings values)
  {
    _values = values;
  }

  public bool ChangesCapture =>
    _values.Enabled.IsSpecified
    || _values.DeviceId.IsSpecified
    || _values.Quality.IsSpecified
    || _values.RecordingLimitMb.IsSpecified
    || _values.RetentionDays.IsSpecified;
  public bool RearmsCapture =>
    _values.Enabled.IsSpecified || _values.DeviceId.IsSpecified || _values.Quality.IsSpecified;
  public bool ChangesRetention => _values.RetentionDays.IsSpecified;

  public static bool TryCreate(WebcamSettings values, out WebcamSettingsPatch patch)
  {
    patch = null;
    if (values == null)
      return false;
    if (
      !(
        values.DeviceId.IsSpecified
        || values.Crop.IsSpecified
        || values.Enabled.IsSpecified
        || values.Quality.IsSpecified
        || values.OffsetMs.IsSpecified
        || values.RecordingLimitMb.IsSpecified
        || values.RetentionDays.IsSpecified
        || values.PlaybackVisible.IsSpecified
        || values.LiveVisible.IsSpecified
        || values.Mirror.IsSpecified
        || values.FlipVertical.IsSpecified
        || values.OverlayX.IsSpecified
        || values.OverlayY.IsSpecified
        || values.OverlayWidth.IsSpecified
      )
    )
      return false;
    if (values.DeviceId.IsSpecified && values.DeviceId.Value?.Length > 4096)
      return false;
    if (
      values.Quality.IsSpecified
      && values.Quality.Value != "compact"
      && values.Quality.Value != "balanced"
      && values.Quality.Value != "quality"
    )
      return false;
    if (
      !ValidInteger(values.OffsetMs, -1000, 1000)
      || !ValidInteger(values.RecordingLimitMb, 64, 8192)
      || !ValidInteger(values.RetentionDays, 0, 30)
    )
      return false;
    if (
      !ValidNumber(values.OverlayX, -1, 2)
      || !ValidNumber(values.OverlayY, -1, 2)
      || !ValidNumber(values.OverlayWidth, 0.1, 0.5)
    )
      return false;
    if (values.Crop.IsSpecified)
    {
      CropPatch crop = values.Crop.Value;
      if (
        crop == null
        || !ValidNumber(crop.X, 0, 1)
        || !ValidNumber(crop.Y, 0, 1)
        || !ValidNumber(crop.Width, WebcamCropRect.MinimumSize, 1)
        || !ValidNumber(crop.Height, WebcamCropRect.MinimumSize, 1)
        || crop.X + crop.Width > 1 + 1e-9
        || crop.Y + crop.Height > 1 + 1e-9
      )
        return false;
    }
    patch = new WebcamSettingsPatch(values);
    return true;
  }

  public void ApplyTo(TUFReplaySetting settings)
  {
    if (_values.Enabled.IsSpecified)
      settings.WebcamEnabled = _values.Enabled.Value;
    if (_values.Quality.IsSpecified)
      settings.WebcamQuality = _values.Quality.Value;
    if (_values.OffsetMs.IsSpecified)
      settings.WebcamOffsetMs = _values.OffsetMs.Value;
    if (_values.RecordingLimitMb.IsSpecified)
      settings.WebcamRecordingLimitMb = _values.RecordingLimitMb.Value;
    if (_values.RetentionDays.IsSpecified)
      settings.WebcamRetentionDays = _values.RetentionDays.Value;
    if (_values.PlaybackVisible.IsSpecified)
      settings.WebcamPlaybackVisible = _values.PlaybackVisible.Value;
    if (_values.LiveVisible.IsSpecified)
      settings.WebcamLiveVisible = _values.LiveVisible.Value;
    if (_values.Mirror.IsSpecified)
      settings.WebcamMirror = _values.Mirror.Value;
    if (_values.FlipVertical.IsSpecified)
      settings.WebcamFlipVertical = _values.FlipVertical.Value;
    if (_values.OverlayX.IsSpecified)
      settings.WebcamOverlayX = _values.OverlayX.Value;
    if (_values.OverlayY.IsSpecified)
      settings.WebcamOverlayY = _values.OverlayY.Value;
    if (_values.OverlayWidth.IsSpecified)
      settings.WebcamOverlayWidth = _values.OverlayWidth.Value;
    if (_values.DeviceId.IsSpecified)
      settings.WebcamDeviceId = string.IsNullOrWhiteSpace(_values.DeviceId.Value)
        ? null
        : _values.DeviceId.Value.Trim();
    if (_values.OverlayX.IsSpecified || _values.OverlayY.IsSpecified)
    {
      settings.WebcamOverlayLeft = null;
      settings.WebcamOverlayTop = null;
    }
    if (_values.Crop.IsSpecified)
    {
      CropPatch crop = _values.Crop.Value;
      WebcamCropRect.Clamp(crop.X, crop.Y, crop.Width, crop.Height).ApplyTo(settings);
    }
  }

  private static bool ValidInteger(IpcOptional<int> value, int min, int max) =>
    !value.IsSpecified || value.Value >= min && value.Value <= max;

  private static bool ValidNumber(IpcOptional<double> value, double min, double max) =>
    !value.IsSpecified || ValidNumber(value.Value, min, max);

  private static bool ValidNumber(double value, double min, double max) =>
    !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
}
